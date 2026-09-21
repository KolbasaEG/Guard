using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System.Data;
using System.Linq.Dynamic.Core;
using System.Text.RegularExpressions;

namespace Guard.ComponentLibrary.Gallery
{
  public partial class Gallery
  {
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Parameter]
    public string Folder { get; set; } = "\\FileStorage";
    [Parameter]
    public bool IsEditor { get; set; } = false;

    int filesCount = 0;
    int imagesCount = 0;
    int selectedIndex = 0;
    int pageSize = 6;
    private string path = "";
    protected bool isLoading = false;
    List<SelectedFile> Files = [];
    List<SelectedFile> Images = [];
    IEnumerable<SelectedFile> imagesList = [];
    public required RadzenDataGrid<SelectedFile> FilesGrid;
    string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";
    private string[] parentStrings;

    void PageChanged(PagerEventArgs args)
    {
      imagesList = GetOrders(args.Skip, args.Top);
    }
    IEnumerable<SelectedFile> GetOrders(int skip, int take)
    {
      return Images.Skip(skip).Take(take).ToList();
    }

    private async void OnParentStringsChanged(string[] updatedStrings)
    {
      parentStrings = updatedStrings;
      LoadData();
      NotificationService.Notify(new NotificationMessage
      {
        Severity = NotificationSeverity.Success,
        Summary = $"Информационное",
        Detail = $"Загружено {parentStrings.Length} файлов!"
      });
      
    }
    protected override async Task OnInitializedAsync()
    {
      if (!string.IsNullOrEmpty(Folder))
      {
        try
        {
          LoadData();
        }
        catch (Exception ex)
        {
          //NotificationService.Notify(new NotificationMessage
          //{
          //  Severity = NotificationSeverity.Error,
          //  Summary = $"Ошибка!",
          //  Detail = $"Добавлена новая запись!", Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" + "; " + ex.StackTrace
          //});
        }
      }
    }
    private string GetIconClass(string url)
    {
      string extension = Path.GetExtension(url).ToLower();

      return extension switch
      {
        ".pdf" => "picture_as_pdf",
        ".doc" or ".docx" => "description",
        ".xls" or ".xlsx" => "table_view",
        ".ppt" or ".pptx" => "contextual_token_add",
        ".txt" => "reorder",
        ".jpg" or ".jpeg" or ".png" or ".gif" => "image",
        ".zip" or ".rar" => "inventory_2",
        _ => "draft" // Иконка для неопознанных файлов
      };
    }
    public (List<SelectedFile> Images, List<SelectedFile> OtherFiles) GetFiles(string directoryPath)
    {
      if (!Directory.Exists(directoryPath))
      {
        throw new DirectoryNotFoundException($"Каталог не найден: {directoryPath}");
      }

      try
      {
        var files = Directory.GetFiles(directoryPath);
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };

        var imageFiles = files
            .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()))
            .Select(f => new SelectedFile
            {
              Created = File.GetCreationTime(f).ToShortDateString() + " " + File.GetCreationTime(f).ToLongTimeString(),
              Name = TrimFileName(Path.GetFileNameWithoutExtension(f)),
              Extension = Path.GetExtension(f),
              Url = f, // Здесь можно указать URL, если нужно
              Icon = GetIconClass(f),
              SizeInMB = Math.Round(new System.IO.FileInfo(f).Length / (1024.0 * 1024.0),2) // Размер в МБ
            })
            .ToList();

        var otherFiles = files
            .Where(f => !imageExtensions.Contains(Path.GetExtension(f).ToLower()))
            .Select(f => new SelectedFile
            {
              Name = TrimFileName(Path.GetFileNameWithoutExtension(f)),
              Extension = Path.GetExtension(f),
              Url = f, // Здесь можно указать URL, если нужно
              Icon = GetIconClass(f),
              SizeInMB = Math.Round(new System.IO.FileInfo(f).Length / (1024.0 * 1024.0),2) // Размер в МБ
            })
            .ToList();

        return (imageFiles, otherFiles);
      }
      catch (Exception ex)
      {
        // Логирование или обработка исключений
        throw new Exception("Ошибка при получении файлов.", ex);
      }
    }
    private string TrimFileName(string fileName)
    {
      // Удаляем символы "_" и GUID в конце имени
      if (fileName.Contains("_"))
      {
        fileName = fileName.Substring(0, fileName.LastIndexOf("_"));
      }

      // Удаляем GUID, если он есть в конце
      var guidPattern = new Regex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");
      return guidPattern.Replace(fileName.TrimEnd(), string.Empty).TrimEnd();
    }
    void LoadData()
    {
      isLoading = !isLoading;
      if (!string.IsNullOrEmpty(Folder))
      {
        path = Folder;
        if (!Directory.Exists(path))
        {
          DirectoryInfo dirInfo = new(path);
          if (!dirInfo.Exists)
          {
            dirInfo.Create();
          }
        }
        
        var temp = GetFiles(Folder);
        Files = temp.OtherFiles;
        Images = temp.Images;
        filesCount = Files.Count;
        imagesCount = Images.Count;
        imagesList = GetOrders(0, pageSize);
        if (Images.Count > 0)
        {
          selectedIndex = 0;
        }
        else
        {
          selectedIndex = 1;
        }
        try
        {

        }
        catch (Exception ex)
        {
          //NotificationService.Notify(new NotificationMessage
          //{
          //  Severity = NotificationSeverity.Error,
          //  Summary = $"Ошибка!",
          //  Detail = $"Добавлена новая запись!", Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" + "; " + ex.StackTrace
          //});
        }
      }
      isLoading = !isLoading;
    }
    public class SelectedFile
    {
      public string Created { get; set; } = string.Empty;
      public string Name { get; set; } = string.Empty;
      public string Extension { get; set; } = string.Empty;
      public string Url { get; set; } = string.Empty;
      public string Icon { get; set; } = string.Empty;
      public double SizeInMB { get; set; } // Размер файла в мегабайтах
    }
    private async Task DownloadFile(MouseEventArgs args, SelectedFile item)
    {
      var fileUrl = item.Url; // Укажите путь к файлу
      await JSRuntime.InvokeVoidAsync("downloadFile", fileUrl, "" + item.Name + item.Extension);
    }
    async Task GridDeleteButtonClick(MouseEventArgs args, SelectedFile item)
    {
      if (await DialogService.Confirm("Вы действительно хотите удалить файл?", "Удаление", new ConfirmOptions { OkButtonText = "Удалить", CancelButtonText = "Отмена" }) == true)
      {
        System.IO.File.Delete(item.Url);
        try
        {
          System.IO.File.Delete(item.Url);
          LoadData();
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Success,
            Summary = $"Информационное",
            Detail = "Файл успешно удален!"
          });
        }
        catch
        {
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Error,
            Summary = $"Ошибка!",
            Detail = $"При удалении файла произошла ошибка!"
          });
        }

      }
    }
    async Task ViewImage(MouseEventArgs args, SelectedFile item)
    {
      await DialogService.OpenAsync<ViewSingleImage>("", new Dictionary<string, object> { { "ImageFolder", item.Url } }, new DialogOptions() { Width = "0vw", Height = "0vh", Resizable = false, ShowTitle = false, ContentCssClass = "rz-p-2" });
    }
  }
}