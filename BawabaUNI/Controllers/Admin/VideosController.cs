using BawabaUNI.Models.Data;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/Admin/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]

    public class VideosController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly HttpClient _httpClient;

        public VideosController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = new HttpClient();

            // Configure Vimeo API
            var accessToken = _configuration["Vimeo:AccessToken"];
            if (!string.IsNullOrEmpty(accessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }
       
        // Vimeo API Integration
        private async Task<string> UploadToVimeo(IFormFile videoFile)
        {
            try
            {
                // Step 1: Create a video on Vimeo
                var createVideoRequest = new
                {
                    upload = new
                    {
                        approach = "tus",
                        size = videoFile.Length.ToString()
                    }
                };

                var createVideoContent = new StringContent(
                    JsonSerializer.Serialize(createVideoRequest),
                    Encoding.UTF8,
                    "application/json");

                var createResponse = await _httpClient.PostAsync(
                    "https://api.vimeo.com/me/videos",
                    createVideoContent);

                if (!createResponse.IsSuccessStatusCode)
                {
                    var errorContent = await createResponse.Content.ReadAsStringAsync();
                    throw new Exception($"Vimeo API error: {errorContent}");
                }

                var vimeoResponse = JsonSerializer.Deserialize<VimeoUploadResponse>(
                    await createResponse.Content.ReadAsStringAsync());

                // Step 2: Upload the file using TUS protocol
                using var fileStream = videoFile.OpenReadStream();
                using var content = new StreamContent(fileStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");

                var uploadResponse = await _httpClient.PatchAsync(
                    vimeoResponse.upload.upload_link,
                    content);

                if (uploadResponse.IsSuccessStatusCode)
                {
                    // Wait for processing and get the final video link
                    await Task.Delay(5000); // Wait 5 seconds for processing

                    var videoUri = vimeoResponse.uri;
                    var videoInfoResponse = await _httpClient.GetAsync(
                        $"https://api.vimeo.com{videoUri}");

                    if (videoInfoResponse.IsSuccessStatusCode)
                    {
                        var videoInfo = JsonSerializer.Deserialize<JsonElement>(
                            await videoInfoResponse.Content.ReadAsStringAsync());

                        if (videoInfo.TryGetProperty("link", out var linkProperty))
                        {
                            return linkProperty.GetString();
                        }
                    }

                    // Fallback to the initial link
                    return vimeoResponse.link;
                }
                else
                {
                    throw new Exception("Failed to upload video to Vimeo");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Vimeo upload failed: {ex.Message}");
            }
        }
        [HttpGet("test-vimeo")]
        public async Task<IActionResult> TestVimeoConnection()
        {
            try
            {
                var response = await _httpClient.GetAsync("https://api.vimeo.com/me");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Ok(new
                    {
                        success = true,
                        message = "Vimeo connection successful",
                        user = JsonSerializer.Deserialize<object>(content)
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = await response.Content.ReadAsStringAsync()
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }
        // POST: api/Admin/Videos/upload-ticket
        [HttpPost("upload-ticket")]
        public async Task<ActionResult<object>> CreateUploadTicket([FromBody] UploadTicketRequest request)
        {
            try
            {
                // Create a video on Vimeo
                var createVideoRequest = new
                {
                    upload = new
                    {
                        approach = "post", // Use POST approach for form upload
                        size = request.FileSize.ToString()
                    },
                    name = request.Title,
                    description = request.Description,
                    privacy = new
                    {
                        view = "disable", // Hide from Vimeo site
                        embed = "public" // Allow embedding
                    }
                };

                var createVideoContent = new StringContent(
                    JsonSerializer.Serialize(createVideoRequest),
                    Encoding.UTF8,
                    "application/json");

                var createResponse = await _httpClient.PostAsync(
                    "https://api.vimeo.com/me/videos",
                    createVideoContent);

                if (!createResponse.IsSuccessStatusCode)
                {
                    var errorContent = await createResponse.Content.ReadAsStringAsync();
                    return BadRequest(new
                    {
                        error = "Failed to create upload ticket",
                        details = errorContent
                    });
                }

                var vimeoResponse = JsonSerializer.Deserialize<JsonElement>(
                    await createResponse.Content.ReadAsStringAsync());

                // Get upload information
                var upload = vimeoResponse.GetProperty("upload");
                var uploadLink = upload.GetProperty("upload_link").GetString();
                var videoUri = vimeoResponse.GetProperty("uri").GetString();
                var videoId = videoUri.Split('/').Last();
                var videoLink = vimeoResponse.GetProperty("link").GetString();

                // Store the video info temporarily in database (or cache)
                var tempVideo = new Video
                {
                    Title = request.Title,
                    DurationInMinutes = request.DurationInMinutes,
                    IsPaid = request.IsPaid,
                    Description = request.Description,
                    VideoLink = videoLink,
                    CourseId = request.CourseId,
                    CreatedAt = DateTime.UtcNow,
                    // Mark as temporary until upload is confirmed
                };

                _context.Videos.Add(tempVideo);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    upload_link = uploadLink,
                    video_uri = videoUri,
                    video_id = videoId,
                    video_link = videoLink,
                    temp_video_id = tempVideo.Id,
                    upload_form = upload.GetProperty("form").GetString(),
                    upload_type = "form"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        // GET: api/Admin/Videos/check-status/{videoId}
        [HttpGet("check-status/{videoId}")]
        public async Task<ActionResult<object>> CheckVideoStatus(string videoId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"https://api.vimeo.com/videos/{videoId}");

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        success = false,
                        error = "Failed to check video status"
                    });
                }

                var videoInfo = JsonSerializer.Deserialize<JsonElement>(
                    await response.Content.ReadAsStringAsync());

                string status = videoInfo.GetProperty("status").GetString();
                string name = videoInfo.GetProperty("name").GetString();
                int duration = videoInfo.GetProperty("duration").GetInt32();
                string link = videoInfo.GetProperty("link").GetString();

                return Ok(new
                {
                    success = true,
                    video_id = videoId,
                    status = status,
                    name = name,
                    duration = duration,
                    link = link,
                    is_playable = status == "available",
                    message = GetStatusMessage(status)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        private string GetStatusMessage(string status)
        {
            return status switch
            {
                "uploading" => "Video is being uploaded",
                "transcoding" => "Video is being processed",
                "available" => "Video is ready to play",
                "failed" => "Video processing failed",
                _ => "Unknown status"
            };
        }

        // POST: api/Admin/Videos/complete-upload/{tempVideoId}
        [HttpPost("complete-upload/{tempVideoId}")]
        public async Task<ActionResult<object>> CompleteUpload(int tempVideoId, [FromBody] CompleteUploadRequest request)
        {
            try
            {
                // Get the temporary video record
                var tempVideo = await _context.Videos
                    .Where(v => v.Id == tempVideoId && !v.IsDeleted)
                    .FirstOrDefaultAsync();

                if (tempVideo == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        error = "Temporary video record not found"
                    });
                }

                // ✅ CRITICAL FIX: Extract Vimeo ID from the VideoLink and create Player URL
                string playerEmbedUrl = tempVideo.VideoLink; // Start with original URL

                if (!string.IsNullOrEmpty(tempVideo.VideoLink) &&
                    tempVideo.VideoLink.Contains("vimeo.com/"))
                {
                    // Extract the video ID from URL like "https://vimeo.com/1161935276"
                    var videoId = tempVideo.VideoLink.Split('/').Last();

                    // ✅ Create the player embed URL
                    playerEmbedUrl = $"https://player.vimeo.com/video/{videoId}";

                    // Also store the Vimeo ID if needed
                    tempVideo.VimeoId = videoId; // Add this field to your Video model
                }

                // Update the video with final details
                tempVideo.Title = request.Title;
                tempVideo.DurationInMinutes = request.DurationInMinutes;
                tempVideo.IsPaid = request.IsPaid;
                tempVideo.Description = request.Description;
                tempVideo.PlayerEmbedUrl = playerEmbedUrl; // ✅ Store the player URL
                tempVideo.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    video = new
                    {
                        tempVideo.Id,
                        tempVideo.Title,
                        tempVideo.DurationInMinutes,
                        tempVideo.IsPaid,
                        tempVideo.Description,
                        vimeo_page_url = tempVideo.VideoLink, // Original Vimeo URL
                        player_embed_url = playerEmbedUrl, // ✅ New embeddable URL
                        tempVideo.CourseId,
                        tempVideo.CreatedAt,
                        tempVideo.UpdatedAt
                    },
                    message = "Video uploaded and saved successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }


        // POST: api/Admin/Videos/simple-upload (Alternative simple method)
        [HttpPost("simple-upload")]
        public async Task<ActionResult<object>> SimpleUpload([FromForm] SimpleUploadRequest request)
        {
            try
            {
                // Validate
                if (request.VideoFile == null || request.VideoFile.Length == 0)
                {
                    return BadRequest(new { error = "No video file provided" });
                }

                // Create video on Vimeo
                var createVideoRequest = new
                {
                    upload = new
                    {
                        approach = "post",
                        size = request.VideoFile.Length.ToString()
                    },
                    name = request.Title,
                    description = request.Description,
                    privacy = new
                    {
                        view = "disable",
                        embed = "public"
                    }
                };

                var createVideoContent = new StringContent(
                    JsonSerializer.Serialize(createVideoRequest),
                    Encoding.UTF8,
                    "application/json");

                var createResponse = await _httpClient.PostAsync(
                    "https://api.vimeo.com/me/videos",
                    createVideoContent);

                if (!createResponse.IsSuccessStatusCode)
                {
                    var errorContent = await createResponse.Content.ReadAsStringAsync();
                    return BadRequest(new
                    {
                        error = "Failed to create video",
                        details = errorContent
                    });
                }

                var vimeoResponse = JsonSerializer.Deserialize<JsonElement>(
                    await createResponse.Content.ReadAsStringAsync());

                // Get upload form
                var upload = vimeoResponse.GetProperty("upload");
                var uploadForm = upload.GetProperty("form").GetString();
                var videoUri = vimeoResponse.GetProperty("uri").GetString();
                var videoId = videoUri.Split('/').Last();

                // Parse the form HTML to get upload URL
                var formDoc = new HtmlAgilityPack.HtmlDocument();
                formDoc.LoadHtml(uploadForm);
                var formAction = formDoc.DocumentNode.SelectSingleNode("//form")?.GetAttributeValue("action", "");

                if (string.IsNullOrEmpty(formAction))
                {
                    return BadRequest(new { error = "Failed to parse upload form" });
                }

                // Upload the file
                using var formData = new MultipartFormDataContent();
                using var fileStream = request.VideoFile.OpenReadStream();
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                formData.Add(fileContent, "file_data", request.VideoFile.FileName);

                var uploadResponse = await _httpClient.PostAsync(formAction, formData);

                if (!uploadResponse.IsSuccessStatusCode)
                {
                    return BadRequest(new
                    {
                        error = "Upload failed",
                        status = uploadResponse.StatusCode
                    });
                }

                // Wait for processing
                await Task.Delay(5000);

                // Get final video info
                var videoInfoResponse = await _httpClient.GetAsync($"https://api.vimeo.com/videos/{videoId}");
                var videoInfo = JsonSerializer.Deserialize<JsonElement>(
                    await videoInfoResponse.Content.ReadAsStringAsync());

                string videoLink = videoInfo.GetProperty("link").GetString();
                int duration = videoInfo.GetProperty("duration").GetInt32();

                // Create video record
                var video = new Video
                {
                    Title = request.Title,
                    DurationInMinutes = request.DurationInMinutes > 0 ?
                        request.DurationInMinutes : (int)Math.Ceiling(duration / 60.0),
                    IsPaid = request.IsPaid,
                    Description = request.Description,
                    VideoLink = videoLink,
                    CourseId = request.CourseId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Videos.Add(video);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    video = new
                    {
                        video.Id,
                        video.Title,
                        video.DurationInMinutes,
                        video.IsPaid,
                        video.Description,
                        video.VideoLink,
                        video.CourseId,
                        video.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        public class SimpleUploadRequest
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public int DurationInMinutes { get; set; }
            public int CourseId { get; set; }
            public bool IsPaid { get; set; }
            public IFormFile VideoFile { get; set; }
        }

       
       

        // GET: api/Admin/Videos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetVideos(
            [FromQuery] int courseId = 0,
            [FromQuery] string search = null,
            [FromQuery] bool? isPaid = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var query = _context.Videos
                .Where(v => !v.IsDeleted)
                .Include(v => v.Course)
                .AsQueryable();

            // Filter by course
            if (courseId > 0)
            {
                query = query.Where(v => v.CourseId == courseId);
            }

            // Filter by payment status
            if (isPaid.HasValue)
            {
                query = query.Where(v => v.IsPaid == isPaid.Value);
            }

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(v =>
                    v.Title.Contains(search) ||
                    v.Description.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var videos = await query
                .OrderBy(v => v.CourseId)
                .ThenBy(v => v.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new
                {
                    v.Id,
                    v.Title,
                    v.DurationInMinutes,
                    v.IsPaid,
                    v.Description,
                    v.VideoLink,
                    v.VimeoId,
                    v.PlayerEmbedUrl,
                    v.CourseId,
                    CourseNameArabic = v.Course.NameArabic,
                    CourseNameEnglish = v.Course.NameEnglish,
                    v.CreatedAt,
                    v.UpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                Data = videos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        // GET: api/Admin/Videos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetVideo(int id)
        {
            var video = await _context.Videos
                .Where(v => v.Id == id && !v.IsDeleted)
                .Include(v => v.Course)
                .Select(v => new
                {
                    v.Id,
                    v.Title,
                    v.DurationInMinutes,
                    v.IsPaid,
                    v.Description,
                    v.VideoLink,
                    v.VimeoId,
                    v.PlayerEmbedUrl,
                    v.CourseId,
                    
                    CourseNameArabic = v.Course.NameArabic,
                    CourseNameEnglish = v.Course.NameEnglish,
                    CourseClassification = v.Course.Classification,
                    InstructorName = v.Course.InstructorName,
                    v.CreatedAt,
                    v.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (video == null)
            {
                return NotFound();
            }

            return Ok(video);
        }

        // GET: api/Admin/Videos/course/5
        [HttpGet("course/{courseId}")]
        public async Task<ActionResult<object>> GetCourseVideos(int courseId)
        {
            var course = await _context.Courses
                .Where(c => c.Id == courseId && !c.IsDeleted)
                .FirstOrDefaultAsync();

            if (course == null)
            {
                return NotFound(new { error = "Course not found" });
            }

            var videos = await _context.Videos
                .Where(v => v.CourseId == courseId && !v.IsDeleted)
                .OrderBy(v => v.CreatedAt)
                .Select(v => new
                {
                    v.Id,
                    v.Title,
                    v.DurationInMinutes,
                    v.IsPaid,
                    v.Description,
                    v.VideoLink,
                    v.VimeoId,
                    v.PlayerEmbedUrl,
                    Hours = v.DurationInMinutes / 60.0,
                    v.CreatedAt
                })
                .ToListAsync();

            var totalHours = videos.Sum(v => v.DurationInMinutes) / 60.0;
            var paidVideosCount = videos.Count(v => v.IsPaid);
            var freeVideosCount = videos.Count(v => !v.IsPaid);

            return Ok(new
            {
                Course = new
                {
                    course.Id,
                    course.NameArabic,
                    course.NameEnglish,
                    course.Classification,
                    course.LessonsNumber
                },
                Videos = videos,
                Statistics = new
                {
                    TotalVideos = videos.Count,
                    TotalHours = Math.Round(totalHours, 2),
                    PaidVideos = paidVideosCount,
                    FreeVideos = freeVideosCount
                }
            });
        }

        //// POST: api/Admin/Videos
        //[HttpPost]
        //public async Task<ActionResult<object>> CreateVideo([FromForm] VideoCreateRequest request)
        //{
        //    // Validate course exists
        //    var course = await _context.Courses
        //        .Where(c => c.Id == request.CourseId && !c.IsDeleted)
        //        .FirstOrDefaultAsync();

        //    if (course == null)
        //    {
        //        return BadRequest(new { error = "Course not found" });
        //    }

        //    string videoLink = null;

        //    try
        //    {
        //        // Handle video upload
        //        if (request.VideoFile != null)
        //        {
        //            // Validate video file
        //            var allowedExtensions = new[] { ".mp4", ".webm", ".ogg", ".mov", ".avi", ".mkv" };
        //            var fileExtension = Path.GetExtension(request.VideoFile.FileName).ToLower();

        //            if (!allowedExtensions.Contains(fileExtension))
        //            {
        //                return BadRequest(new { error = "Invalid video format. Allowed: MP4, WebM, OGG, MOV, AVI, MKV" });
        //            }

        //            if (request.VideoFile.Length > 500 * 1024 * 1024) // 500MB limit
        //            {
        //                return BadRequest(new { error = "Video file exceeds 500MB limit" });
        //            }

        //            // Upload to Vimeo
        //            videoLink = await UploadToVimeo(request.VideoFile);
        //        }
        //        else if (!string.IsNullOrEmpty(request.VimeoVideoUrl))
        //        {
        //            // Use provided Vimeo URL
        //            videoLink = request.VimeoVideoUrl;
        //        }
        //        else
        //        {
        //            return BadRequest(new { error = "Either VideoFile or VimeoVideoUrl is required" });
        //        }

        //        // Create video entity
        //        var video = new Video
        //        {
        //            Title = request.Title,
        //            DurationInMinutes = request.DurationInMinutes,
        //            IsPaid = request.IsPaid,
        //            Description = request.Description,
        //            VideoLink = videoLink,
        //            CourseId = request.CourseId,
        //            CreatedAt = DateTime.UtcNow
        //        };

        //        _context.Videos.Add(video);
        //        await _context.SaveChangesAsync();

        //        // Update course hours if needed
        //        if (!course.HoursNumber.HasValue ||
        //            course.HoursNumber < request.DurationInMinutes / 60.0)
        //        {
        //            course.HoursNumber = (int?)Math.Ceiling(request.DurationInMinutes / 60.0);
        //            course.UpdatedAt = DateTime.UtcNow;
        //            await _context.SaveChangesAsync();
        //        }

        //        var response = new
        //        {
        //            video.Id,
        //            video.Title,
        //            video.DurationInMinutes,
        //            video.IsPaid,
        //            video.Description,
        //            video.VideoLink,
        //            video.CourseId,
        //            CourseNameArabic = course.NameArabic,
        //            CourseNameEnglish = course.NameEnglish,
        //            video.CreatedAt,
        //            video.UpdatedAt
        //        };

        //        return CreatedAtAction("GetVideo", new { id = video.Id }, response);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { error = $"An error occurred while creating the video: {ex.Message}" });
        //    }
        //}

        //// PUT: api/Admin/Videos/5
        //[HttpPut("{id}")]
        //public async Task<IActionResult> UpdateVideo(int id, [FromForm] VideoUpdateRequest request)
        //{
        //    var video = await _context.Videos
        //        .Where(v => v.Id == id && !v.IsDeleted)
        //        .Include(v => v.Course)
        //        .FirstOrDefaultAsync();

        //    if (video == null)
        //    {
        //        return NotFound();
        //    }

        //    string newVideoLink = video.VideoLink;

        //    try
        //    {
        //        // Handle video update
        //        if (request.VideoFile != null)
        //        {
        //            // Validate video file
        //            var allowedExtensions = new[] { ".mp4", ".webm", ".ogg", ".mov", ".avi", ".mkv" };
        //            var fileExtension = Path.GetExtension(request.VideoFile.FileName).ToLower();

        //            if (!allowedExtensions.Contains(fileExtension))
        //            {
        //                return BadRequest(new { error = "Invalid video format. Allowed: MP4, WebM, OGG, MOV, AVI, MKV" });
        //            }

        //            if (request.VideoFile.Length > 500 * 1024 * 1024) // 500MB limit
        //            {
        //                return BadRequest(new { error = "Video file exceeds 500MB limit" });
        //            }

        //            // Upload new video to Vimeo
        //            newVideoLink = await UploadToVimeo(request.VideoFile);
        //        }
        //        else if (!string.IsNullOrEmpty(request.VimeoVideoUrl))
        //        {
        //            // Use new Vimeo URL
        //            newVideoLink = request.VimeoVideoUrl;
        //        }

        //        // Update video properties
        //        video.Title = request.Title;
        //        video.DurationInMinutes = request.DurationInMinutes;
        //        video.IsPaid = request.IsPaid;
        //        video.Description = request.Description;
        //        video.VideoLink = newVideoLink;
        //        video.UpdatedAt = DateTime.UtcNow;

        //        await _context.SaveChangesAsync();

        //        return NoContent();
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { error = $"An error occurred while updating the video: {ex.Message}" });
        //    }
        //}

        // PATCH: api/Admin/Videos/5/toggle-payment
        [HttpPatch("{id}/toggle-payment")]
        public async Task<IActionResult> TogglePaymentStatus(int id)
        {
            var video = await _context.Videos
                .Where(v => v.Id == id && !v.IsDeleted)
                .FirstOrDefaultAsync();

            if (video == null)
            {
                return NotFound();
            }

            video.IsPaid = !video.IsPaid;
            video.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Id = video.Id,
                NewStatus = video.IsPaid,
                Message = $"Video payment status updated to {(video.IsPaid ? "Paid" : "Free")}"
            });
        }

        // DELETE: api/Admin/Videos/5 (Soft Delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVideo(int id)
        {
            var video = await _context.Videos
                .Where(v => v.Id == id && !v.IsDeleted)
                .FirstOrDefaultAsync();

            if (video == null)
            {
                return NotFound();
            }

            // Soft delete
            video.IsDeleted = true;
            video.DeletedAt = DateTime.UtcNow;
            video.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Admin/Videos/stats/course/5
        [HttpGet("stats/course/{courseId}")]
        public async Task<ActionResult<object>> GetCourseVideoStats(int courseId)
        {
            var course = await _context.Courses
                .Where(c => c.Id == courseId && !c.IsDeleted)
                .FirstOrDefaultAsync();

            if (course == null)
            {
                return NotFound(new { error = "Course not found" });
            }

            var videos = await _context.Videos
                .Where(v => v.CourseId == courseId && !v.IsDeleted)
                .ToListAsync();

            var totalVideos = videos.Count;
            var totalMinutes = videos.Sum(v => v.DurationInMinutes);
            var totalHours = totalMinutes / 60.0;
            var paidVideos = videos.Count(v => v.IsPaid);
            var freeVideos = videos.Count(v => !v.IsPaid);

            // Calculate average duration
            var averageDuration = totalVideos > 0 ? totalMinutes / totalVideos : 0;

            return Ok(new
            {
                CourseId = courseId,
                CourseNameArabic = course.NameArabic,
                CourseNameEnglish = course.NameEnglish,
                Statistics = new
                {
                    TotalVideos = totalVideos,
                    TotalHours = Math.Round(totalHours, 2),
                    TotalMinutes = totalMinutes,
                    AverageDurationMinutes = Math.Round((double)averageDuration, 1),
                    PaidVideos = paidVideos,
                    FreeVideos = freeVideos,
                    PaidPercentage = totalVideos > 0 ? Math.Round((paidVideos * 100.0) / totalVideos, 1) : 0,
                    FreePercentage = totalVideos > 0 ? Math.Round((freeVideos * 100.0) / totalVideos, 1) : 0
                },
                DurationBreakdown = new
                {
                    ShortVideos = videos.Count(v => v.DurationInMinutes <= 10),
                    MediumVideos = videos.Count(v => v.DurationInMinutes > 10 && v.DurationInMinutes <= 30),
                    LongVideos = videos.Count(v => v.DurationInMinutes > 30)
                }
            });
        }

        public class VideoCreateRequest
        {
            public string Title { get; set; }
            public int DurationInMinutes { get; set; }
            public bool IsPaid { get; set; }
            public string Description { get; set; }
            public int CourseId { get; set; }
            public IFormFile? VideoFile { get; set; } // Optional for direct upload
            public string? VimeoVideoUrl { get; set; } // Optional for Vimeo URL
        }

        public class VideoUpdateRequest
        {
            public string Title { get; set; }
            public int DurationInMinutes { get; set; }
            public bool IsPaid { get; set; }
            public string Description { get; set; }
            public IFormFile? VideoFile { get; set; }
            public string? VimeoVideoUrl { get; set; }
        }

        public class VimeoUploadResponse
        {
            public string uri { get; set; }
            public string link { get; set; }
            public Upload upload { get; set; }
        }
        public class Upload
        {
            public string upload_link { get; set; }
            public string form { get; set; }
        }
        // DTOs
        public class UploadTicketRequest
        {
            public long FileSize { get; set; }
            public string FileName { get; set; }
            public string Description { get; set; }
            public int CourseId { get; set; }
            public string Title { get; set; }
            public int DurationInMinutes { get; set; }
            public bool IsPaid { get; set; }
        }

        public class CompleteUploadRequest
        {
            public string Title { get; set; }
            public int DurationInMinutes { get; set; }
            public bool IsPaid { get; set; }
            public string Description { get; set; }
            public int CourseId { get; set; }
        }

    }
}