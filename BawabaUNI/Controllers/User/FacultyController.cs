using BawabaUNI.Models.Data;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;

namespace BawabaUNI.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class FacultyController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FacultyController(AppDbContext context)
        {
            _context = context;
        }
        public class FacultyResponseDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string Description { get; set; }
            public int? StudentsNumber { get; set; }
            public string DurationOfStudy { get; set; }
            public int? ProgramsNumber { get; set; }
            public int? Rank { get; set; }
            public bool RequireAcceptanceTests { get; set; }
            public int Expenses { get; set; }
            public int Coordination { get; set; }

            [MaxLength(500)]
            public string? GroupLink { get; set; }
            public string? Address { get; set; }

            public string? ImageUrl { get; set; }
            public string? DescriptionOfStudyPlan { get; set; }
            public DateTime CreatedDate { get; set; }

            // معلومات الجامعة
            public int UniversityId { get; set; }
            public string UniversityNameArabic { get; set; }
            public string UniversityNameEnglish { get; set; }
            public string UniversityType { get; set; }
            public string UniversityLocation { get; set; }

            // حقول محسوبة
            public int SpecializationsCount { get; set; }
            public int JobOpportunitiesCount { get; set; }
            public int StudyYearsCount { get; set; }
            public string ShortDescription { get; set; }
            public string AcceptanceType { get; set; }
        }

        public class StudyPlanSectionDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Code { get; set; }
            public int? CreditHours { get; set; }
            public int AcademicMaterialsCount { get; set; }

            public List<AcademicMaterialDto> AcademicMaterials { get; set; }
        }

        public class AcademicMaterialDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Code { get; set; }
            public int Semester { get; set; }
            public string Type { get; set; }
            public int CreditHours { get; set; }
        }

        public class StudyPlanMediaDto
        {
            public int Id { get; set; }
            public string MediaType { get; set; }
            public string MediaLink { get; set; }
            public string VisitLink { get; set; }
        }

        public class SpecializationDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int YearsNumber { get; set; }
            public string Description { get; set; }
            public string AcademicQualification { get; set; }
        }

        public class JobOpportunityDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
        public class FacultyDetailDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string Description { get; set; }
            public string? Address { get; set; }
            public int? UniversityId { get; set; }
            public string? ImageUrl { get; set; }
            public string? DescriptionOfStudyPlan { get; set; }
            public int? StudentsNumber { get; set; }
            public string DurationOfStudy { get; set; }
            public int? ProgramsNumber { get; set; }
            public int? Rank { get; set; }
            public int Expenses { get; set; }
            public int Coordination { get; set; }

            [MaxLength(500)]
            public string? GroupLink { get; set; }
            public bool RequireAcceptanceTests { get; set; }
            public DateTime CreatedDate { get; set; }

            public UniversityInfoDto University { get; set; }
            public List<StudyPlanYearDto> StudyPlanYears { get; set; }
            public List<SpecializationDto> SpecializationList { get; set; }
            public List<JobOpportunityDto> JobOpportunities { get; set; }
        }

        public class UniversityInfoDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string Type { get; set; }
            public string Location { get; set; }
            public string UniversityImage { get; set; }
            public string Website { get; set; }
            public string PhoneNumber { get; set; }
            public string Email { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
            public string Governate { get; set; }
            public int FoundingYear { get; set; }

            public List<DocumentRequired> Documents { get; set; }

        }

        public class StudyPlanYearDto
        {
            public int Id { get; set; }
            public string YearName { get; set; }
            public int YearNumber { get; set; }
            public string Type { get; set; }
            public DateTime CreatedDate { get; set; }
            public List<StudyPlanSectionDto> Sections { get; set; }
            public List<StudyPlanMediaDto> Media { get; set; }
            public List<AcademicMaterialDto> AcademicMaterials { get; set; }
        }
        [HttpGet("faculties")]
        public async Task<IActionResult> GetAllFaculties(
    [FromQuery] string search = null,
    [FromQuery] int? universityId = null,
    [FromQuery] string universityName = null,
    [FromQuery] string duration = null,
    [FromQuery] bool? requireTests = null,
    [FromQuery] int? minStudents = null,
    [FromQuery] int? maxStudents = null,
    [FromQuery] int? minRank = null,
    [FromQuery] string sortBy = "name",
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.Faculties
                    .Where(u => !u.IsDeleted)
                    .Include(f => f.University)
                    .Include(f => f.StudyPlanYears)
                    .Include(f => f.SpecializationList)
                    .Include(f => f.JobOpportunities)
                    .AsQueryable();

                // البحث بالنص
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();
                    query = query.Where(f =>
                        f.NameArabic.ToLower().Contains(search) ||
                        f.NameEnglish.ToLower().Contains(search) ||
                        f.Description.ToLower().Contains(search) );
                }

                // البحث بجامعة محددة (ID)
                if (universityId.HasValue)
                {
                    query = query.Where(f => f.UniversityId == universityId.Value);
                }

                // البحث بجامعة محددة (اسم)
                if (!string.IsNullOrWhiteSpace(universityName))
                {
                    universityName = universityName.Trim();
                    query = query.Where(f => f.University.NameArabic.Contains(universityName) ||
                                           f.University.NameEnglish.Contains(universityName));
                }

                // البحث بمدة الدراسة
                if (!string.IsNullOrWhiteSpace(duration))
                {
                    duration = duration.Trim();
                    query = query.Where(f => f.DurationOfStudy.Contains(duration));
                }

                // البحث بوجود اختبارات قبول
                if (requireTests.HasValue)
                {
                    query = query.Where(f => f.RequireAcceptanceTests == requireTests.Value);
                }

                // فلترة حسب عدد الطلاب (الحد الأدنى)
                if (minStudents.HasValue)
                {
                    query = query.Where(f => f.StudentsNumber >= minStudents.Value);
                }

                // فلترة حسب عدد الطلاب (الحد الأقصى)
                if (maxStudents.HasValue)
                {
                    query = query.Where(f => f.StudentsNumber <= maxStudents.Value);
                }

                // فلترة حسب الترتيب (الحد الأدنى)
                if (minRank.HasValue)
                {
                    query = query.Where(f => f.Rank >= minRank.Value);
                }

                // فلترة حسب الترتيب (الحد الأقصى)
               

                // الترتيب حسب الاختيار
                switch (sortBy.ToLower())
                {
                    case "name":
                        query = query.OrderBy(f => f.NameArabic);
                        break;
                    case "name_desc":
                        query = query.OrderByDescending(f => f.NameArabic);
                        break;
                    case "students":
                        query = query.OrderBy(f => f.StudentsNumber ?? int.MaxValue);
                        break;
                    case "students_desc":
                        query = query.OrderByDescending(f => f.StudentsNumber);
                        break;
                    case "rank":
                        query = query.OrderBy(f => f.Rank ?? int.MaxValue);
                        break;
                    case "rank_desc":
                        query = query.OrderByDescending(f => f.Rank);
                        break;
                    case "newest":
                        query = query.OrderByDescending(f => f.CreatedAt);
                        break;
                    case "oldest":
                        query = query.OrderBy(f => f.CreatedAt);
                        break;
                    default:
                        query = query.OrderBy(f => f.NameArabic);
                        break;
                }

                // حساب العدد الإجمالي
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // التجزئة
                var faculties = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(f => new FacultyResponseDto
                    {
                        Id = f.Id,
                        NameArabic = f.NameArabic,
                        NameEnglish = f.NameEnglish,
                        Description = f.Description,
                        ImageUrl = f.ImageUrl,
                        Address = f.Address,
                        DescriptionOfStudyPlan = f.DescriptionOfStudyPlan,
                        StudentsNumber = f.StudentsNumber,
                        DurationOfStudy = f.DurationOfStudy,
                        ProgramsNumber = f.ProgramsNumber,
                        Rank = f.Rank,
                        RequireAcceptanceTests = f.RequireAcceptanceTests,
                        CreatedDate = f.CreatedAt,
                        Expenses = f.Expenses,  
                        Coordination = f.Coordination,
                        GroupLink  = f.GroupLink,
                        // معلومات الجامعة
                        UniversityId = f.UniversityId ?? 0,
                        UniversityNameArabic = f.University.NameArabic,
                        UniversityNameEnglish = f.University.NameEnglish,
                        UniversityType = f.University.Type,
                        UniversityLocation = f.University.Location,
                        // حقول محسوبة
                        SpecializationsCount = f.SpecializationList != null ? f.SpecializationList.Count : 0,
                        JobOpportunitiesCount = f.JobOpportunities != null ? f.JobOpportunities.Count : 0,
                        StudyYearsCount = f.StudyPlanYears != null ? f.StudyPlanYears.Count : 0,
                        ShortDescription = f.Description.Length > 200 ?
                            f.Description.Substring(0, 200) + "..." : f.Description,
                        // تصنيف حسب الاختبارات
                        AcceptanceType = f.RequireAcceptanceTests ? "باختبارات قبول" : "بدون اختبارات قبول"
                    })
                    .ToListAsync();

                // استجابة مع بيانات التجزئة
                var response = new
                {
                    Success = true,
                    Message = "تم جلب الكليات بنجاح",
                    Data = new
                    {
                        Faculties = faculties,
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
                            UniversityId = universityId,
                            UniversityName = universityName,
                            Duration = duration,
                            RequireTests = requireTests,
                            MinStudents = minStudents,
                            MaxStudents = maxStudents,
                            MinRank = minRank,
                           
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
                    Message = "حدث خطأ أثناء جلب الكليات",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("faculties/{id}")]
        public async Task<IActionResult> GetFacultyById(int id)
        {
            try
            {
                // 1. First, get just the basic faculty info with minimal includes
                var faculty = await _context.Faculties
                    .Where(f => f.Id == id && !f.IsDeleted)
                    .Select(f => new FacultyDetailDto
                    {
                        Id = f.Id,
                        NameArabic = f.NameArabic,
                        NameEnglish = f.NameEnglish,
                        Description = f.Description,
                        ImageUrl = f.ImageUrl,
                        Address = f.Address,
                        DescriptionOfStudyPlan = f.DescriptionOfStudyPlan,
                        StudentsNumber = f.StudentsNumber,
                        DurationOfStudy = f.DurationOfStudy,
                        ProgramsNumber = f.ProgramsNumber,
                        Rank = f.Rank,
                        RequireAcceptanceTests = f.RequireAcceptanceTests,
                        CreatedDate = f.CreatedAt,
                        Expenses = f.Expenses,
                        Coordination = f.Coordination,
                        GroupLink = f.GroupLink,
                        UniversityId = f.UniversityId, // Store ID instead of full object initially
                                                       // Initialize empty lists
                        StudyPlanYears = new List<StudyPlanYearDto>(),
                        SpecializationList = new List<SpecializationDto>(),
                        JobOpportunities = new List<JobOpportunityDto>()
                    })
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (faculty == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = "الكلية غير موجودة"
                    });
                }

                // 2. Load University data separately if needed
                if (faculty.UniversityId.HasValue)
                {
                    var university = await _context.Universities
                        .Where(u => u.Id == faculty.UniversityId)
                        .Select(u => new UniversityInfoDto
                        {
                            Id = u.Id,
                            NameArabic = u.NameArabic,
                            NameEnglish = u.NameEnglish,
                            Type = u.Type,
                            Location = u.Location,
                            UniversityImage = u.UniversityImage,
                            Website = u.Website,
                            PhoneNumber = u.PhoneNumber,
                            Email = u.Email,
                            Address = u.Address,
                            City = u.City,
                            FoundingYear = u.FoundingYear,
                            Documents = u.DocumentsRequired
                                .Select(x => new DocumentRequired
                                {
                                    DocumentName = x.DocumentName,
                                    Description = x.Description
                                }).ToList()
                        })
                        .AsNoTracking()
                        .FirstOrDefaultAsync();

                    faculty.University = university;
                }

                // 3. Load StudyPlanYears with pagination or more selective loading
                var studyPlanYears = await _context.StudyPlanYears
                    .Where(y => y.FacultyId == id)
                    .Select(y => new StudyPlanYearDto
                    {
                        Id = y.Id,
                        YearName = y.YearName,
                        YearNumber = y.YearNumber,
                        Type = y.Type,
                        CreatedDate = y.CreatedAt,
                        // Limit the number of academic materials if there are many
                        AcademicMaterials = y.AcademicMaterials
                            .Select(am => new AcademicMaterialDto
                            {
                                Id = am.Id,
                                Name = am.Name,
                                Code = am.Code,
                                Semester = am.Semester,
                                Type = am.Type,
                                CreditHours = am.CreditHours
                            }).ToList(),
                        // Load sections without their academic materials to reduce complexity
                        Sections = y.Sections.Select(s => new StudyPlanSectionDto
                        {
                            Id = s.Id,
                            Name = s.Name,
                            Code = s.Code,
                            CreditHours = s.CreditHours,
                            // Load academic materials count instead of full list if needed only for stats
                            AcademicMaterialsCount = s.AcademicMaterials.Count,
                            AcademicMaterials = new List<AcademicMaterialDto>() // Empty initially
                        }).ToList(),
                        Media = y.StudyPlanMedia
                            .Select(m => new StudyPlanMediaDto
                            {
                                Id = m.Id,
                                MediaType = m.MediaType,
                                MediaLink = m.MediaLink,
                                VisitLink = m.VisitLink
                            }).ToList()
                    })
                    .AsNoTracking()
                    .ToListAsync();

                // 4. Load all academic materials in separate queries if needed
                foreach (var year in studyPlanYears)
                {
                    foreach (var section in year.Sections.Where(s => s.Id > 0))
                    {
                        var materials = await _context.AcademicMaterials
                            .Where(am => am.StudyPlanSectionId == section.Id)
                            .Select(am => new AcademicMaterialDto
                            {
                                Id = am.Id,
                                Name = am.Name,
                                Code = am.Code,
                                Semester = am.Semester,
                                Type = am.Type,
                                CreditHours = am.CreditHours
                            })
                            .AsNoTracking()
                            .ToListAsync();

                        section.AcademicMaterials = materials;
                    }
                }

                faculty.StudyPlanYears = studyPlanYears;

                // Rest of the code remains the same...
                // 5. Load Specializations
                var specializations = await _context.Specializations
                    .Where(s => s.FacultyId == id)
                    .Select(s => new SpecializationDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        YearsNumber = s.YearsNumber,
                        Description = s.Description,
                        AcademicQualification = s.AcademicQualification
                    })
                    .AsNoTracking()
                    .ToListAsync();

                faculty.SpecializationList = specializations;

                // 6. Load JobOpportunities
                var jobOpportunities = await _context.JobOpportunities
                    .Where(j => j.FacultyId == id)
                    .Select(j => new JobOpportunityDto
                    {
                        Id = j.Id,
                        Name = j.Name,
                    })
                    .AsNoTracking()
                    .ToListAsync();

                faculty.JobOpportunities = jobOpportunities;

                // Calculate statistics (update this part if needed based on changes)
                var response = new
                {
                    Success = true,
                    Message = "تم جلب الكلية بنجاح",
                    Data = new
                    {
                        Faculty = faculty,
                        Statistics = new
                        {
                            TotalYears = faculty.StudyPlanYears.Count,
                            TotalSpecializations = faculty.SpecializationList.Count,
                            TotalMaterials = faculty.StudyPlanYears.Sum(y =>
                                y.Sections.Sum(s => s.AcademicMaterials.Count)),
                            AcceptanceStatus = faculty.RequireAcceptanceTests ?
                                "تتطلب اختبارات قبول" : "لا تتطلب اختبارات قبول"
                        },
                        StudyPlanSummary = new
                        {
                            GeneralYears = faculty.StudyPlanYears.Count(y => y.Type == "General"),
                            SpecializedYears = faculty.StudyPlanYears.Count(y => y.Type == "Specialized"),
                            TotalSections = faculty.StudyPlanYears.Sum(y => y.Sections.Count),
                            TotalCreditHours = faculty.StudyPlanYears
                                .SelectMany(y => y.Sections)
                                .Where(s => s.CreditHours.HasValue)
                                .Sum(s => s.CreditHours.Value)
                        }
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                // Log the exception
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء جلب الكلية",
                    Error = ex.Message
                });
            }
        }

        
        // 3. الحصول على كليات حسب الجامعة
        [HttpGet("faculties/by-university/{universityId}")]
        public async Task<IActionResult> GetFacultiesByUniversity(int universityId)
        {
            var faculties = await _context.Faculties
                .Where(u => !u.IsDeleted)
                .Where(f => f.UniversityId == universityId)
                .OrderBy(f => f.NameArabic)
                .Select(f => new FacultyResponseDto
                {
                    Id = f.Id,
                    NameArabic = f.NameArabic,
                    NameEnglish = f.NameEnglish,
                    Description = f.Description,
                    ImageUrl = f.ImageUrl,
                    Address = f.Address,
                    DescriptionOfStudyPlan = f.DescriptionOfStudyPlan,
                    StudentsNumber = f.StudentsNumber,
                    DurationOfStudy = f.DurationOfStudy,
                    ProgramsNumber = f.ProgramsNumber,
                    Rank = f.Rank,
                    Expenses = f.Expenses,
                    Coordination = f.Coordination,
                    GroupLink = f.GroupLink,
                    RequireAcceptanceTests = f.RequireAcceptanceTests,
                    UniversityId = f.UniversityId ?? 0,
                    UniversityNameArabic = f.University.NameArabic,
                    ShortDescription = f.Description.Length > 150 ?
                        f.Description.Substring(0, 150) + "..." : f.Description
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                UniversityId = universityId,
                FacultiesCount = faculties.Count,
                Data = faculties
            });
        }

        // 4. الحصول على كليات حسب التخصص
        [HttpGet("faculties/by-specialization")]
        public async Task<IActionResult> GetFacultiesBySpecialization([FromQuery] string specialization)
        {
            var faculties = await _context.Faculties
                .Where(u => !u.IsDeleted)
              
                .OrderBy(f => f.NameArabic)
                .Select(f => new FacultyResponseDto
                {
                    Id = f.Id,
                    NameArabic = f.NameArabic,
                    NameEnglish = f.NameEnglish,
                    ImageUrl = f.ImageUrl,
                    Address = f.Address,
                    DescriptionOfStudyPlan = f.DescriptionOfStudyPlan,
                    Description = f.Description,
                    StudentsNumber = f.StudentsNumber,
                    DurationOfStudy = f.DurationOfStudy,
                    UniversityNameArabic = f.University.NameArabic,
                    UniversityLocation = f.University.Location
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                Specialization = specialization,
                FacultiesCount = faculties.Count,
                Data = faculties
            });
        }

    }
}
