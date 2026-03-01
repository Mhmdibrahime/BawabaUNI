using BawabaUNI.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BawabaUNI.Controllers.User
{
    [ApiController]
    [Route("api/[controller]")]
    public class InstitutesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public InstitutesController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // DTO classes defined inside the controller
        #region DTOs

        public class InstituteResponseDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string Description { get; set; }
            public string ImageUrl { get; set; }
            public string Address { get; set; }
            public string DescriptionOfStudyPlan { get; set; }
            public int? StudentsNumber { get; set; }
            public string DurationOfStudy { get; set; }
            public int? ProgramsNumber { get; set; }
            public int? Rank { get; set; }
            public bool RequireAcceptanceTests { get; set; }
            public DateTime CreatedDate { get; set; }
            public int Expenses { get; set; }
            public int Coordination { get; set; }
            public string GroupLink { get; set; }

            // Institute specific fields
            public string Type { get; set; } // "معهد حكومي", "معهد خاص"
            public bool? HasHousing { get; set; }
            

            // Housing options
            public List<HousingOptionDto> HousingOptions { get; set; }

            // Calculated fields
            public int SpecializationsCount { get; set; }
            public int JobOpportunitiesCount { get; set; }
            public int StudyYearsCount { get; set; }
            public string ShortDescription { get; set; }
            public string AcceptanceType { get; set; }
        }

        public class HousingOptionDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string PhoneNumber { get; set; }
            public string Description { get; set; }
            public string ImagePath { get; set; }
        }

        public class InstituteDetailDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string Description { get; set; }
            public string ImageUrl { get; set; }
            public string Address { get; set; }
            public string DescriptionOfStudyPlan { get; set; }
            public int? StudentsNumber { get; set; }
            public string DurationOfStudy { get; set; }
            public int? ProgramsNumber { get; set; }
            public int? Rank { get; set; }
            public bool RequireAcceptanceTests { get; set; }
            public DateTime CreatedDate { get; set; }
            public int Expenses { get; set; }
            public int Coordination { get; set; }
            public string GroupLink { get; set; }

            // Institute specific fields
            public string Type { get; set; }
            public bool? HasHousing { get; set; }
            public string InstituteType { get; set; }
            public string Accreditation { get; set; }
            public string DirectorName { get; set; }
            public string DirectorPhone { get; set; }
            public int? EstablishedYear { get; set; }

            // Navigation properties
            public List<StudyPlanYearDto> StudyPlanYears { get; set; }
            public List<SpecializationDto> SpecializationList { get; set; }
            public List<JobOpportunityDto> JobOpportunities { get; set; }
            public List<HousingOptionDto> HousingOptions { get; set; }
        }

        public class StudyPlanYearDto
        {
            public int Id { get; set; }
            public string YearName { get; set; }
            public int YearNumber { get; set; }
            public string Type { get; set; }
            public DateTime CreatedDate { get; set; }
            public List<AcademicMaterialDto> AcademicMaterials { get; set; }
            public List<StudyPlanSectionDto> Sections { get; set; }
            public List<StudyPlanMediaDto> Media { get; set; }
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

        public class StudyPlanSectionDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Code { get; set; }
            public int? CreditHours { get; set; }
            public List<AcademicMaterialDto> AcademicMaterials { get; set; }
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

        #endregion

        /// <summary>
        /// GET: api/institutes?type=معهد حكومي&search=تقني&hasHousing=true
        /// Get all institutes with filtering by type
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllInstitutes(
            [FromQuery] string type = null, // "معهد حكومي", "معهد خاص", or any other type
            [FromQuery] string search = null,
            [FromQuery] bool? hasHousing = null,
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
                // Base query: only institutes (UniversityId == null) and not deleted
                var query = _context.Faculties
                    .Where(i => i.UniversityId == null && !i.IsDeleted)
                    .Include(i => i.FacultyHousingOption)
                    .Include(i => i.StudyPlanYears)
                    .Include(i => i.SpecializationList)
                    .Include(i => i.JobOpportunities)
                    .AsQueryable();

                // Filter by type (معهد حكومي, معهد خاص, etc.)
                if (!string.IsNullOrWhiteSpace(type))
                {
                    type = type.Trim();
                    query = query.Where(i => i.Type == type);
                }

               

                // Filter by housing availability
                if (hasHousing.HasValue)
                {
                    query = query.Where(i => i.HasHousing == hasHousing.Value);
                }

                // Search by text
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();
                    query = query.Where(i =>
                        i.NameArabic.ToLower().Contains(search) ||
                        i.NameEnglish.ToLower().Contains(search) ||
                        i.Description.ToLower().Contains(search));
                }

                // Filter by duration
                if (!string.IsNullOrWhiteSpace(duration))
                {
                    duration = duration.Trim();
                    query = query.Where(i => i.DurationOfStudy.Contains(duration));
                }

                // Filter by acceptance tests
                if (requireTests.HasValue)
                {
                    query = query.Where(i => i.RequireAcceptanceTests == requireTests.Value);
                }

                // Filter by students count
                if (minStudents.HasValue)
                {
                    query = query.Where(i => i.StudentsNumber >= minStudents.Value);
                }
                if (maxStudents.HasValue)
                {
                    query = query.Where(i => i.StudentsNumber <= maxStudents.Value);
                }

                // Filter by rank
                if (minRank.HasValue)
                {
                    query = query.Where(i => i.Rank >= minRank.Value);
                }

                // Sorting
                switch (sortBy.ToLower())
                {
                    case "name":
                        query = query.OrderBy(i => i.NameArabic);
                        break;
                    case "name_desc":
                        query = query.OrderByDescending(i => i.NameArabic);
                        break;
                    case "students":
                        query = query.OrderBy(i => i.StudentsNumber ?? int.MaxValue);
                        break;
                    case "students_desc":
                        query = query.OrderByDescending(i => i.StudentsNumber);
                        break;
                    case "rank":
                        query = query.OrderBy(i => i.Rank ?? int.MaxValue);
                        break;
                    case "rank_desc":
                        query = query.OrderByDescending(i => i.Rank);
                        break;
                   
                    case "newest":
                        query = query.OrderByDescending(i => i.CreatedAt);
                        break;
                    case "oldest":
                        query = query.OrderBy(i => i.CreatedAt);
                        break;
                    default:
                        query = query.OrderBy(i => i.NameArabic);
                        break;
                }

                // Pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var institutes = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(i => new InstituteResponseDto
                    {
                        Id = i.Id,
                        NameArabic = i.NameArabic,
                        NameEnglish = i.NameEnglish,
                        Description = i.Description,
                        ImageUrl = i.ImageUrl,
                        Address = i.Address,
                        DescriptionOfStudyPlan = i.DescriptionOfStudyPlan,
                        StudentsNumber = i.StudentsNumber,
                        DurationOfStudy = i.DurationOfStudy,
                        ProgramsNumber = i.ProgramsNumber,
                        Rank = i.Rank,
                        RequireAcceptanceTests = i.RequireAcceptanceTests,
                        CreatedDate = i.CreatedAt,
                        Expenses = i.Expenses,
                        Coordination = i.Coordination,
                        GroupLink = i.GroupLink,

                        // Institute specific fields
                        Type = i.Type,
                        HasHousing = i.HasHousing,
                       
                        // Housing options
                        HousingOptions = i.FacultyHousingOption != null
                            ? i.FacultyHousingOption.Select(h => new HousingOptionDto
                            {
                                Id = h.Id,
                                Name = h.Name,
                                PhoneNumber = h.PhoneNumber,
                                Description = h.Description,
                                ImagePath = h.ImagePath
                            }).ToList()
                            : new List<HousingOptionDto>(),

                        // Calculated fields
                        SpecializationsCount = i.SpecializationList != null ? i.SpecializationList.Count : 0,
                        JobOpportunitiesCount = i.JobOpportunities != null ? i.JobOpportunities.Count : 0,
                        StudyYearsCount = i.StudyPlanYears != null ? i.StudyPlanYears.Count : 0,
                        ShortDescription = i.Description.Length > 200
                            ? i.Description.Substring(0, 200) + "..."
                            : i.Description,
                        AcceptanceType = i.RequireAcceptanceTests ? "باختبارات قبول" : "بدون اختبارات قبول"
                    })
                    .ToListAsync();

                var response = new
                {
                    Success = true,
                    Message = "تم جلب المعاهد بنجاح",
                    Data = new
                    {
                        Institutes = institutes,
                        TotalCount = totalCount,
                        TotalPages = totalPages,
                        CurrentPage = page,
                        PageSize = pageSize,
                        HasPreviousPage = page > 1,
                        HasNextPage = page < totalPages,
                        Filters = new
                        {
                            Type = type,
                            HasHousing = hasHousing,
                            Search = search,
                            Duration = duration,
                            RequireTests = requireTests,
                            MinStudents = minStudents,
                            MaxStudents = maxStudents,
                            MinRank = minRank,
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
                    Message = "حدث خطأ أثناء جلب المعاهد",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// GET: api/institutes/{id}
        /// Get institute by ID with all details including housing options
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetInstituteById(int id)
        {
            try
            {
                // Get basic institute info
                var institute = await _context.Faculties
                    .Where(i => i.Id == id && i.UniversityId == null && !i.IsDeleted)
                    .Select(i => new InstituteDetailDto
                    {
                        Id = i.Id,
                        NameArabic = i.NameArabic,
                        NameEnglish = i.NameEnglish,
                        Description = i.Description,
                        ImageUrl = i.ImageUrl,
                        Address = i.Address,
                        DescriptionOfStudyPlan = i.DescriptionOfStudyPlan,
                        StudentsNumber = i.StudentsNumber,
                        DurationOfStudy = i.DurationOfStudy,
                        ProgramsNumber = i.ProgramsNumber,
                        Rank = i.Rank,
                        RequireAcceptanceTests = i.RequireAcceptanceTests,
                        CreatedDate = i.CreatedAt,
                        Expenses = i.Expenses,
                        Coordination = i.Coordination,
                        GroupLink = i.GroupLink,

                        // Institute specific fields
                        Type = i.Type,
                        HasHousing = i.HasHousing,
                       

                        // Initialize empty lists
                        StudyPlanYears = new List<StudyPlanYearDto>(),
                        SpecializationList = new List<SpecializationDto>(),
                        JobOpportunities = new List<JobOpportunityDto>(),
                        HousingOptions = new List<HousingOptionDto>()
                    })
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (institute == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = "المعهد غير موجود"
                    });
                }

                // Load housing options
                var housingOptions = await _context.FacultyHousingOptions
                    .Where(h => h.FacultyId == id && !h.IsDeleted)
                    .Select(h => new HousingOptionDto
                    {
                        Id = h.Id,
                        Name = h.Name,
                        PhoneNumber = h.PhoneNumber,
                        Description = h.Description,
                        ImagePath = h.ImagePath
                    })
                    .AsNoTracking()
                    .ToListAsync();

                institute.HousingOptions = housingOptions;

                // Load study plan years with sections and materials
                var studyPlanYears = await _context.StudyPlanYears
                    .Where(y => y.FacultyId == id && !y.IsDeleted)
                    .Include(y => y.Sections)
                        .ThenInclude(s => s.AcademicMaterials)
                    .Include(y => y.StudyPlanMedia)
                    .Select(y => new StudyPlanYearDto
                    {
                        Id = y.Id,
                        YearName = y.YearName,
                        YearNumber = y.YearNumber,
                        Type = y.Type,
                        CreatedDate = y.CreatedAt,
                        AcademicMaterials = y.AcademicMaterials != null
                            ? y.AcademicMaterials.Where(am => !am.IsDeleted).Select(am => new AcademicMaterialDto
                            {
                                Id = am.Id,
                                Name = am.Name,
                                Code = am.Code,
                                Semester = am.Semester,
                                Type = am.Type,
                                CreditHours = am.CreditHours
                            }).ToList()
                            : new List<AcademicMaterialDto>(),
                        Sections = y.Sections != null
                            ? y.Sections.Where(s => !s.IsDeleted).Select(s => new StudyPlanSectionDto
                            {
                                Id = s.Id,
                                Name = s.Name,
                                Code = s.Code,
                                CreditHours = s.CreditHours,
                                AcademicMaterials = s.AcademicMaterials != null
                                    ? s.AcademicMaterials.Where(am => !am.IsDeleted).Select(am => new AcademicMaterialDto
                                    {
                                        Id = am.Id,
                                        Name = am.Name,
                                        Code = am.Code,
                                        Semester = am.Semester,
                                        Type = am.Type,
                                        CreditHours = am.CreditHours
                                    }).ToList()
                                    : new List<AcademicMaterialDto>()
                            }).ToList()
                            : new List<StudyPlanSectionDto>(),
                        Media = y.StudyPlanMedia != null
                            ? y.StudyPlanMedia.Where(m => !m.IsDeleted).Select(m => new StudyPlanMediaDto
                            {
                                Id = m.Id,
                                MediaType = m.MediaType,
                                MediaLink = m.MediaLink,
                                VisitLink = m.VisitLink
                            }).ToList()
                            : new List<StudyPlanMediaDto>()
                    })
                    .AsNoTracking()
                    .ToListAsync();

                institute.StudyPlanYears = studyPlanYears;

                // Load specializations
                var specializations = await _context.Specializations
                    .Where(s => s.FacultyId == id && !s.IsDeleted)
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

                institute.SpecializationList = specializations;

                // Load job opportunities
                var jobOpportunities = await _context.JobOpportunities
                    .Where(j => j.FacultyId == id && !j.IsDeleted)
                    .Select(j => new JobOpportunityDto
                    {
                        Id = j.Id,
                        Name = j.Name
                    })
                    .AsNoTracking()
                    .ToListAsync();

                institute.JobOpportunities = jobOpportunities;

                // Calculate statistics
                var response = new
                {
                    Success = true,
                    Message = "تم جلب المعهد بنجاح",
                    Data = new
                    {
                        Institute = institute,
                        Statistics = new
                        {
                            TotalYears = institute.StudyPlanYears.Count,
                            TotalSpecializations = institute.SpecializationList.Count,
                            TotalMaterials = institute.StudyPlanYears.Sum(y =>
                                y.AcademicMaterials.Count + y.Sections.Sum(s => s.AcademicMaterials.Count)),
                            TotalHousingOptions = institute.HousingOptions.Count,
                            AcceptanceStatus = institute.RequireAcceptanceTests ?
                                "تتطلب اختبارات قبول" : "لا تتطلب اختبارات قبول",
                            HasHousing = institute.HousingOptions.Any()
                        },
                        StudyPlanSummary = new
                        {
                            GeneralYears = institute.StudyPlanYears.Count(y => y.Type == "General"),
                            SpecializedYears = institute.StudyPlanYears.Count(y => y.Type == "Specialized"),
                            TotalSections = institute.StudyPlanYears.Sum(y => y.Sections.Count),
                            TotalCreditHours = institute.StudyPlanYears
                                .SelectMany(y => y.Sections)
                                .Where(s => s.CreditHours.HasValue)
                                .Sum(s => s.CreditHours.Value)
                        },
                        HousingSummary = new
                        {
                            Available = institute.HousingOptions.Any(),
                            OptionsCount = institute.HousingOptions.Count,
                            Options = institute.HousingOptions.Select(h => new
                            {
                                h.Name,
                                h.PhoneNumber
                            }).ToList()
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
                    Message = "حدث خطأ أثناء جلب المعهد",
                    Error = ex.Message
                });
            }
        }
    }
}