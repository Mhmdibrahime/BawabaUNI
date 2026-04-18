using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BawabaUNI.Services
{
   
    public class BackblazeService : IBackblazeService
    {
        private readonly string _bucketId;
        private readonly string _applicationKeyId;
        private readonly string _applicationKey;
        private readonly string _bucketName;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BackblazeService> _logger;

        private string _apiUrl;
        private string _accountAuthToken;  // This is the master auth token from b2_authorize_account
        private string _downloadUrl;
        private DateTime _tokenExpiry;
        private readonly SemaphoreSlim _tokenSemaphore = new SemaphoreSlim(1, 1);

        public BackblazeService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<BackblazeService> logger)
        {
            _bucketId = configuration["Backblaze:BucketId"];
            _applicationKeyId = configuration["Backblaze:KeyId"];
            _applicationKey = configuration["Backblaze:ApplicationKey"];
            _bucketName = configuration["Backblaze:BucketName"];
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // Authorize account and get API URL and auth token
        private async Task AuthorizeAccountAsync()
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "BawabaUNI/1.0");

            var authString = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_applicationKeyId}:{_applicationKey}")
            );

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authString);

            var response = await client.GetAsync("https://api.backblazeb2.com/b2api/v2/b2_authorize_account");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to authorize with Backblaze: {error}");
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"Auth response received");

            // Parse the response
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            _apiUrl = root.GetProperty("apiUrl").GetString();
            _accountAuthToken = root.GetProperty("authorizationToken").GetString();
            _downloadUrl = root.GetProperty("downloadUrl").GetString();

            _logger.LogInformation($"Backblaze authorized. API URL: {_apiUrl}");
            _logger.LogInformation($"Auth Token (first 50 chars): {_accountAuthToken?.Substring(0, Math.Min(50, _accountAuthToken.Length))}...");

            // Token expires in 24 hours, set expiry 1 hour early for safety
            _tokenExpiry = DateTime.UtcNow.AddHours(23);
        }

        // Ensure valid token before each operation
        private async Task EnsureValidTokenAsync()
        {
            if (string.IsNullOrEmpty(_accountAuthToken) || DateTime.UtcNow >= _tokenExpiry)
            {
                await _tokenSemaphore.WaitAsync();
                try
                {
                    if (string.IsNullOrEmpty(_accountAuthToken) || DateTime.UtcNow >= _tokenExpiry)
                    {
                        await AuthorizeAccountAsync();
                    }
                }
                finally
                {
                    _tokenSemaphore.Release();
                }
            }
        }

        // Get upload URL with retry logic
        private async Task<(string uploadUrl, string authToken)> GetUploadUrlWithRetryAsync(int maxRetries = 3)
        {
            await EnsureValidTokenAsync();

            var client = _httpClientFactory.CreateClient();

            // IMPORTANT: Use the account auth token from b2_authorize_account
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accountAuthToken);
            client.DefaultRequestHeaders.Add("User-Agent", "BawabaUNI/1.0");

            var requestContent = new StringContent(
                $"{{\"bucketId\":\"{_bucketId}\"}}",
                Encoding.UTF8,
                "application/json"
            );

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var requestUrl = $"{_apiUrl}/b2api/v2/b2_get_upload_url";
                    _logger.LogInformation($"Requesting upload URL from: {requestUrl}");
                    _logger.LogInformation($"Using auth token: {_accountAuthToken?.Substring(0, Math.Min(50, _accountAuthToken?.Length ?? 0))}...");

                    var response = await client.PostAsync(requestUrl, requestContent);

                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Response Status: {response.StatusCode}");
                    _logger.LogInformation($"Response Content: {responseContent}");

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(responseContent);
                        var root = doc.RootElement;
                        var uploadUrl = root.GetProperty("uploadUrl").GetString();
                        var authorizationToken = root.GetProperty("authorizationToken").GetString();

                        _logger.LogInformation($"Got upload URL: {uploadUrl}");
                        return (uploadUrl, authorizationToken);
                    }

                    // Handle specific error codes
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _logger.LogWarning("Authorization failed. Force re-authorization.");
                        // Force token refresh
                        _tokenExpiry = DateTime.UtcNow;
                        await EnsureValidTokenAsync();

                        // Update the authorization header with new token
                        client.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", _accountAuthToken);
                        continue;
                    }

                    if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                    {
                        var retryAfter = response.Headers.RetryAfter?.Delta?.Seconds ??
                                        (int)Math.Pow(2, attempt);
                        _logger.LogWarning($"Upload URL request failed. Retrying after {retryAfter}s. Attempt {attempt}/{maxRetries}");
                        await Task.Delay(TimeSpan.FromSeconds(retryAfter));
                        continue;
                    }

                    throw new Exception($"Failed to get upload URL: {responseContent}");
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, $"Connection error. Attempt {attempt}/{maxRetries}");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, $"Error getting upload URL. Attempt {attempt}/{maxRetries}");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
            }

            throw new Exception($"Failed to get upload URL after {maxRetries} attempts");
        }

        // Upload file with retry logic
        public async Task<string> UploadFileAsync(IFormFile file, string fileName)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty");

            if (file.Length > 500 * 1024 * 1024)
                throw new ArgumentException("File size exceeds 500MB limit");

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();
            var sha1 = ComputeSha1(fileBytes);

            var maxRetries = 3;
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var (uploadUrl, uploadAuthToken) = await GetUploadUrlWithRetryAsync();

                    if (string.IsNullOrEmpty(uploadUrl))
                    {
                        throw new Exception("Upload URL is null or empty");
                    }

                    var client = _httpClientFactory.CreateClient();

                    // IMPORTANT: Use the upload auth token from b2_get_upload_url, NOT the account token
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", uploadAuthToken);
                    client.DefaultRequestHeaders.Add("User-Agent", "BawabaUNI/1.0");

                    client.DefaultRequestHeaders.Add("X-Bz-File-Name", EncodeFileName(fileName));
                    client.DefaultRequestHeaders.Add("X-Bz-Content-Sha1", sha1);
                    client.DefaultRequestHeaders.Add("X-Bz-Info-src_last_modified_millis",
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

                    using var content = new ByteArrayContent(fileBytes);
                    content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                    _logger.LogInformation($"Uploading to: {uploadUrl}");
                    _logger.LogInformation($"File name: {fileName}");
                    _logger.LogInformation($"File size: {fileBytes.Length} bytes");
                    _logger.LogInformation($"SHA1: {sha1}");

                    var response = await client.PostAsync(uploadUrl, content);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation($"Upload successful!");
                        var publicUrl = $"https://f006.backblazeb2.com/file/{_bucketName}/{fileName}";
                        return publicUrl;
                    }

                    _logger.LogError($"Upload failed with status {response.StatusCode}: {responseContent}");

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _logger.LogInformation("Upload auth token expired, getting new one");
                        continue;
                    }

                    if ((int)response.StatusCode == 408 || (int)response.StatusCode == 429 ||
                        ((int)response.StatusCode >= 500 && (int)response.StatusCode <= 599))
                    {
                        var retryAfter = response.Headers.RetryAfter?.Delta?.Seconds ??
                                        (int)Math.Pow(2, attempt);
                        _logger.LogWarning($"Upload failed. Retrying after {retryAfter}s. Attempt {attempt}/{maxRetries}");
                        await Task.Delay(TimeSpan.FromSeconds(retryAfter));
                        continue;
                    }

                    throw new Exception($"Upload failed: {responseContent}");
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, $"Connection error. Attempt {attempt}/{maxRetries}");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, $"Upload attempt {attempt}/{maxRetries} failed: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
            }

            throw new Exception($"Failed to upload after {maxRetries} attempts", lastException);
        }

        // Download file with range support
        public async Task<Stream> DownloadFileAsync(string fileUrl, long startByte = 0, long endByte = 0)
        {
            await EnsureValidTokenAsync();

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accountAuthToken);
            client.DefaultRequestHeaders.Add("User-Agent", "BawabaUNI/1.0");

            if (endByte > startByte)
            {
                client.DefaultRequestHeaders.Range = new RangeHeaderValue(startByte, endByte);
            }

            var response = await client.GetAsync(fileUrl);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to download file: {response.StatusCode}");
            }

            return await response.Content.ReadAsStreamAsync();
        }

        // Delete file
        public async Task<bool> DeleteFileAsync(string fileUrl)
        {
            await EnsureValidTokenAsync();

            var fileName = fileUrl.Split('/').Last();

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accountAuthToken);
            client.DefaultRequestHeaders.Add("User-Agent", "BawabaUNI/1.0");

            var listRequest = new StringContent(
                $"{{\"bucketId\":\"{_bucketId}\",\"startFileName\":\"{fileName}\",\"maxFileCount\":1}}",
                Encoding.UTF8,
                "application/json"
            );

            var listResponse = await client.PostAsync(
                $"{_apiUrl}/b2api/v2/b2_list_file_names",
                listRequest
            );

            if (!listResponse.IsSuccessStatusCode)
                return false;

            var listContent = await listResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(listContent);
            var files = doc.RootElement.GetProperty("files");

            if (files.GetArrayLength() == 0)
                return false;

            var file = files[0];
            var fileId = file.GetProperty("fileId").GetString();

            var deleteRequest = new StringContent(
                $"{{\"fileId\":\"{fileId}\",\"fileName\":\"{fileName}\"}}",
                Encoding.UTF8,
                "application/json"
            );

            var deleteResponse = await client.PostAsync(
                $"{_apiUrl}/b2api/v2/b2_delete_file_version",
                deleteRequest
            );

            return deleteResponse.IsSuccessStatusCode;
        }

        private string ComputeSha1(byte[] data)
        {
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private string EncodeFileName(string fileName)
        {
            return Uri.EscapeDataString(fileName);
        }
    }
}