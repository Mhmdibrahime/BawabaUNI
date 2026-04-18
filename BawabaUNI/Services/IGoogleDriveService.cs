namespace BawabaUNI.Services
{
    public interface IGoogleDriveService
    {
        Task<string> UploadFileAsync(IFormFile file, string fileName, string folderId = null);
        Task<bool> DeleteFileAsync(string fileId);
        Task<Stream> DownloadFileAsync(string fileId);
        Task<string> GetFileUrlAsync(string fileId);
    }
}