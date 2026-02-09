using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BawabaUNI.Models.Data;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/Admin/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class StudentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StudentsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Admin/Students
        // Get all students for the list view (as shown in first screenshot)
        [HttpGet]
        public async Task<ActionResult<SPagedResult<StudentListDto>>> GetStudents(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "CreatedAt",
            [FromQuery] string? sortOrder = "desc")
        {
            try
            {
                var query = _context.Students
                    .Include(s => s.ApplicationUser)
                    .Include(s => s.StudentCourses)
                        .ThenInclude(sc => sc.Course)
                    .AsQueryable();

                // Apply search filter (search by name or email)
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(s =>
                        s.Name.Contains(search) ||
                        (s.ApplicationUser != null && s.ApplicationUser.Email.Contains(search)) ||
                        (s.ApplicationUser != null && s.ApplicationUser.FullName.Contains(search)));
                }

               
                query = query.OrderByDescending(s => s.CreatedAt);
               

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply pagination
                var students = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Map to StudentListDto (matching the list view)
                var items = students.Select(s => new StudentListDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Email = s.ApplicationUser?.Email ?? "N/A",
                    PhoneNumber = s.ApplicationUser?.PhoneNumber ?? "N/A",
                    FullName = s.ApplicationUser?.FullName ?? s.Name,
                    CreatedAt = s.CreatedAt,
                    CoursesCount = s.StudentCourses?.Count ?? 0,
                    LastCourseDate = s.StudentCourses?.Any() == true ? s.StudentCourses.Max(sc => sc.EnrollmentDate) : null
                }).ToList();

                var result = new SPagedResult<StudentListDto>
                {
                    Items = items,
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

        // GET: api/Admin/Students/5
        // Get student details (as shown in second screenshot - Student Details)
        [HttpGet("{id}")]
        public async Task<ActionResult<StudentDetailsDto>> GetStudent(int id)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.ApplicationUser)
                    .Include(s => s.StudentCourses)
                        .ThenInclude(sc => sc.Course)
                    .Include(s => s.CourseFeedbacks)
                        .ThenInclude(cf => cf.Course)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (student == null)
                {
                    return NotFound(new { message = $"Student with ID {id} not found." });
                }

                // Get all courses with purchase details
                var courses = await _context.StudentCourses
                    .Where(sc => sc.StudentId == id)
                    .Include(sc => sc.Course)
                    .OrderByDescending(sc => sc.EnrollmentDate)
                    .Select(sc => new StudentCourseDto
                    {
                        CourseId = sc.CourseId,
                        CourseNameArabic = sc.Course.NameArabic,
                        CourseNameEnglish = sc.Course.NameEnglish,
                        Price = sc.Course.Price,
                        Discount = sc.Course.Discount ?? 0,
                        FinalPrice = sc.Course.Price - (sc.Course.Price * (sc.Course.Discount ?? 0) / 100),
                        EnrollmentDate = sc.EnrollmentDate,
                        EnrollmentStatus = sc.EnrollmentStatus,
                        ProgressPercentage = sc.ProgressPercentage ?? 0,
                        CompletionDate = sc.CompletionDate
                    })
                    .ToListAsync();

                // Calculate totals
                var totalCourses = courses.Count;
                var totalSpent = courses.Sum(c => c.FinalPrice);
                var completedCourses = courses.Count(c => c.EnrollmentStatus == "Completed");
                var activeCourses = courses.Count(c => c.EnrollmentStatus == "Active");

                var studentDetails = new StudentDetailsDto
                {
                    Id = student.Id,
                    Name = student.Name,
                    FullName = student.ApplicationUser?.FullName ?? student.Name,
                    Email = student.ApplicationUser?.Email ?? "N/A",
                    PhoneNumber = student.ApplicationUser?.PhoneNumber ?? "N/A",
                    JoinDate = student.CreatedAt, // تاريخ الانضمام

                    // Statistics
                    TotalCourses = totalCourses,
                    CompletedCourses = completedCourses,
                    ActiveCourses = activeCourses,
                    TotalSpent = totalSpent,

                    // Courses list (as shown in the table)
                    Courses = courses,

                    // Additional info
                    CreatedAt = student.CreatedAt
                };

                return Ok(studentDetails);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        // GET: api/Admin/Students/5/courses
        // Get student's courses only (for the courses table in details page)
        [HttpGet("{id}/courses")]
        public async Task<ActionResult<SPagedResult<StudentCourseDto>>> GetStudentCourses(
            int id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            try
            {
                var studentExists = await _context.Students.AnyAsync(s => s.Id == id);
                if (!studentExists)
                {
                    return NotFound(new { message = $"Student with ID {id} not found." });
                }

                var query = _context.StudentCourses
                    .Where(sc => sc.StudentId == id)
                    .Include(sc => sc.Course)
                    .AsQueryable();

                // Apply status filter if provided
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(sc => sc.EnrollmentStatus == status);
                }

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply pagination
                var courses = await query
                    .OrderByDescending(sc => sc.EnrollmentDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(sc => new StudentCourseDto
                    {
                        Id = sc.Id,
                        CourseId = sc.CourseId,
                        CourseNameArabic = sc.Course.NameArabic,
                        CourseNameEnglish = sc.Course.NameEnglish,
                        Price = sc.Course.Price,
                        Discount = sc.Course.Discount ?? 0,
                        FinalPrice = sc.Course.Price - (sc.Course.Price * (sc.Course.Discount ?? 0) / 100),
                        EnrollmentDate = sc.EnrollmentDate,
                        EnrollmentStatus = sc.EnrollmentStatus,
                        ProgressPercentage = sc.ProgressPercentage ?? 0,
                        CompletionDate = sc.CompletionDate,
                        CreatedAt = sc.CreatedAt
                    })
                    .ToListAsync();

                var result = new SPagedResult<StudentCourseDto>
                {
                    Items = courses,
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

        // GET: api/Admin/Students/5/overview
        // Get student overview (for dashboard card)
        [HttpGet("{id}/overview")]
        public async Task<ActionResult<StudentOverviewDto>> GetStudentOverview(int id)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.ApplicationUser)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (student == null)
                {
                    return NotFound(new { message = $"Student with ID {id} not found." });
                }

                // Get quick statistics
                var totalCourses = await _context.StudentCourses
                    .CountAsync(sc => sc.StudentId == id);

                var completedCourses = await _context.StudentCourses
                    .CountAsync(sc => sc.StudentId == id && sc.EnrollmentStatus == "Completed");

                var totalSpent = await _context.StudentCourses
                    .Where(sc => sc.StudentId == id)
                    .Include(sc => sc.Course)
                    .SumAsync(sc =>
                        sc.Course.Price - (sc.Course.Price * (sc.Course.Discount ?? 0) / 100));

                var overview = new StudentOverviewDto
                {
                    Id = student.Id,
                    Name = student.Name,
                    FullName = student.ApplicationUser?.FullName ?? student.Name,
                    Email = student.ApplicationUser?.Email ?? "N/A",
                    PhoneNumber = student.ApplicationUser?.PhoneNumber ?? "N/A",
                    JoinDate = student.CreatedAt,
                    TotalCourses = totalCourses,
                    CompletedCourses = completedCourses,
                    TotalSpent = totalSpent
                };

                return Ok(overview);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }
    }

    // DTO Classes matching the screenshots

    public class SPagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    // DTO for student list (first screenshot)
    public class StudentListDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CoursesCount { get; set; }
        public DateTime? LastCourseDate { get; set; }
    }

    // DTO for student details (second screenshot)
    public class StudentDetailsDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; } // عبد الرحمن سعيد علي
        public string Email { get; set; } // abdulrahman@example.com
        public string PhoneNumber { get; set; } // +٥٠٢٠٤٥٦٧٨٩٠
        public DateTime JoinDate { get; set; } // تاريخ الانضمام: ١٠ يناير ٢٠٢٤

        // Statistics
        public int TotalCourses { get; set; } // الدورات (2)
        public int CompletedCourses { get; set; }
        public int ActiveCourses { get; set; }
        public decimal TotalSpent { get; set; } // إجمالي الإنفاق

        // Courses list - matches the table in screenshot
        public List<StudentCourseDto> Courses { get; set; } = new List<StudentCourseDto>();

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // DTO for student course (table in second screenshot)
    public class StudentCourseDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string CourseNameArabic { get; set; } // اسم الدورة
        public string CourseNameEnglish { get; set; }
        public decimal Price { get; set; } // السعر (e.g., ٩٩ $)
        public decimal Discount { get; set; }
        public decimal FinalPrice { get; set; } // السعر بعد الخصم
        public DateTime EnrollmentDate { get; set; } // تاريخ الشراء
        public string EnrollmentStatus { get; set; }
        public decimal ProgressPercentage { get; set; }
        public DateTime? CompletionDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // DTO for quick overview (for dashboard card)
    public class StudentOverviewDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime JoinDate { get; set; }
        public int TotalCourses { get; set; }
        public int CompletedCourses { get; set; }
        public decimal TotalSpent { get; set; }
    }
}