using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BawabaUNI.Models.Data;
using BawabaUNI.Models.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using BawabaUNI.Models.DTOs.Admin.CoursesDTOs;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/Admin/[controller]")]
    [ApiController]
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

     
      

       

        private bool CourseExists(int id)
        {
            return _context.Courses.Any(c => c.Id == id && !c.IsDeleted);
        }
    }
}