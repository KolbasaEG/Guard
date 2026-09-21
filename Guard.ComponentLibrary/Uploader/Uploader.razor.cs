using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Radzen;
using System.Text.RegularExpressions;

namespace Guard.ComponentLibrary.Uploader
{
  public partial class Uploader
  {
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Parameter]
    public string UploadingFolder { get; set; } = "\\FileStorage";
    [Parameter]
    public string Formats { get; set; } = "*";
    [Parameter]
    public int? FileCount { get; set; } = 1;
    [Parameter]
    public EventCallback<string[]> ParentStringsChanged { get; set; }
    [Parameter]
    public bool AutoUpload { get; set; } = true;
    double value = 0;

    protected int counter = 1;
    protected int uploadCounter = 1;
    bool ShowProgress = false;
    bool ShowUploader = true;
    long UploadedBytes;
    long TotalBytes;
    private string path = "";
    IReadOnlyList<IBrowserFile> selectedFiles;
    List<MyIBrowserFile> myIBrowserFiles = [];
    List<MyIBrowserFile> uploadedFiles = [];
    public class MyIBrowserFile
    {
      public int Id { get; set; } = 0;
      public IBrowserFile File { get; set; }
      public string NewFileName { get; set; }
      public double Progress { get; set; } = 0;
    }
    async Task HandleSelection(InputFileChangeEventArgs eventArgs)
    {
      counter = 1;
      myIBrowserFiles.Clear();
      selectedFiles = eventArgs.GetMultipleFiles();
      if (selectedFiles != null)
      {
        if(selectedFiles.Count() > FileCount)
        {
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Error,
            Summary = $"Внимание!",
            Detail = $"Превышено допустимое количество загружаемых файлов! ("+FileCount+")"
          });
          return;
        }
        myIBrowserFiles.AddRange(selectedFiles.Select(p => new MyIBrowserFile { Id = counter++, File = p }));
        if (AutoUpload) await Upload();
      }
    }
    protected async Task DeleteFile(MouseEventArgs args, MyIBrowserFile file)
    {
      counter = 1;
      myIBrowserFiles = myIBrowserFiles.Where(p=>p != file).Select(p=> new MyIBrowserFile { Id = counter++, File = p.File, Progress = 0 }).ToList();
    }
    //-----------------------------------------------------------


   
    protected override async Task OnInitializedAsync()
    {
      if (!string.IsNullOrEmpty(UploadingFolder))
      {
        try
        {
          path = UploadingFolder;
          if (!Directory.Exists(path))
          {
            DirectoryInfo dirInfo = new(path);
            if (!dirInfo.Exists)
            {
              dirInfo.Create();
            }
          }
        }
        catch (Exception ex)
        {
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Error,
            Summary = $"Ошибка!",
            Detail = $"Добавлена новая запись!", Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" + "; " + ex.StackTrace
          });
        }
      }
    }
    async Task<string> OnLargeFileInputFileChange(IBrowserFile args)
    {
      value = 0;    
      UploadedBytes = 0;
      await InvokeAsync(StateHasChanged);
      // calculate the chunks we have to send
      TotalBytes = args.Size;
      long chunkSize = 400000;
      long numChunks = TotalBytes / chunkSize;
      long remainder = TotalBytes % chunkSize;

      string oldFilename = Path.GetFileNameWithoutExtension(args.Name);
      string extension = Path.GetExtension(args.Name);
      string newFileNameWithoutPath = $"{NormalizeFileName(oldFilename)}{extension}";
      string filename = $"{path}\\{newFileNameWithoutPath}";

      using (var inStream = args.OpenReadStream(long.MaxValue))
      {
        using (var outStream = File.OpenWrite(filename))
        {
          while (UploadedBytes < TotalBytes)
          {
            var whatsLeft = TotalBytes - UploadedBytes;
            if (whatsLeft < chunkSize)
              chunkSize = remainder;
            // Read the next chunk
            var bytes = new byte[chunkSize];
            var buffer = new Memory<byte>(bytes);
            var read = await inStream.ReadAsync(buffer);
            // Write it
            await outStream.WriteAsync(bytes, 0, read);
            // Update our progress data and UI
            UploadedBytes += read;
            value = UploadedBytes * 100 / TotalBytes;
            
            // Report progress with a string
            await InvokeAsync(StateHasChanged);
          }
        }
      }
      return newFileNameWithoutPath;
    }

    protected async Task Upload()
    {
      ShowUploader = !ShowUploader;
      ShowProgress = true;
      uploadedFiles.Clear();

      var temp = myIBrowserFiles.ToList();
      foreach (var item in temp)
      {
        item.NewFileName = await OnLargeFileInputFileChange(item.File);
        myIBrowserFiles.Remove(item);
        item.Progress = 100;
        item.Id = uploadCounter++;
        uploadedFiles.Add(item);
      }
      ShowProgress = false;
      ShowUploader = !ShowUploader;
      // Обновляем массив строк на основе загруженных файлов
      var updatedStrings = uploadedFiles.Select(p => p.NewFileName).ToArray();

      // Уведомляем родителя об изменении
      await ParentStringsChanged.InvokeAsync(updatedStrings);

    }


    private string NormalizeFileName(string fileName)
    {
      // Удаление недопустимых символов
      string normalized = Regex.Replace(fileName, @"[<>:""/\\|?*]", "_").ToLower();

      // Ограничение длины имени файла
      const int maxLength = 30;
      if (normalized.Length > maxLength)
      {
        normalized = normalized.Substring(0, maxLength);
      }
      normalized = normalized.Trim();
      normalized = normalized + "_" + Guid.NewGuid().ToString();
      return normalized;
    }
  }
}