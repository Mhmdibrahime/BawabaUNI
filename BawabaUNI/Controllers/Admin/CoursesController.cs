using BawabaUNI.Models.Data;
using BawabaUNI.Models.DTOs.Admin.CoursesDTOs;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/Admin/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]

    public class CoursesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public CoursesController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        

        // Helper methods for file handling
        private async Task<string> SaveFile(IFormFile file, string subFolder)
        {
            if (file == null || file.Length == 0)
                return null;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
                throw new ArgumentException($"Invalid file type for {subFolder}. Only JPG, JPEG, PNG, GIF, and WebP are allowed.");

            if (file.Length > 5 * 1024 * 1024)
                throw new ArgumentException($"File size exceeds 5MB limit for {subFolder}.");

            var fileName = Guid.NewGuid().ToString() + fileExtension;
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", subFolder);

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{subFolder}/{fileName}";
        }

        private void DeleteFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch (Exception)
            {
                // Log error if needed
            }
        }

        // Helper method to update lessons learned
        private async Task UpdateLessonsLearned(int courseId, List<LessonLearnedDto> lessonDtos)
        {
            // Get existing lessons for this course
            var existingLessons = await _context.LessonsLearned
                .Where(ll => ll.CourseId == courseId && !ll.IsDeleted)
                .ToListAsync();

            // Create lookup dictionaries
            var existingLessonsDict = existingLessons.ToDictionary(ll => ll.Id);
            var newLessonsDict = lessonDtos
                .Where(l => l.Id.HasValue)
                .ToDictionary(l => l.Id.Value);

            // Delete lessons that are not in the new list
            foreach (var existingLesson in existingLessons)
            {
                if (!newLessonsDict.ContainsKey(existingLesson.Id))
                {
                    // Soft delete
                    existingLesson.IsDeleted = true;
                    existingLesson.DeletedAt = DateTime.UtcNow;
                    existingLesson.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Update or add lessons
            foreach (var lessonDto in lessonDtos)
            {
                if (lessonDto.Id.HasValue && existingLessonsDict.ContainsKey(lessonDto.Id.Value))
                {
                    // Update existing lesson
                    var existingLesson = existingLessonsDict[lessonDto.Id.Value];
                    existingLesson.PointName = lessonDto.PointName;
                    existingLesson.UpdatedAt = DateTime.UtcNow;
                    existingLesson.IsDeleted = false; // In case it was previously deleted
                    existingLesson.DeletedAt = null;
                }
                else
                {
                    // Add new lesson
                    var newLesson = new LessonLearned
                    {
                        PointName = lessonDto.PointName,
                        CourseId = courseId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.LessonsLearned.Add(newLesson);
                }
            }
        }

        // GET: api/Courses
        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<object>>> GetCourses(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string search = null,
            [FromQuery] string classification = null,
            [FromQuery] string sortBy = "newest")
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            // Available sorting terms for frontend reference
            var availableSortTerms = new[]
            {
        "newest",        // الأحدث أولاً
        "oldest",        // الأقدم أولاً
        "priceLowToHigh", // السعر (منخفض إلى مرتفع)
        "priceHighToLow", // السعر (مرتفع إلى منخفض)
        "mostStudents",   // الأكثر طلاباً (replaces lastEnrollment)
        "highestRated"    // الأعلى تقييماً
    };

            // Base query with includes
            var query = _context.Courses
                .Where(c => !c.IsDeleted)
                .Include(c => c.LessonsLearned)
                .Include(c => c.StudentCourses)
                .Include(c => c.CourseFeedbacks)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c =>
                    c.NameArabic.Contains(search) ||
                    c.NameEnglish.Contains(search) ||
                    c.Description.Contains(search) ||
                    c.InstructorName.Contains(search) ||
                    c.Classification.Contains(search));
            }

            // Classification filter
            if (!string.IsNullOrEmpty(classification))
            {
                query = query.Where(c => c.Classification == classification);
            }

            // First, get the base query results to calculate averages and student counts
            var baseQuery = query.Select(c => new
            {
                Course = c,
                AverageRating = c.CourseFeedbacks
                    .Where(cf => !cf.IsDeleted)
                    .Average(cf => (double?)cf.Rating) ?? 0,
                StudentsCount = c.StudentCourses.Count,
                FinalPrice = c.Discount.HasValue ?
                    c.Price - (c.Price * c.Discount.Value / 100) : c.Price
            });

            // Apply sorting
            var sortedQuery = sortBy.ToLower() switch
            {
                "newest" => baseQuery.OrderByDescending(x => x.Course.CreatedAt),
                "oldest" => baseQuery.OrderBy(x => x.Course.CreatedAt),
                "pricelowtohigh" => baseQuery.OrderBy(x => x.FinalPrice),
                "pricehightolow" => baseQuery.OrderByDescending(x => x.FinalPrice),
                "moststudents" => baseQuery.OrderByDescending(x => x.StudentsCount),
                "highestrated" => baseQuery.OrderByDescending(x => x.AverageRating),
                _ => baseQuery.OrderByDescending(x => x.Course.CreatedAt) // Default to newest
            };

            // Get total count
            var totalCount = await sortedQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Apply pagination and select final result
            var courses = await sortedQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Course.Id,
                    x.Course.NameArabic,
                    x.Course.NameEnglish,
                    x.Course.Description,
                    x.Course.Price,
                    x.Course.Discount,
                    FinalPrice = x.FinalPrice,
                    x.Course.LessonsNumber,
                    x.Course.HoursNumber,
                    x.Course.PosterImage,
                    x.Course.Classification,
                    x.Course.InstructorName,
                    x.Course.InstructorImage,
                    x.Course.InstructorDescription,
               
                    StudentsCount = x.StudentsCount,
                    AverageRating = x.AverageRating,
                    x.Course.CreatedAt,
                    x.Course.UpdatedAt
                })
                .ToListAsync();

            var response = new
            {
                Data = courses.Cast<object>().ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                AvailableSortTerms = availableSortTerms,
                CurrentSort = sortBy
            };

            return Ok(response);
        }

        // GET: api/Courses/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetCourse(int id)
        {
            var course = await _context.Courses
                .Where(c => c.Id == id && !c.IsDeleted)
                .Include(c => c.LessonsLearned)
                .Include(c => c.Videos)
                .Include(c => c.StudentCourses)
                .Select(c => new
                {
                    c.Id,
                    c.NameArabic,
                    c.NameEnglish,
                    c.Description,
                    c.Price,
                    c.Discount,
                    FinalPrice = c.Discount.HasValue ?
                        c.Price - (c.Price * c.Discount.Value / 100) : c.Price,
                    c.LessonsNumber,
                    c.HoursNumber,
                    c.PosterImage,
                    c.Classification,
                    c.InstructorName,
                    c.InstructorImage,
                    c.InstructorDescription,
                   
                    Videos = c.Videos.Select(v => new
                    {
                        v.Id,
                        v.Title,
                        v.DurationInMinutes,
                        v.VideoLink
                    }),
                    StudentsCount = c.StudentCourses.Count,
                    Students = c.StudentCourses.Select(sc => new
                    {
                        sc.StudentId,
                        EnrollmentDate = sc.CreatedAt,
                    }).Take(10),
                    c.CreatedAt,
                    c.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (course == null)
            {
                return NotFound();
            }

            return Ok(course);
        }

        // POST: api/Courses
        [HttpPost]
        public async Task<ActionResult<object>> CreateCourse([FromForm] CourseCreateRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            string posterImagePath = null;
            string instructorImagePath = null;

            try
            {
                // Validate required images
                if (request.PosterImage == null)
                    return BadRequest(new { error = "Poster image is required." });
                if (request.InstructorImage == null)
                    return BadRequest(new { error = "Instructor image is required." });

                // Save poster image
                posterImagePath = await SaveFile(request.PosterImage, "courses/posters");

                // Save instructor image
                instructorImagePath = await SaveFile(request.InstructorImage, "courses/instructors");

                // Create course
                var course = new Course
                {
                    NameArabic = request.NameArabic,
                    NameEnglish = request.NameEnglish,
                    Description = request.Description,
                    Price = request.Price,
                    Discount = request.Discount,
                    LessonsNumber = request.LessonsNumber,
                    HoursNumber = request.HoursNumber,
                    PosterImage = posterImagePath,
                    Classification = request.Classification,
                    InstructorName = request.InstructorName,
                    InstructorImage = instructorImagePath,
                    InstructorDescription = request.InstructorDescription,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Courses.Add(course);
                await _context.SaveChangesAsync();

                //// Add lessons learned
                //if (request.LessonsLearned != null && request.LessonsLearned.Any())
                //{
                //    foreach (var lessonDto in request.LessonsLearned)
                //    {
                //        var lesson = new LessonLearned
                //        {
                //            PointName = lessonDto.PointName,
                //            CourseId = course.Id,
                //            CreatedAt = DateTime.UtcNow
                //        };
                //        _context.LessonsLearned.Add(lesson);
                //    }
                //    await _context.SaveChangesAsync();
                //}

                await transaction.CommitAsync();

                // Get the complete course with lessons
                var createdCourse = await _context.Courses
                    .Where(c => c.Id == course.Id)
                    .Include(c => c.LessonsLearned)
                    .Select(c => new
                    {
                        c.Id,
                        c.NameArabic,
                        c.NameEnglish,
                        c.Description,
                        c.Price,
                        c.Discount,
                        FinalPrice = c.Discount.HasValue ?
                            c.Price - (c.Price * c.Discount.Value / 100) : c.Price,
                        c.LessonsNumber,
                        c.HoursNumber,
                        c.PosterImage,
                        c.Classification,
                        c.InstructorName,
                        c.InstructorImage,
                        c.InstructorDescription,
                        LessonsLearned = c.LessonsLearned
                            .Where(ll => !ll.IsDeleted)
                            .Select(ll => new { ll.Id, ll.PointName }),
                        c.CreatedAt,
                        c.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                return CreatedAtAction("GetCourse", new { id = course.Id }, createdCourse);
            }
            catch (ArgumentException ex)
            {
                await transaction.RollbackAsync();
                if (!string.IsNullOrEmpty(posterImagePath)) DeleteFile(posterImagePath);
                if (!string.IsNullOrEmpty(instructorImagePath)) DeleteFile(instructorImagePath);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                if (!string.IsNullOrEmpty(posterImagePath)) DeleteFile(posterImagePath);
                if (!string.IsNullOrEmpty(instructorImagePath)) DeleteFile(instructorImagePath);
                return StatusCode(500, new { error = "An error occurred while creating the course." });
            }
        }

        // PUT: api/Courses/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCourse(int id, [FromForm] CourseUpdateRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            var course = await _context.Courses
                .Where(c => c.Id == id && !c.IsDeleted)
                .Include(c => c.LessonsLearned)
                .FirstOrDefaultAsync();

            if (course == null)
            {
                return NotFound();
            }

            string oldPosterImagePath = course.PosterImage;
            string oldInstructorImagePath = course.InstructorImage;
            string newPosterImagePath = null;
            string newInstructorImagePath = null;

            try
            {
                // Handle poster image update
                if (request.PosterImage != null)
                {
                    newPosterImagePath = await SaveFile(request.PosterImage, "courses/posters");
                    course.PosterImage = newPosterImagePath;
                }

                // Handle instructor image update
                if (request.InstructorImage != null)
                {
                    newInstructorImagePath = await SaveFile(request.InstructorImage, "courses/instructors");
                    course.InstructorImage = newInstructorImagePath;
                }

                // Update course properties
                course.NameArabic = request.NameArabic;
                course.NameEnglish = request.NameEnglish;
                course.Description = request.Description;
                course.Price = request.Price;
                course.Discount = request.Discount;
                course.LessonsNumber = request.LessonsNumber;
                course.HoursNumber = request.HoursNumber;
                course.Classification = request.Classification;
                course.InstructorName = request.InstructorName;
                course.InstructorDescription = request.InstructorDescription;
                course.UpdatedAt = DateTime.UtcNow;

                // Update lessons learned
                //await UpdateLessonsLearned(course.Id, request.LessonsLearned ?? new List<LessonLearnedDto>());

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Delete old files if new ones were uploaded successfully
                if (!string.IsNullOrEmpty(newPosterImagePath) && !string.IsNullOrEmpty(oldPosterImagePath))
                {
                    DeleteFile(oldPosterImagePath);
                }
                if (!string.IsNullOrEmpty(newInstructorImagePath) && !string.IsNullOrEmpty(oldInstructorImagePath))
                {
                    DeleteFile(oldInstructorImagePath);
                }

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                await transaction.RollbackAsync();
                if (!string.IsNullOrEmpty(newPosterImagePath)) DeleteFile(newPosterImagePath);
                if (!string.IsNullOrEmpty(newInstructorImagePath)) DeleteFile(newInstructorImagePath);
                return BadRequest(new { error = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                if (!CourseExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                if (!string.IsNullOrEmpty(newPosterImagePath)) DeleteFile(newPosterImagePath);
                if (!string.IsNullOrEmpty(newInstructorImagePath)) DeleteFile(newInstructorImagePath);
                return StatusCode(500, new { error = "An error occurred while updating the course." });
            }
        }

        // DELETE: api/Courses/5 (Soft Delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _context.Courses
                .Where(c => c.Id == id && !c.IsDeleted)
                .FirstOrDefaultAsync();

            if (course == null)
            {
                return NotFound();
            }

            // Soft delete course and its lessons
            course.IsDeleted = true;
            course.DeletedAt = DateTime.UtcNow;
            course.UpdatedAt = DateTime.UtcNow;

            // Also soft delete related lessons
            var lessons = await _context.LessonsLearned
                .Where(ll => ll.CourseId == id && !ll.IsDeleted)
                .ToListAsync();

            foreach (var lesson in lessons)
            {
                lesson.IsDeleted = true;
                lesson.DeletedAt = DateTime.UtcNow;
                lesson.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Courses/stats
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetCourseStats()
        {
            var totalCourses = await _context.Courses.CountAsync(c => !c.IsDeleted);

            var totalStudents = await _context.StudentCourses
                .Select(sc => sc.StudentId)
                .Distinct()
                .CountAsync();

            var averagePrice = await _context.Courses
                .Where(c => !c.IsDeleted)
                .AverageAsync(c => (double?)c.Price) ?? 0;

            var coursesByClassification = await _context.Courses
                .Where(c => !c.IsDeleted)
                .GroupBy(c => c.Classification)
                .Select(g => new
                {
                    Classification = g.Key,
                    Count = g.Count(),
                    AveragePrice = g.Average(c => c.Price)
                })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            var popularCourses = await _context.Courses
                .Where(c => !c.IsDeleted)
                .Include(c => c.StudentCourses)
                .Select(c => new
                {
                    c.Id,
                    c.NameArabic,
                    c.NameEnglish,
                    c.Classification,
                    StudentsCount = c.StudentCourses.Count,
                    Revenue = c.StudentCourses.Count * c.Price * (c.Discount.HasValue ?
                        (100 - c.Discount.Value) / 100 : 1)
                })
                .OrderByDescending(c => c.StudentsCount)
                .Take(5)
                .ToListAsync();

            var totalRevenue = await _context.StudentCourses
                .Include(sc => sc.Course)
                .Where(sc => !sc.Course.IsDeleted)
                .SumAsync(sc => sc.Course.Price * (sc.Course.Discount.HasValue ?
                    (100 - sc.Course.Discount.Value) / 100 : 1));

            

            return Ok(new
            {
                TotalCourses = totalCourses,
                TotalStudents = totalStudents,
                AveragePrice = Math.Round(averagePrice, 2),
                CoursesByClassification = coursesByClassification,
                PopularCourses = popularCourses,
                TotalRevenue = Math.Round(totalRevenue, 2),
            });
        }

        // GET: api/Courses/classifications
        [HttpGet("classifications")]
        public async Task<ActionResult<IEnumerable<string>>> GetClassifications()
        {
            var classifications = await _context.Courses
                .Where(c => !c.IsDeleted)
                .Select(c => c.Classification)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(classifications);
        }


        // Generate activation code for a course
        [HttpPost("generate-code")]
        public async Task<IActionResult> GenerateActivationCode([FromBody] GenerateActivationCodeDto request)
        {
            try
            {
                // Check if course exists
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.Id == request.CourseId && !c.IsDeleted);

                if (course == null)
                {
                    return NotFound(new { Success = false, Message = "الدورة غير موجودة" });
                }

                // Validate expiry date
                if (request.ExpiryDate <= DateTime.UtcNow)
                {
                    return BadRequest(new { Success = false, Message = "تاريخ الانتهاء يجب أن يكون في المستقبل" });
                }

                // Generate unique code
                string code;
                bool codeExists;
                do
                {
                    code = GenerateUniqueCode();
                    codeExists = await _context.CourseActivationCodes
                        .AnyAsync(c => c.Code == code && !c.IsDeleted);
                } while (codeExists);

                var activationCode = new CourseActivationCode
                {
                    Code = code,
                    CourseId = request.CourseId,
                    ExpiryDate = request.ExpiryDate.ToUniversalTime(),
                    Notes = request.Notes,
                    CreatedAt = DateTime.UtcNow,
                    IsUsed = false
                };

                _context.CourseActivationCodes.Add(activationCode);
                await _context.SaveChangesAsync();

                var response = new
                {
                    Success = true,
                    Message = "تم إنشاء رمز التفعيل بنجاح",
                    Data = new
                    {
                        activationCode.Id,
                        activationCode.Code,
                        CourseName = course.NameArabic,
                        activationCode.ExpiryDate,
                        activationCode.Notes,
                        activationCode.CreatedAt
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء إنشاء رمز التفعيل",
                    Error = ex.Message
                });
            }
        }

        // Generate multiple activation codes
        [HttpPost("generate-codes-bulk")]
        public async Task<IActionResult> GenerateMultipleActivationCodes([FromBody] BulkGenerateCodeDto request)
        {
            try
            {
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.Id == request.CourseId && !c.IsDeleted);

                if (course == null)
                {
                    return NotFound(new { Success = false, Message = "الدورة غير موجودة" });
                }

                if (request.NumberOfCodes <= 0 || request.NumberOfCodes > 100)
                {
                    return BadRequest(new { Success = false, Message = "عدد الرموز يجب أن يكون بين 1 و 100" });
                }

                if (request.ExpiryDate <= DateTime.UtcNow)
                {
                    return BadRequest(new { Success = false, Message = "تاريخ الانتهاء يجب أن يكون في المستقبل" });
                }

                var codes = new List<CourseActivationCode>();
                var generatedCodes = new List<string>();

                for (int i = 0; i < request.NumberOfCodes; i++)
                {
                    string code;
                    bool codeExists;
                    do
                    {
                        code = GenerateUniqueCode();
                        codeExists = await _context.CourseActivationCodes
                            .AnyAsync(c => c.Code == code && !c.IsDeleted);
                    } while (codeExists);

                    var activationCode = new CourseActivationCode
                    {
                        Code = code,
                        CourseId = request.CourseId,
                        ExpiryDate = request.ExpiryDate.ToUniversalTime(),
                        Notes = request.Notes,
                        CreatedAt = DateTime.UtcNow,
                        IsUsed = false
                    };

                    codes.Add(activationCode);
                    generatedCodes.Add(code);
                }

                await _context.CourseActivationCodes.AddRangeAsync(codes);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Success = true,
                    Message = $"تم إنشاء {request.NumberOfCodes} رمز تفعيل بنجاح",
                    Data = new
                    {
                        CourseName = course.NameArabic,
                        Codes = generatedCodes,
                        Count = generatedCodes.Count,
                        ExpiryDate = request.ExpiryDate
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء إنشاء الرموز",
                    Error = ex.Message
                });
            }
        }

        // Get all activation codes for a course
        [HttpGet("codes/{courseId}")]
        public async Task<IActionResult> GetActivationCodes(int courseId,
            [FromQuery] bool? isUsed = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.CourseActivationCodes
                    .Include(c => c.Course)
                    .Include(c => c.UsedByStudent)
                    .ThenInclude(s => s.ApplicationUser)
                    .Where(c => c.CourseId == courseId && !c.IsDeleted);

                if (isUsed.HasValue)
                {
                    query = query.Where(c => c.IsUsed == isUsed.Value);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var codes = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new ActivationCodeResponseDto
                    {
                        Id = c.Id,
                        Code = c.Code,
                        CourseId = c.CourseId,
                        CourseName = c.Course.NameArabic,
                        ExpiryDate = c.ExpiryDate,
                        IsUsed = c.IsUsed,
                        UsedAt = c.UsedAt,
                        UsedByStudentName = c.UsedByStudent != null ?
                            c.UsedByStudent.ApplicationUser.FullName : null,
                        Notes = c.Notes,
                        CreatedAt = c.CreatedAt,
                        IsExpired = c.ExpiryDate < DateTime.UtcNow && !c.IsUsed
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Success = true,
                    Message = "تم جلب رموز التفعيل بنجاح",
                    Data = new
                    {
                        Codes = codes,
                        TotalCount = totalCount,
                        TotalPages = totalPages,
                        CurrentPage = page,
                        PageSize = pageSize
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء جلب رموز التفعيل",
                    Error = ex.Message
                });
            }
        }

        // Get code statistics
        [HttpGet("codes/stats")]
        public async Task<IActionResult> GetCodeStatistics()
        {
            try
            {
                var totalCodes = await _context.CourseActivationCodes
                    .CountAsync(c => !c.IsDeleted);

                var usedCodes = await _context.CourseActivationCodes
                    .CountAsync(c => !c.IsDeleted && c.IsUsed);

                var expiredCodes = await _context.CourseActivationCodes
                    .CountAsync(c => !c.IsDeleted && !c.IsUsed && c.ExpiryDate < DateTime.UtcNow);

                var validCodes = totalCodes - usedCodes - expiredCodes;

                var codesByCourse = await _context.CourseActivationCodes
                    .Where(c => !c.IsDeleted)
                    .GroupBy(c => c.CourseId)
                    .Select(g => new
                    {
                        CourseId = g.Key,
                        CourseName = g.First().Course.NameArabic,
                        TotalCodes = g.Count(),
                        UsedCodes = g.Count(c => c.IsUsed),
                        ExpiredCodes = g.Count(c => !c.IsUsed && c.ExpiryDate < DateTime.UtcNow)
                    })
                    .OrderByDescending(g => g.TotalCodes)
                    .Take(10)
                    .ToListAsync();

                return Ok(new
                {
                    Success = true,
                    Data = new
                    {
                        TotalCodes = totalCodes,
                        UsedCodes = usedCodes,
                        ExpiredCodes = expiredCodes,
                        ValidCodes = validCodes,
                        UsagePercentage = totalCodes > 0 ? (usedCodes * 100 / totalCodes) : 0,
                        CodesByCourse = codesByCourse
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء جلب إحصائيات الرموز",
                    Error = ex.Message
                });
            }
        }

        // Delete (soft delete) an activation code
        [HttpDelete("codes/{id}")]
        public async Task<IActionResult> DeleteActivationCode(int id)
        {
            try
            {
                var code = await _context.CourseActivationCodes
                    .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

                if (code == null)
                {
                    return NotFound(new { Success = false, Message = "رمز التفعيل غير موجود" });
                }

                if (code.IsUsed)
                {
                    return BadRequest(new { Success = false, Message = "لا يمكن حذف رمز تم استخدامه بالفعل" });
                }

                code.IsDeleted = true;
                code.DeletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Success = true,
                    Message = "تم حذف رمز التفعيل بنجاح"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء حذف رمز التفعيل",
                    Error = ex.Message
                });
            }
        }

        // Helper method to generate unique code
        private string GenerateUniqueCode()
        {
            // Format: XXX-XXX-XXX (e.g., ABC-123-DEF)
            var chars = "ABCDEFGHJKLMNPQRSTUVWXYZ0123456789";
            var random = new Random();

            var part1 = new string(Enumerable.Repeat(chars, 3)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            var part2 = new string(Enumerable.Repeat(chars, 3)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            var part3 = new string(Enumerable.Repeat(chars, 3)
                .Select(s => s[random.Next(s.Length)]).ToArray());

            return $"{part1}-{part2}-{part3}";
        }



        private bool CourseExists(int id)
        {
            return _context.Courses.Any(c => c.Id == id && !c.IsDeleted);
        }
    }
    public class GenerateActivationCodeDto
    {
        [Required]
        public int CourseId { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class ActivationCodeResponseDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedAt { get; set; }
        public string? UsedByStudentName { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsExpired { get; set; }
    }

    public class ActivateCourseDto
    {
        [Required]
        public string Code { get; set; }
    }
    public class BulkGenerateCodeDto
    {
        [Required]
        public int CourseId { get; set; }

        [Required]
        [Range(1, 100)]
        public int NumberOfCodes { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}