using BawabaUNI.Models.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BawabaUNI.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class UniversityController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UniversityController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("universities")]
        public async Task<IActionResult> GetAllUniversities(
    [FromQuery] string search = null,
    [FromQuery] string type = null,
    [FromQuery] string governate = null,
    [FromQuery] string city = null,
    [FromQuery] int? minYear = null,
    [FromQuery] int? maxYear = null,
    [FromQuery] int? minStudents = null,
    [FromQuery] int? maxStudents = null,
    [FromQuery] bool? isTrending = null,
    [FromQuery] string sortBy = "name",
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.Universities
                    .Where(u=>!u.IsDeleted)
                    .Include(u => u.DocumentsRequired)
                    .Include(u => u.HousingOptions)
                    .Include(u => u.Faculties)
                    .AsQueryable();
                
                // البحث بالنص
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();
                    query = query.Where(u =>
                        u.NameArabic.ToLower().Contains(search) ||
                        u.NameEnglish.ToLower().Contains(search) ||
                        u.Description.ToLower().Contains(search) ||
                        u.Location.ToLower().Contains(search) ||
                        u.Address.ToLower().Contains(search) ||
                        u.City.ToLower().Contains(search) ||
                        u.Governate.ToLower().Contains(search));
                }

                // البحث بالنوع
                if (!string.IsNullOrWhiteSpace(type))
                {
                    type = type.Trim();
                    query = query.Where(u => u.Type.Contains(type));
                }

                // البحث بالمحافظة
                if (!string.IsNullOrWhiteSpace(governate))
                {
                    governate = governate.Trim();
                    query = query.Where(u => u.Governate.Contains(governate));
                }

                // البحث بالمدينة
                if (!string.IsNullOrWhiteSpace(city))
                {
                    city = city.Trim();
                    query = query.Where(u => u.City.Contains(city));
                }

                // فلترة حسب سنة التأسيس (من)
                if (minYear.HasValue)
                {
                    query = query.Where(u => u.FoundingYear >= minYear.Value);
                }

                // فلترة حسب سنة التأسيس (إلى)
                if (maxYear.HasValue)
                {
                    query = query.Where(u => u.FoundingYear <= maxYear.Value);
                }

                // فلترة حسب عدد الطلاب (الحد الأدنى)
                if (minStudents.HasValue)
                {
                    query = query.Where(u => u.StudentsNumber >= minStudents.Value);
                }

                // فلترة حسب عدد الطلاب (الحد الأقصى)
                if (maxStudents.HasValue)
                {
                    query = query.Where(u => u.StudentsNumber <= maxStudents.Value);
                }

                // فلترة حسب Trending
                if (isTrending.HasValue)
                {
                    query = query.Where(u => u.IsTrending == isTrending.Value);
                }

                // الترتيب حسب الاختيار
                switch (sortBy.ToLower())
                {
                    case "name":
                        query = query.OrderBy(u => u.NameArabic);
                        break;
                    case "name_desc":
                        query = query.OrderByDescending(u => u.NameArabic);
                        break;
                    case "year_asc":
                        query = query.OrderBy(u => u.FoundingYear);
                        break;
                    case "year_desc":
                        query = query.OrderByDescending(u => u.FoundingYear);
                        break;
                    case "ranking":
                        query = query.OrderBy(u => u.GlobalRanking ?? int.MaxValue);
                        break;
                    case "ranking_desc":
                        query = query.OrderByDescending(u => u.GlobalRanking);
                        break;
                    case "students":
                        query = query.OrderBy(u => u.StudentsNumber ?? int.MaxValue);
                        break;
                    case "students_desc":
                        query = query.OrderByDescending(u => u.StudentsNumber);
                        break;
                    case "newest":
                        query = query.OrderByDescending(u => u.CreatedAt);
                        break;
                    case "oldest":
                        query = query.OrderBy(u => u.CreatedAt);
                        break;
                    default:
                        query = query.OrderBy(u => u.NameArabic);
                        break;
                }

                // حساب العدد الإجمالي
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // التجزئة
                var universities = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new UniversityResponseDto
                    {
                        Id = u.Id,
                        Type = u.Type,
                        NameArabic = u.NameArabic,
                        NameEnglish = u.NameEnglish,
                        IsTrending = u.IsTrending,
                        Description = u.Description,
                        FoundingYear = u.FoundingYear,
                        StudentsNumber = u.StudentsNumber,
                        Location = u.Location,
                        GlobalRanking = u.GlobalRanking,
                        UniversityImage = u.UniversityImage,
                        Email = u.Email,
                        Website = u.Website,
                        PhoneNumber = u.PhoneNumber,
                        FacebookPage = u.FacebookPage,
                        Address = u.Address,
                        City = u.City,
                        Governate = u.Governate,
                        PostalCode = u.PostalCode,
                        CreatedDate = u.CreatedAt,
                        // حقول محسوبة
                        FacultiesCount = u.Faculties != null ? u.Faculties.Count : 0,
                        HasHousing = u.HousingOptions != null && u.HousingOptions.Any(),
                        ShortDescription = u.Description.Length > 200 ?
                            u.Description.Substring(0, 200) + "..." : u.Description
                    })
                    .ToListAsync();

                // استجابة مع بيانات التجزئة
                var response = new
                {
                    Success = true,
                    Message = "تم جلب الجامعات بنجاح",
                    Data = new
                    {
                        Universities = universities,
                        TotalCount = totalCount,
                        TotalPages = totalPages,
                        CurrentPage = page,
                        PageSize = pageSize,
                        HasPreviousPage = page > 1,
                        HasNextPage = page < totalPages,
                        SearchQuery = search,
                        SortBy = sortBy,
                        Filters = new
                        {
                            Type = type,
                            Governate = governate,
                            City = city,
                            MinYear = minYear,
                            MaxYear = maxYear,
                            MinStudents = minStudents,
                            MaxStudents = maxStudents,
                            IsTrending = isTrending
                        },
                        Summary = new
                        {
                            PublicCount = await _context.Universities.CountAsync(u => u.Type == "حكومية"),
                            PrivateCount = await _context.Universities.CountAsync(u => u.Type == "خاصة"),
                            TrendingCount = await _context.Universities.Where(u => !u.IsDeleted).CountAsync(u => u.IsTrending)
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
                    Message = "حدث خطأ أثناء جلب الجامعات",
                    Error = ex.Message
                });
            }
        }

        // 2. الحصول على جامعة واحدة بالكامل حسب ID
        [HttpGet("universities/{id}")]
        public async Task<IActionResult> GetUniversityById(int id)
        {
            try
            {
                var university = await _context.Universities
                    .Where(u => !u.IsDeleted)
                    .Include(u => u.DocumentsRequired)
                    .Include(u => u.HousingOptions)
                    .Include(u => u.Faculties)
                    .Where(u => u.Id == id)
                    .Select(u => new UniversityDetailDto
                    {
                        Id = u.Id,
                        Type = u.Type,
                        NameArabic = u.NameArabic,
                        NameEnglish = u.NameEnglish,
                        IsTrending = u.IsTrending,
                        Description = u.Description,
                        FoundingYear = u.FoundingYear,
                        StudentsNumber = u.StudentsNumber,
                        Location = u.Location,
                        GlobalRanking = u.GlobalRanking,
                        UniversityImage = u.UniversityImage,
                        Email = u.Email,
                        Website = u.Website,
                        PhoneNumber = u.PhoneNumber,
                        FacebookPage = u.FacebookPage,
                        Address = u.Address,
                        City = u.City,
                        Governate = u.Governate,
                        PostalCode = u.PostalCode,
                        CreatedDate = u.CreatedAt,
                        DocumentsRequired = u.DocumentsRequired != null ?
                            u.DocumentsRequired.Select(d => new DocumentDto
                            {
                                Id = d.Id,
                                Name = d.DocumentName,
                                Description = d.Description,
                          
                            }).ToList() : new List<DocumentDto>(),
                        HousingOptions = u.HousingOptions != null ?
                            u.HousingOptions.Select(h => new HousingOptionDto
                            {
                                Id = h.Id,
                                Name = h.Name,
                                PhoneNumber = h.PhoneNumber,
                                Description = h.Description,
                                ImagePath = h.ImagePath
                            }).ToList() : new List<HousingOptionDto>(),
                        Faculties = u.Faculties != null ?
                            u.Faculties.Select(f => new FacultyDto
                            {
                                Id = f.Id,
                                NameArabic = f.NameArabic,
                                NameEnglish = f.NameEnglish,
                                Description = f.Description
                            }).ToList() : new List<FacultyDto>()
                    })
                    .FirstOrDefaultAsync();

                if (university == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = "الجامعة غير موجودة"
                    });
                }

                // الحصول على جامعات مشابهة (بنفس النوع أو المحافظة)
              
                var response = new
                {
                    Success = true,
                    Message = "تم جلب الجامعة بنجاح",
                    Data = new
                    {
                        University = university,
                
                        Statistics = new
                        {
                            Age = DateTime.Now.Year - university.FoundingYear,
                            FacultiesCount = university.Faculties.Count,
                            HousingOptionsCount = university.HousingOptions.Count,
                            DocumentsCount = university.DocumentsRequired.Count,
                            HasContactInfo = !string.IsNullOrEmpty(university.PhoneNumber) ||
                                           !string.IsNullOrEmpty(university.Email)
                        },
                        Contact = new
                        {
                            Phone = university.PhoneNumber,
                            Email = university.Email,
                            Website = university.Website,
                            Facebook = university.FacebookPage,
                            Address = university.Address
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
                    Message = "حدث خطأ أثناء جلب الجامعة",
                    Error = ex.Message
                });
            }
        }

        // 3. الحصول على الجامعات الرائجة (Trending)
        [HttpGet("universities/trending")]
        public async Task<IActionResult> GetTrendingUniversities([FromQuery] int count = 10)
        {
            var universities = await _context.Universities
                .Where(u => u.IsTrending)
                .Where(u=>!u.IsDeleted)
                .OrderBy(u => u.GlobalRanking ?? int.MaxValue)
                .Take(count)
                .Select(u => new UniversityResponseDto
                {
                    Id = u.Id,
                    Type = u.Type,
                    NameArabic = u.NameArabic,
                    NameEnglish = u.NameEnglish,
                    Description = u.Description,
                    FoundingYear = u.FoundingYear,
                    StudentsNumber = u.StudentsNumber,
                    Location = u.Location,
                    GlobalRanking = u.GlobalRanking,
                    UniversityImage = u.UniversityImage,
                    City = u.City,
                    Governate = u.Governate,
                    ShortDescription = u.Description.Length > 150 ?
                        u.Description.Substring(0, 150) + "..." : u.Description
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Data = universities,
                Count = universities.Count,
                Trending = true
            });
        }

        // 4. الحصول على الجامعات حسب النوع
        [HttpGet("universities/by-type/{type}")]
        public async Task<IActionResult> GetUniversitiesByType(string type,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = _context.Universities
                .Where(u => u.Type.ToLower() == type.ToLower())
                .OrderBy(u => u.NameArabic);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var universities = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UniversityResponseDto
                {
                    Id = u.Id,
                    Type = u.Type,
                    NameArabic = u.NameArabic,
                    NameEnglish = u.NameEnglish,
                    Description = u.Description,
                    FoundingYear = u.FoundingYear,
                    StudentsNumber = u.StudentsNumber,
                    Location = u.Location,
                    GlobalRanking = u.GlobalRanking,
                    UniversityImage = u.UniversityImage,
                    City = u.City,
                    Governate = u.Governate
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Data = universities,
                Type = type,
                TotalCount = totalCount,
                TotalPages = totalPages,
                CurrentPage = page
            });
        }
        public class UniversityResponseDto
        {
            public int Id { get; set; }
            public string Type { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public bool IsTrending { get; set; }
            public string Description { get; set; }
            public int FoundingYear { get; set; }
            public int? StudentsNumber { get; set; }
            public string Location { get; set; }
            public int? GlobalRanking { get; set; }
            public string UniversityImage { get; set; }
            public string Email { get; set; }
            public string Website { get; set; }
            public string PhoneNumber { get; set; }
            public string FacebookPage { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
            public string Governate { get; set; }
            public string PostalCode { get; set; }
            public DateTime CreatedDate { get; set; }
            public int FacultiesCount { get; set; }
            public bool HasHousing { get; set; }
            public string ShortDescription { get; set; }
        }

        public class UniversityDetailDto : UniversityResponseDto
        {
            public List<DocumentDto> DocumentsRequired { get; set; }
            public List<HousingOptionDto> HousingOptions { get; set; }
            public List<FacultyDto> Faculties { get; set; }
        }

        public class DocumentDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public bool IsRequired { get; set; }
        }

        public class HousingOptionDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string PhoneNumber { get; set; }
            public string Description { get; set; }
            public string ImagePath { get; set; }
        }

        public class FacultyDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string Description { get; set; }
        }
    }
}
