using System.Globalization;
using System.IO.Compression;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.Extensions.Options;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Guard.Core.Services.Exports;

public sealed record ExportWrittenFile(string Path, string Name, string ContentType);
public sealed class ExportDocumentWriter(ExportStorage storage, IOptions<ExportOptions> options)
{
  public async Task<ExportWrittenFile> WriteAsync(ExportJob job, ExportRequest request, ExportSnapshotInfo snapshot,
    ExportDocumentInfo info, Func<CancellationToken, Task> checkpoint, CancellationToken ct)
  {
    var work = storage.WorkDirectory(job.Id);
    var extension = request.Format == ExportFormat.Excel ? ".xlsx" : ".docx";
    var headers = (request.Format == ExportFormat.Excel ? Enumerable.Range(1, snapshot.Depth).Select(n => $"Уровень {n}") : [])
      .Concat(ExportSchema.Headers(request.Area)).ToArray();
    var max = options.Value.RowsPerPart; var parts = new List<string>(); long count = 0;
    PartWriter? writer = null;
    try {
      await foreach (var row in storage.RowsAsync(job.Id, ct)) {
        if (count % 500 == 0) { await checkpoint(ct); storage.CheckSpace(); }
        if (count % max == 0) {
          writer?.Dispose(); var path = Path.Combine(work, $"part-{parts.Count + 1:D4}" + extension); parts.Add(path);
          writer = request.Format == ExportFormat.Excel ? new ExcelWriter(path, headers, info) : new WordWriter(path, headers, info);
        }
        var cells = (request.Format == ExportFormat.Excel ? Enumerable.Range(0, snapshot.Depth).Select(n => new ExportCell(n < row.Ancestors.Count ? row.Ancestors[n] : "")) : [])
          .Concat(row.Cells).ToArray();
        writer!.Add(cells); count++;
      }
      if (writer == null) {
        var path = Path.Combine(work, "part-0001" + extension); parts.Add(path);
        writer = request.Format == ExportFormat.Excel ? new ExcelWriter(path, headers, info) : new WordWriter(path, headers, info);
        writer.Add(headers.Select((_, i) => new ExportCell(i == 0 ? "Записи не найдены" : "")).ToArray());
      }
    }
    finally { writer?.Dispose(); }
    if (count != snapshot.Count) throw new InvalidDataException("Число строк снимка не совпадает.");
    var name = $"{info.Title}-{job.Id}";
    if (parts.Count == 1) return new(parts[0], name + extension, request.Format == ExportFormat.Excel ?
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    var zipPath = Path.Combine(work, "export.zip");
    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create)) {
      for (var i = 0; i < parts.Count; i++) {
        ct.ThrowIfCancellationRequested(); storage.CheckSpace();
        archive.CreateEntryFromFile(parts[i], $"{name}-часть-{i + 1:D4}" + extension, CompressionLevel.Fastest);
      }
      var manifest = archive.CreateEntry("Сведения.txt");
      await using var output = manifest.Open(); await using var text = new StreamWriter(output);
      await text.WriteAsync($"Выгрузка: {info.Id}\nАвтор: {info.Author}\nДата UTC: {info.CreatedAtUtc:O}\nЧастей: {parts.Count}\nСтрок: {snapshot.Count}\n");
    }
    return new(zipPath, name + ".zip", "application/zip");
  }
  public static string SafeText(string text)
  {
    if (text.Length > 32767) throw new ArgumentException("Значение превышает допустимую длину ячейки.");
    for (var i = 0; i < text.Length; i++) {
      if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) { i++; continue; }
      if (!XmlConvert.IsXmlChar(text[i])) throw new ArgumentException("Значение содержит недопустимый символ документа.");
    }
    return text;
  }
  public static string ColumnName(int number)
  {
    var result = "";
    do { result = (char)('A' + number % 26) + result; number = number / 26 - 1; } while (number >= 0);
    return result;
  }
  private abstract class PartWriter : IDisposable { public abstract void Add(IReadOnlyList<ExportCell> cells); public abstract void Dispose(); }
  private sealed class ExcelWriter : PartWriter
  {
    private readonly SpreadsheetDocument document; private readonly WorksheetPart sheet; private readonly OpenXmlWriter xml;
    private readonly int columns; private uint row = 1; private bool closed;
    public ExcelWriter(string path, string[] headers, ExportDocumentInfo info)
    {
      columns = headers.Length; document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
      var workbook = document.AddWorkbookPart(); workbook.Workbook = new S.Workbook(new S.Sheets());
      var styles = workbook.AddNewPart<WorkbookStylesPart>();
      styles.Stylesheet = new S.Stylesheet(new S.Fonts(new S.Font(), new S.Font(new S.Bold())),
        new S.Fills(new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }), new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 })),
        new S.Borders(new S.Border()), new S.CellStyleFormats(new S.CellFormat()),
        new S.CellFormats(new S.CellFormat(), new S.CellFormat { FontId = 1, ApplyFont = true })); styles.Stylesheet.Save();
      sheet = workbook.AddNewPart<WorksheetPart>(); var sheets = workbook.Workbook.GetFirstChild<S.Sheets>()!;
      sheets.Append(new S.Sheet { Id = workbook.GetIdOfPart(sheet), SheetId = 1, Name = "Данные" });
      xml = OpenXmlWriter.Create(sheet); xml.WriteStartElement(new S.Worksheet());
      xml.WriteElement(new S.SheetViews(new S.SheetView(new S.Pane { VerticalSplit = 1, TopLeftCell = "A2", ActivePane = S.PaneValues.BottomLeft, State = S.PaneStateValues.Frozen }) { WorkbookViewId = 0 }));
      xml.WriteElement(new S.Columns(headers.Select((_, i) => new S.Column { Min = (uint)i + 1, Max = (uint)i + 1, Width = 24, CustomWidth = true })));
      xml.WriteStartElement(new S.SheetData()); WriteRow(headers.Select(h => new ExportCell(h)).ToArray(), true);
      var meta = workbook.AddNewPart<WorksheetPart>();
      sheets.Append(new S.Sheet { Id = workbook.GetIdOfPart(meta), SheetId = 2, Name = "Сведения о выгрузке" });
      using var writer = OpenXmlWriter.Create(meta); writer.WriteStartElement(new S.Worksheet()); writer.WriteStartElement(new S.SheetData());
      var zone = TimeZoneInfo.FindSystemTimeZoneById(info.TimeZone);
      foreach (var pair in new[] {
        ("Номер выгрузки", info.Id.ToString()), ("Отчёт", info.Title), ("Автор", info.Author),
        ("Сформировано", TimeZoneInfo.ConvertTime(info.CreatedAtUtc, zone).ToString("dd.MM.yyyy HH:mm:ss zzz")),
        ("Часовой пояс", info.TimeZone), ("Время UTC", info.CreatedAtUtc.ToString("O")),
        ("Условия", info.Conditions), ("Версия состава полей", info.SchemaVersion.ToString()) }) {
        writer.WriteStartElement(new S.Row()); writer.WriteElement(TextCell(pair.Item1)); writer.WriteElement(TextCell(pair.Item2)); writer.WriteEndElement();
      }
      writer.WriteEndElement(); writer.WriteEndElement(); workbook.Workbook.Save();
    }
    private static S.Cell TextCell(string value) => new(new S.InlineString(new S.Text(SafeText(value)) { Space = SpaceProcessingModeValues.Preserve })) { DataType = S.CellValues.InlineString };
    private void WriteRow(IReadOnlyList<ExportCell> values, bool header = false)
    {
      if (values.Count != columns) throw new InvalidDataException("Неверное число колонок.");
      xml.WriteStartElement(new S.Row { RowIndex = row });
      for (var i = 0; i < values.Count; i++) {
        var value = values[i]; var cell = value.Number is double n ? new S.Cell(new S.CellValue(n.ToString("R", CultureInfo.InvariantCulture))) { DataType = S.CellValues.Number } :
          value.Boolean is bool b ? new S.Cell(new S.CellValue(b ? "1" : "0")) { DataType = S.CellValues.Boolean } : TextCell(value.Text);
        cell.CellReference = ColumnName(i) + row; cell.StyleIndex = header ? 1U : 0U; xml.WriteElement(cell);
      }
      xml.WriteEndElement(); row++;
    }
    public override void Add(IReadOnlyList<ExportCell> values) => WriteRow(values);
    public override void Dispose()
    {
      if (closed) return; closed = true;
      xml.WriteEndElement(); xml.WriteElement(new S.AutoFilter { Reference = $"A1:{ColumnName(columns - 1)}{row - 1}" }); xml.WriteEndElement(); xml.Dispose(); document.Dispose();
    }
  }
  private sealed class WordWriter : PartWriter
  {
    private readonly WordprocessingDocument document; private readonly OpenXmlWriter xml;
    private readonly int columns; private readonly string footerId; private bool closed;
    public WordWriter(string path, string[] headers, ExportDocumentInfo info)
    {
      columns = headers.Length; document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
      var main = document.AddMainDocumentPart(); var footer = main.AddNewPart<FooterPart>(); footerId = main.GetIdOfPart(footer);
      footer.Footer = new W.Footer(new W.Paragraph(new W.Run(new W.Text($"Выгрузка {info.Id} · {info.Author} · ")), new W.SimpleField { Instruction = "PAGE" })); footer.Footer.Save();
      xml = OpenXmlWriter.Create(main); xml.WriteStartElement(new W.Document()); xml.WriteStartElement(new W.Body());
      Paragraph(info.Title, true); Paragraph($"Выгрузка: {info.Id}"); Paragraph($"Автор: {info.Author}");
      Paragraph($"Сформировано: {TimeZoneInfo.ConvertTime(info.CreatedAtUtc, TimeZoneInfo.FindSystemTimeZoneById(info.TimeZone)):dd.MM.yyyy HH:mm:ss zzz}");
      Paragraph(info.Conditions);
      xml.WriteStartElement(new W.Table());
      xml.WriteElement(new W.TableProperties(new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct },
        new W.TableBorders(new W.TopBorder { Val = W.BorderValues.Single, Size = 4 }, new W.LeftBorder { Val = W.BorderValues.Single, Size = 4 },
          new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 }, new W.RightBorder { Val = W.BorderValues.Single, Size = 4 },
          new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 }, new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 }),
        new W.TableLayout { Type = W.TableLayoutValues.Fixed }));
      xml.WriteElement(new W.TableGrid(headers.Select(_ => new W.GridColumn { Width = (15000 / columns).ToString() })));
      WriteRow(headers.Select(h => new ExportCell(h)).ToArray(), true);
    }
    private void Paragraph(string value, bool bold = false, bool table = false)
    {
      var props = new W.RunProperties(); if (bold) props.Append(new W.Bold());
      props.Append(new W.FontSize { Val = table ? "18" : bold ? "28" : "20" });
      xml.WriteElement(new W.Paragraph(new W.Run(props, new W.Text(SafeText(value)) { Space = SpaceProcessingModeValues.Preserve })));
    }
    private void WriteRow(IReadOnlyList<ExportCell> values, bool header = false)
    {
      if (values.Count != columns) throw new InvalidDataException("Неверное число колонок.");
      xml.WriteStartElement(new W.TableRow()); if (header) xml.WriteElement(new W.TableRowProperties(new W.TableHeader()));
      foreach (var value in values) {
        xml.WriteStartElement(new W.TableCell()); xml.WriteElement(new W.TableCellProperties(new W.TableCellWidth { Width = (15000 / columns).ToString(), Type = W.TableWidthUnitValues.Dxa }));
        Paragraph(value.Text, header, true); xml.WriteEndElement();
      }
      xml.WriteEndElement();
    }
    public override void Add(IReadOnlyList<ExportCell> values) => WriteRow(values);
    public override void Dispose()
    {
      if (closed) return; closed = true;
      xml.WriteEndElement(); xml.WriteElement(new W.SectionProperties(new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = footerId },
        new W.PageSize { Width = 16838, Height = 11906, Orient = W.PageOrientationValues.Landscape },
        new W.PageMargin { Top = 720, Bottom = 720, Left = 720, Right = 720, Header = 360, Footer = 360, Gutter = 0 }));
      xml.WriteEndElement(); xml.WriteEndElement(); xml.Dispose(); document.Dispose();
    }
  }
}
