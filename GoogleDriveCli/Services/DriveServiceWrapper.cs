using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using File = System.IO.File;
using GoogleDriveCli.Models;

namespace GoogleDriveCli.Services;

public class DriveServiceWrapper
{
    private readonly DriveService _service;

    public DriveServiceWrapper(DriveService service)
    {
        _service = service;
    }

    // ------------------------------------------------------------
    // Get all files (handles pagination)
    // ------------------------------------------------------------
    public async Task<IList<Google.Apis.Drive.v3.Data.File>> GetAllFilesAsync()
    {
        var list = new List<Google.Apis.Drive.v3.Data.File>();
        string? pageToken = null;

        do
        {
            var request = _service.Files.List();
            request.PageSize = 1000;
            request.Q = "'me' in owners";
            request.Fields = "nextPageToken, files(id, name, mimeType, parents)";
            request.PageToken = pageToken;

            var result = await request.ExecuteAsync();
            if (result.Files != null)
                list.AddRange(result.Files);

            pageToken = result.NextPageToken;
        }
        while (pageToken != null);

        return list;
    }

    // ------------------------------------------------------------
    // Download file
    // ------------------------------------------------------------
    public async Task<bool> DownloadFileAsync(string fileId, string localPath, SyncStatistics stats)
    {
        try
        {
            var request = _service.Files.Get(fileId);
            await using var stream = new FileStream(localPath, FileMode.Create, FileAccess.Write);
            await request.DownloadAsync(stream);
            stats.IncrementDownloaded();
            return true;
        }
        catch
        {
            stats.IncrementFailed();
            return false;
        }
    }

    // ------------------------------------------------------------
    // Search files
    // ------------------------------------------------------------
    public async Task<IList<Google.Apis.Drive.v3.Data.File>> SearchFilesAsync(string query)
    {
        var list = new List<Google.Apis.Drive.v3.Data.File>();
        string? pageToken = null;

        do
        {
            var request = _service.Files.List();
            request.PageSize = 1000;
            request.Q = $"name contains '{query.Replace("'", "\\'")}'";
            request.Fields = "nextPageToken, files(id, name, mimeType, parents)";
            request.PageToken = pageToken;

            var result = await request.ExecuteAsync();
            if (result.Files != null)
                list.AddRange(result.Files);
            pageToken = result.NextPageToken;
        }
        while (pageToken != null);

        return list;
    }

    // ------------------------------------------------------------
    // Resolve folder ID by path (e.g., "Folder1/SubFolder")
    // ------------------------------------------------------------
    public async Task<string?> ResolveFolderIdByPathAsync(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? parentId = "root";

        foreach (var part in parts)
        {
            var request = _service.Files.List();
            request.Q = $"'{parentId}' in parents and name='{part.Replace("'", "\\'")}' and mimeType='application/vnd.google-apps.folder' and trashed=false";
            request.Fields = "files(id)";
            request.PageSize = 1;

            var result = await request.ExecuteAsync();
            if (result.Files == null || result.Files.Count == 0)
                return null; // folder doesn’t exist

            parentId = result.Files[0].Id;
        }

        return parentId;
    }

    // ------------------------------------------------------------
    // Upload file
    // ------------------------------------------------------------
    public async Task<Google.Apis.Drive.v3.Data.File?> UploadFileAsync(string localPath, string folderId)
    {
        var fileMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = Path.GetFileName(localPath),
            Parents = new List<string> { folderId }
        };

        await using var stream = File.OpenRead(localPath);
        var request = _service.Files.Create(fileMetadata, stream, GetMimeType(localPath));
        request.Fields = "id, name, size";
        var uploadedFile = await request.UploadAsync();

        if (uploadedFile.Status == Google.Apis.Upload.UploadStatus.Failed)
            throw new Exception($"Upload failed: {uploadedFile.Exception}");

        return ((FilesResource.CreateMediaUpload)request).ResponseBody;
    }

    private string GetMimeType(string fileName)
    {
        // Basic MIME map – extend as needed
        return Path.GetExtension(fileName).ToLower() switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }
}