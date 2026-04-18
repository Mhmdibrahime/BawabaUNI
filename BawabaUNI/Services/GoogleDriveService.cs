using System;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BawabaUNI.Services
{

    public class GoogleDriveService : IGoogleDriveService
    {
        private readonly DriveService _driveService;
        private readonly string _folderId;
        private readonly ILogger<GoogleDriveService> _logger;

        public GoogleDriveService(IConfiguration configuration, ILogger<GoogleDriveService> logger)
        {
            _logger = logger;
            _folderId = configuration["GoogleDrive:FolderId"]; // Optional: specific folder ID

            // Initialize Google Drive Service
            var credential = GoogleCredential.FromFile(configuration["GoogleDrive:CredentialsPath"])
                .CreateScoped(DriveService.ScopeConstants.DriveFile);

            _driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "BawabaUNI"
            });
        }

        public async Task<string> UploadFileAsync(IFormFile file, string fileName, string folderId = null)
        {
            try
            {
                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = fileName,
                    MimeType = file.ContentType
                };

                // Set parent folder if specified
                if (!string.IsNullOrEmpty(folderId) || !string.IsNullOrEmpty(_folderId))
                {
                    fileMetadata.Parents = new[] { folderId ?? _folderId };
                }

                using var stream = file.OpenReadStream();

                var request = _driveService.Files.Create(fileMetadata, stream, file.ContentType);
                request.Fields = "id, name, webContentLink";

                var result = await request.UploadAsync();

                if (result.Status == UploadStatus.Failed)
                {
                    throw new Exception($"Upload failed: {result.Exception.Message}");
                }

                var uploadedFile = request.ResponseBody;

                // Make file publicly readable
                await MakeFilePublicAsync(uploadedFile.Id);

                _logger.LogInformation($"File uploaded successfully. File ID: {uploadedFile.Id}");

                return uploadedFile.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file to Google Drive");
                throw new Exception($"Failed to upload file: {ex.Message}");
            }
        }

        private async Task MakeFilePublicAsync(string fileId)
        {
            var permission = new Permission()
            {
                Type = "anyone",
                Role = "reader"
            };

            await _driveService.Permissions.Create(permission, fileId).ExecuteAsync();
        }

        public async Task<bool> DeleteFileAsync(string fileId)
        {
            try
            {
                await _driveService.Files.Delete(fileId).ExecuteAsync();
                _logger.LogInformation($"File deleted: {fileId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete file: {fileId}");
                return false;
            }
        }

        public async Task<Stream> DownloadFileAsync(string fileId)
        {
            try
            {
                var request = _driveService.Files.Get(fileId);
                var stream = new MemoryStream();
                await request.DownloadAsync(stream);
                stream.Position = 0;
                return stream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to download file: {fileId}");
                throw new Exception($"Failed to download file: {ex.Message}");
            }
        }

        public async Task<string> GetFileUrlAsync(string fileId)
        {
            try
            {
                var file = await _driveService.Files.Get(fileId).ExecuteAsync();
                // Return direct download link
                return $"https://drive.google.com/uc?export=download&id={fileId}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get file URL: {fileId}");
                throw;
            }
        }
    }
}