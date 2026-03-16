using BawabaUNI.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BawabaUNI.Controllers.User
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudyAbroadController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public StudyAbroadController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        #region DTOs

        public class StudyAbroadResponseDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string Description { get; set; }
            public string? ImageUrl { get; set; }
            public string? Email { get; set; }
            public string? PhoneNumber { get; set; }
            public string? WhatsAppNumber { get; set; }
            public string? FacebookPage { get; set; }
            public string? Website { get; set; }
            public string? Licenses { get; set; }
            public string? Partnership { get; set; }
            public string? Services { get; set; }
            public DateTime CreatedAt { get; set; }

            // Calculated fields
            public int FacultiesCount { get; set; }
            public int HousingOptionsCount { get; set; }
            public int DocumentsRequiredCount { get; set; }
            public string ShortDescription { get; set; }
        }

        public class StudyAbroadDetailDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string Description { get; set; }
            public string? ImageUrl { get; set; }
            public string? Email { get; set; }
            public string? PhoneNumber { get; set; }
            public string? WhatsAppNumber { get; set; }
            public string? FacebookPage { get; set; }
            public string? Website { get; set; }
            public string? Licenses { get; set; }
            public string? Partnership { get; set; }
            public string? Services { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }

            // Navigation properties
            public List<FacultyForAbroadDto> Faculties { get; set; }
            public List<HousingOptionForAbroadDto> HousingOptions { get; set; }
            public List<DocumentRequiredForAbroadDto> DocumentsRequired { get; set; }
        }

        public class FacultyForAbroadDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string? ImageUrl { get; set; }
            public decimal Expenses { get; set; }
            public decimal Coordination { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class HousingOptionForAbroadDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string? PhoneNumber { get; set; }
            public string? Description { get; set; }
            public string? ImagePath { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class DocumentRequiredForAbroadDto
        {
            public int Id { get; set; }
            public string DocumentName { get; set; }
            public string? Description { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        #endregion

        /// <summary>
        /// GET: api/StudyAbroad
        /// Get all study abroad offices with filtering options
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllStudyAbroad(
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                // Base query: only study abroad entities not deleted
                var query = _context.StudyAbroads
                    .Where(s => !s.IsDeleted)
                    .Include(s => s.Faculties)
                    .Include(s => s.HousingOptions)
                    .Include(s => s.DocumentsRequired)
                    .AsQueryable();

                // Search by text (Arabic or English)
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();
                    query = query.Where(s =>
                        s.NameArabic.ToLower().Contains(search) ||
                        s.NameEnglish.ToLower().Contains(search) ||
                        s.Description.ToLower().Contains(search) ||
                        (s.Services != null && s.Services.ToLower().Contains(search)));
                }

                // Sorting
                switch (sortBy.ToLower())
                {
                    case "name":
                        query = query.OrderBy(s => s.NameArabic);
                        break;
                    case "name_desc":
                        query = query.OrderByDescending(s => s.NameArabic);
                        break;
                    case "newest":
                        query = query.OrderByDescending(s => s.CreatedAt);
                        break;
                    case "oldest":
                        query = query.OrderBy(s => s.CreatedAt);
                        break;
                    case "faculties":
                        query = query.OrderBy(s => s.Faculties.Count);
                        break;
                    case "faculties_desc":
                        query = query.OrderByDescending(s => s.Faculties.Count);
                        break;
                    default:
                        query = query.OrderBy(s => s.NameArabic);
                        break;
                }

                // Pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var studyAbroadList = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new StudyAbroadResponseDto
                    {
                        Id = s.Id,
                        NameArabic = s.NameArabic,
                        NameEnglish = s.NameEnglish,
                        Description = s.Description,
                        ImageUrl = s.ImageUrl,
                        Email = s.Email,
                        PhoneNumber = s.PhoneNumber,
                        WhatsAppNumber = s.WhatsAppNumber,
                        FacebookPage = s.FacebookPage,
                        Website = s.Website,
                        Licenses = s.Licenses,
                        Partnership = s.Partnership,
                        Services = s.Services,
                        CreatedAt = s.CreatedAt,

                        // Calculated fields
                        FacultiesCount = s.Faculties != null ? s.Faculties.Count(f => !f.IsDeleted) : 0,
                        HousingOptionsCount = s.HousingOptions != null ? s.HousingOptions.Count(h => !h.IsDeleted) : 0,
                        DocumentsRequiredCount = s.DocumentsRequired != null ? s.DocumentsRequired.Count(d => !d.IsDeleted) : 0,
                        ShortDescription = s.Description.Length > 150
                            ? s.Description.Substring(0, 150) + "..."
                            : s.Description
                    })
                    .ToListAsync();

                var response = new
                {
                    Success = true,
                    Message = "تم جلب مكاتب الدراسة بالخارج بنجاح",
                    Data = new
                    {
                        StudyAbroad = studyAbroadList,
                        TotalCount = totalCount,
                        TotalPages = totalPages,
                        CurrentPage = page,
                        PageSize = pageSize,
                        HasPreviousPage = page > 1,
                        HasNextPage = page < totalPages,
                        Filters = new
                        {
                            Search = search,
                            SortBy = sortBy
                        }
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء جلب مكاتب الدراسة بالخارج",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// GET: api/StudyAbroad/{id}
        /// Get study abroad office by ID with all details including faculties, housing, and documents
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStudyAbroadById(int id)
        {
            try
            {
                // 1. Get basic study abroad info
                var studyAbroad = await _context.StudyAbroads
                    .Where(s => s.Id == id && !s.IsDeleted)
                    .Select(s => new StudyAbroadDetailDto
                    {
                        Id = s.Id,
                        NameArabic = s.NameArabic,
                        NameEnglish = s.NameEnglish,
                        Description = s.Description,
                        ImageUrl = s.ImageUrl,
                        Email = s.Email,
                        PhoneNumber = s.PhoneNumber,
                        WhatsAppNumber = s.WhatsAppNumber,
                        FacebookPage = s.FacebookPage,
                        Website = s.Website,
                        Licenses = s.Licenses,
                        Partnership = s.Partnership,
                        Services = s.Services,
                        CreatedAt = s.CreatedAt,
                        UpdatedAt = DateTime.UtcNow // Assuming we want to return the current time as updated time since we don't have it in the entity
                    })
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (studyAbroad == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = "مكتب الدراسة بالخارج غير موجود"
                    });
                }

                // 2. Get faculties (programs) with their expenses and coordination
                var faculties = await _context.FacultiesForAbroad
                    .Where(f => f.StudyAbroadId == id && !f.IsDeleted)
                    .Select(f => new FacultyForAbroadDto
                    {
                        Id = f.Id,
                        NameArabic = f.NameArabic,
                        NameEnglish = f.NameEnglish,
                        ImageUrl = f.ImageUrl,
                        Expenses = f.Expenses,
                        Coordination = f.Coordination,
                        CreatedAt = f.CreatedAt
                    })
                    .AsNoTracking()
                    .ToListAsync();

                studyAbroad.Faculties = faculties;

                // 3. Get housing options
                var housingOptions = await _context.HousingOptionsForAbroad
                    .Where(h => h.StudyAbroadId == id && !h.IsDeleted)
                    .Select(h => new HousingOptionForAbroadDto
                    {
                        Id = h.Id,
                        Name = h.Name,
                        PhoneNumber = h.PhoneNumber,
                        Description = h.Description,
                        ImagePath = h.ImagePath,
                        CreatedAt = h.CreatedAt
                    })
                    .AsNoTracking()
                    .ToListAsync();

                studyAbroad.HousingOptions = housingOptions;

                // 4. Get required documents
                var documentsRequired = await _context.DocumentsRequiredForStudyAbroad
                    .Where(d => d.StudyAbroadId == id && !d.IsDeleted)
                    .Select(d => new DocumentRequiredForAbroadDto
                    {
                        Id = d.Id,
                        DocumentName = d.DocumentName,
                        Description = d.Description,
                        CreatedAt = d.CreatedAt
                    })
                    .AsNoTracking()
                    .ToListAsync();

                studyAbroad.DocumentsRequired = documentsRequired;

                // Calculate statistics
                var response = new
                {
                    Success = true,
                    Message = "تم جلب المكتب بنجاح",
                    Data = new
                    {
                        StudyAbroad = studyAbroad,
                        Statistics = new
                        {
                            TotalFaculties = studyAbroad.Faculties.Count,
                            TotalHousingOptions = studyAbroad.HousingOptions.Count,
                            TotalDocumentsRequired = studyAbroad.DocumentsRequired.Count,
                            AverageExpenses = studyAbroad.Faculties.Any()
                                ? Math.Round(studyAbroad.Faculties.Average(f => f.Expenses), 2)
                                : 0,
                            AverageCoordination = studyAbroad.Faculties.Any()
                                ? Math.Round(studyAbroad.Faculties.Average(f => f.Coordination), 2)
                                : 0,
                            MinExpenses = studyAbroad.Faculties.Any()
                                ? studyAbroad.Faculties.Min(f => f.Expenses)
                                : 0,
                            MaxExpenses = studyAbroad.Faculties.Any()
                                ? studyAbroad.Faculties.Max(f => f.Expenses)
                                : 0,
                        },
                        FacultiesSummary = studyAbroad.Faculties
                            .OrderByDescending(f => f.Expenses)
                            .Take(5)
                            .Select(f => new
                            {
                                f.Id,
                                f.NameArabic,
                                f.NameEnglish,
                                f.Expenses,
                                f.Coordination,
                                f.ImageUrl
                            }).ToList(),
                        HousingSummary = new
                        {
                            HasHousing = studyAbroad.HousingOptions.Any(),
                            OptionsCount = studyAbroad.HousingOptions.Count,
                            Options = studyAbroad.HousingOptions.Select(h => new
                            {
                                h.Id,
                                h.Name,
                                h.PhoneNumber,
                                h.Description
                            }).ToList()
                        },
                        DocumentsSummary = studyAbroad.DocumentsRequired
                            .Select(d => new
                            {
                                d.Id,
                                d.DocumentName,
                                d.Description
                            }).ToList()
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء جلب المكتب",
                    Error = ex.Message
                });
            }
        }
    }
}