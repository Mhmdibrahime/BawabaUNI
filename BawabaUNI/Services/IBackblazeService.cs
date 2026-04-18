namespace BawabaUNI.Services
{
    public interface IBackblazeService
    {
        Task<string> UploadFileAsync(IFormFile file, string fileName);
        Task<bool> DeleteFileAsync(string fileUrl);
        Task<Stream> DownloadFileAsync(string fileUrl, long startByte = 0, long endByte = 0);
    }
}