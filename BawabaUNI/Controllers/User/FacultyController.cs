using BawabaUNI.Models.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            public string Specializations { get; set; }
            public bool RequireAcceptanceTests { get; set; }
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

        public class FacultyDetailDto : FacultyResponseDto
        {
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
        }

        public class StudyPlanSectionDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Code { get; set; }
            public int? CreditHours { get; set; }
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
                        f.Description.ToLower().Contains(search) ||
                        f.Specializations.ToLower().Contains(search));
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
                        StudentsNumber = f.StudentsNumber,
                        DurationOfStudy = f.DurationOfStudy,
                        ProgramsNumber = f.ProgramsNumber,
                        Rank = f.Rank,
                        Specializations = f.Specializations,
                        RequireAcceptanceTests = f.RequireAcceptanceTests,
                        CreatedDate = f.CreatedAt,
                        // معلومات الجامعة
                        UniversityId = f.UniversityId,
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

        // 2. الحصول على كلية واحدة بالكامل حسب ID
        [HttpGet("faculties/{id}")]
        public async Task<IActionResult> GetFacultyById(int id)
        {
            try
            {
                var faculty = await _context.Faculties
                    .Where(u => !u.IsDeleted)
                    .Include(f => f.University)
                    .Include(f => f.StudyPlanYears)
                        .ThenInclude(spy => spy.Sections)
                            .ThenInclude(sps => sps.AcademicMaterials)
                    .Include(f => f.StudyPlanYears)
                        .ThenInclude(spy => spy.StudyPlanMedia)
                    .Include(f => f.SpecializationList)
                    .Include(f => f.JobOpportunities)
                    .Where(f => f.Id == id)
                    .Select(f => new FacultyDetailDto
                    {
                        Id = f.Id,
                        NameArabic = f.NameArabic,
                        NameEnglish = f.NameEnglish,
                        Description = f.Description,
                        StudentsNumber = f.StudentsNumber,
                        DurationOfStudy = f.DurationOfStudy,
                        ProgramsNumber = f.ProgramsNumber,
                        Rank = f.Rank,
                        Specializations = f.Specializations,
                        RequireAcceptanceTests = f.RequireAcceptanceTests,
                        CreatedDate = f.CreatedAt,
                        // معلومات الجامعة
                        University = f.University != null ? new UniversityInfoDto
                        {
                            Id = f.University.Id,
                            NameArabic = f.University.NameArabic,
                            NameEnglish = f.University.NameEnglish,
                            Type = f.University.Type,
                            Location = f.University.Location,
                            UniversityImage = f.University.UniversityImage,
                            Website = f.University.Website,
                            PhoneNumber = f.University.PhoneNumber,
                            Email = f.University.Email,
                            Address = f.University.Address,
                            City = f.University.City,
                            Governate = f.University.Governate
                        } : null,
                        // خطة الدراسة
                        StudyPlanYears = f.StudyPlanYears != null ? f.StudyPlanYears.Select(spy => new StudyPlanYearDto
                        {
                            Id = spy.Id,
                            YearName = spy.YearName,
                            YearNumber = spy.YearNumber,
                            Type = spy.Type,
                            CreatedDate = spy.CreatedAt,
                            Sections = spy.Sections != null ? spy.Sections.Select(sps => new StudyPlanSectionDto
                            {
                                Id = sps.Id,
                                Name = sps.Name,
                                Code = sps.Code,
                                CreditHours = sps.CreditHours,
                                AcademicMaterials = sps.AcademicMaterials != null ? sps.AcademicMaterials.Select(am => new AcademicMaterialDto
                                {
                                    Id = am.Id,
                                    Name = am.Name,
                                    Code = am.Code,
                                    Semester = am.Semester,
                                    Type = am.Type,
                                    CreditHours = am.CreditHours
                                }).ToList() : new List<AcademicMaterialDto>()
                            }).ToList() : new List<StudyPlanSectionDto>(),
                            Media = spy.StudyPlanMedia != null ? spy.StudyPlanMedia.Select(spm => new StudyPlanMediaDto
                            {
                                Id = spm.Id,
                                MediaType = spm.MediaType,
                                MediaLink = spm.MediaLink,
                                VisitLink = spm.VisitLink
                            }).ToList() : new List<StudyPlanMediaDto>()
                        }).ToList() : new List<StudyPlanYearDto>(),
                        // التخصصات
                        SpecializationList = f.SpecializationList != null ? f.SpecializationList.Select(s => new SpecializationDto
                        {
                            Id = s.Id,
                            Name = s.Name,
                            YearsNumber = s.YearsNumber,
                            Description = s.Description,
                            AcademicQualification = s.AcademicQualification
                        }).ToList() : new List<SpecializationDto>(),
                        // فرص العمل
                        JobOpportunities = f.JobOpportunities != null ? f.JobOpportunities.Select(j => new JobOpportunityDto
                        {
                            Id = j.Id,
                            Name = j.Name
                        }).ToList() : new List<JobOpportunityDto>()
                    })
                    .FirstOrDefaultAsync();

                if (faculty == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = "الكلية غير موجودة"
                    });
                }

                // الحصول على كليات مشابهة (بنفس الجامعة أو نفس التخصصات)
               

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
                            TotalJobOpportunities = faculty.JobOpportunities.Count,
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
                    StudentsNumber = f.StudentsNumber,
                    DurationOfStudy = f.DurationOfStudy,
                    ProgramsNumber = f.ProgramsNumber,
                    Rank = f.Rank,
                    RequireAcceptanceTests = f.RequireAcceptanceTests,
                    UniversityId = f.UniversityId,
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
                .Where(f => f.Specializations.Contains(specialization))
                .OrderBy(f => f.NameArabic)
                .Select(f => new FacultyResponseDto
                {
                    Id = f.Id,
                    NameArabic = f.NameArabic,
                    NameEnglish = f.NameEnglish,
                    Description = f.Description,
                    StudentsNumber = f.StudentsNumber,
                    DurationOfStudy = f.DurationOfStudy,
                    Specializations = f.Specializations,
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
