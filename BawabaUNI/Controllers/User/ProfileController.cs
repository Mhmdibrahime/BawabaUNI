using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BawabaUNI.Models.Data;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;

namespace BawabaUNI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProfileController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Profile
        // Get current student's  
        [HttpGet]
        public async Task<ActionResult<StudentProfileDto>> GetProfile()
        {
            try
            {
                // Get current user ID from claims
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                // Find student by ApplicationUserId
                var student = await _context.Students
                    .Include(s => s.ApplicationUser)
                    .Include(s => s.StudentCourses)
                        .ThenInclude(sc => sc.Course)
                    .Include(s => s.CourseFeedbacks)
                    .FirstOrDefaultAsync(s => s.ApplicationUserId == userId);

                if (student == null)
                {
                    return NotFound(new { message = "Student profile not found" });
                }

                // Get enrollment statistics
                var enrollments = await _context.StudentCourses
                    .Where(sc => sc.StudentId == student.Id)
                    .Include(sc => sc.Course)
                    .OrderByDescending(sc => sc.EnrollmentDate)
                    .ToListAsync();

                var profile = new StudentProfileDto
                {
                    Id = student.Id,
                    Name = student.Name,
                    Email = student.ApplicationUser?.Email,
                    PhoneNumber = student.ApplicationUser?.PhoneNumber,
                    FullName = student.ApplicationUser?.FullName,
                    UserId = student.ApplicationUserId,

                    // Statistics
                    TotalEnrolledCourses = enrollments.Count,
                    
                    
                    // Recent Activity
                    RecentEnrollments = enrollments
                        .Take(5)
                        .Select(sc => new ProfileEnrollmentDto
                        {
                            CourseId = sc.CourseId,
                            CourseName = sc.Course?.NameArabic,
                            CourseNameEnglish = sc.Course?.NameEnglish,
                            EnrollmentDate = sc.EnrollmentDate,
                            Status = sc.EnrollmentStatus,
                            Progress = sc.ProgressPercentage ?? 0,
                            Price = (sc.Course?.Price ?? 0) - ((sc.Course?.Price ?? 0) * (sc.Course?.Discount ?? 0) / 100)
                        }).ToList(),


                    JoinDate = student.CreatedAt,
                };

                return Ok(profile);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        // PUT: api/Profile
        // Update student profile
        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                // Get current user ID from claims
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                // Find student and user
                var student = await _context.Students
                    .Include(s => s.ApplicationUser)
                    .FirstOrDefaultAsync(s => s.ApplicationUserId == userId);

                if (student == null)
                {
                    return NotFound(new { message = "Student profile not found" });
                }

                var user = student.ApplicationUser;
                if (user == null)
                {
                    return NotFound(new { message = "User account not found" });
                }

                // Validate email uniqueness if changed
                if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
                {
                    var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != userId);
                    if (emailExists)
                    {
                        return BadRequest(new { message = "Email already in use" });
                    }
                }

                // Validate phone uniqueness if changed
                if (!string.IsNullOrEmpty(request.PhoneNumber) && request.PhoneNumber != user.PhoneNumber)
                {
                    var phoneExists = await _context.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber && u.Id != userId);
                    if (phoneExists)
                    {
                        return BadRequest(new { message = "Phone number already in use" });
                    }
                }

                // Update student
                if (!string.IsNullOrEmpty(request.Name))
                {
                    student.Name = request.Name;
                }
                student.UpdatedAt = DateTime.UtcNow;

                // Update user
                if (!string.IsNullOrEmpty(request.FullName))
                {
                    user.FullName = request.FullName;
                }
                if (!string.IsNullOrEmpty(request.Email))
                {
                    user.Email = request.Email;
                    user.UserName = request.Email;
                    user.NormalizedEmail = request.Email.ToUpper();
                    user.NormalizedUserName = request.Email.ToUpper();
                }
                if (!string.IsNullOrEmpty(request.PhoneNumber))
                {
                    user.PhoneNumber = request.PhoneNumber;
                }

                _context.Entry(student).State = EntityState.Modified;
                _context.Entry(user).State = EntityState.Modified;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Profile updated successfully",
                    updatedAt = student.UpdatedAt
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, new { message = "Concurrency error occurred" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        // GET: api/Profile/enrollments
        // Get all enrollments for current student
        [HttpGet("enrollments")]
        public async Task<ActionResult<PPagedResult<ProfileEnrollmentDto>>> GetEnrollments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] string? search = null)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.ApplicationUserId == userId);

                if (student == null)
                {
                    return NotFound(new { message = "Student profile not found" });
                }

                var query = _context.StudentCourses
                    .Where(sc => sc.StudentId == student.Id)
                    .Include(sc => sc.Course)
                    .AsQueryable();

                // Apply status filter
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(sc => sc.EnrollmentStatus == status);
                }

                // Apply search filter
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(sc =>
                        sc.Course.NameArabic.Contains(search) ||
                        sc.Course.NameEnglish.Contains(search));
                }

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply pagination
                var enrollments = await query
                    .OrderByDescending(sc => sc.EnrollmentDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(sc => new ProfileEnrollmentDto
                    {
                        Id = sc.Id,
                        CourseId = sc.CourseId,
                        CourseName = sc.Course.NameArabic,
                        CourseNameEnglish = sc.Course.NameEnglish,
                        EnrollmentDate = sc.EnrollmentDate,
                        Status = sc.EnrollmentStatus,
                        Progress = sc.ProgressPercentage ?? 0,
                        CompletionDate = sc.CompletionDate,
                        Price = sc.Course.Price - (sc.Course.Price * (sc.Course.Discount ?? 0) / 100),
                        OriginalPrice = sc.Course.Price,
                        Discount = sc.Course.Discount ?? 0,
                        InstructorName = sc.Course.InstructorName,
                        LessonsNumber = sc.Course.LessonsNumber,
                        HoursNumber = sc.Course.HoursNumber ?? 0
                    })
                    .ToListAsync();

                var result = new PPagedResult<ProfileEnrollmentDto>
                {
                    Items = enrollments,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        

        

        // GET: api/Profile/activity
        // Get recent activity timeline
        [HttpGet("activity")]
        public async Task<ActionResult<List<ProfileActivityDto>>> GetActivity()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.ApplicationUserId == userId);

                if (student == null)
                {
                    return NotFound(new { message = "Student profile not found" });
                }

                // Get enrollments (last 10)
                var enrollments = await _context.StudentCourses
                    .Where(sc => sc.StudentId == student.Id)
                    .Include(sc => sc.Course)
                    .OrderByDescending(sc => sc.EnrollmentDate)
                    .Take(10)
                    .Select(sc => new ProfileActivityDto
                    {
                        Type = "Enrollment",
                        Title = $"تم التسجيل في دورة {sc.Course.NameArabic}",
                        Description = $"قمت بالتسجيل في الدورة {sc.Course.NameArabic}",
                        Date = sc.EnrollmentDate,
                        Icon = "book"
                    })
                    .ToListAsync();

                // Get feedback (last 5)
                var feedbacks = await _context.CourseFeedbacks
                    .Where(cf => cf.StudentId == student.Id)
                    .Include(cf => cf.Course)
                    .OrderByDescending(cf => cf.CreatedAt)
                    .Take(5)
                    .Select(cf => new ProfileActivityDto
                    {
                        Type = "Feedback",
                        Title = $"تقييم دورة {cf.Course.NameArabic}",
                        Description = $"قمت بتقييم الدورة {cf.Course.NameArabic} بدرجة {cf.Rating}/5",
                        Date = cf.CreatedAt,
                        Icon = "star"
                    })
                    .ToListAsync();

                // Combine and order by date
                var activities = enrollments.Concat(feedbacks)
                    .OrderByDescending(a => a.Date)
                    .Take(15)
                    .ToList();

                return Ok(activities);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }
    }

    // DTO Classes for Profile

    public class StudentProfileDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string UserId { get; set; }

        // Statistics
        public int TotalEnrolledCourses { get; set; }
      

        // Recent Items
        public List<ProfileEnrollmentDto> RecentEnrollments { get; set; } = new List<ProfileEnrollmentDto>();

        // Dates
        public DateTime JoinDate { get; set; }
    }

    public class UpdateProfileRequest
    {
        public string? Name { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class ProfileEnrollmentDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public string CourseNameEnglish { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public string Status { get; set; }
        public decimal Progress { get; set; }
        public DateTime? CompletionDate { get; set; }
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal Discount { get; set; }
        public string InstructorName { get; set; }
        public int LessonsNumber { get; set; }
        public int HoursNumber { get; set; }
    }

    public class ProfileFeedbackDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class ProfileStatsDto
    {
        public int TotalEnrolledCourses { get; set; }
        public int CompletedCourses { get; set; }
        public int ActiveCourses { get; set; }
        public decimal TotalSpent { get; set; }
        public double AverageProgress { get; set; }
        public int ThisMonthEnrollments { get; set; }
        public int TotalLearningHours { get; set; }
        public double CompletionRate { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class ProfileActivityDto
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Date { get; set; }
        public string Icon { get; set; }
    }

    public class PPagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}