using BawabaUNI.Models.Data;
using BawabaUNI.Models.DTOs;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static BawabaUNI.Controllers.User.FacultyController;

namespace BawabaUNI.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatbotController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ChatbotController(AppDbContext context)
        {
            _context = context;
        }

        #region Faculty Endpoints for Chatbot

        // GET: api/chatbot/faculties/distinct-names
        [HttpGet("faculties/distinct-names")]
        public async Task<ActionResult<IEnumerable<FacultyNameDto>>> GetDistinctFacultyNames()
        {
            var faculties = await _context.Faculties
                .Where(f => !f.IsDeleted && f.UniversityId != null )
                .Select(f => new
                {
                    f.NameArabic
                })
                .Distinct()
                .OrderBy(f => f.NameArabic)
                .ToListAsync();

            var result = faculties.Select(f => new FacultyNameDto
            {
                NameArabic = f.NameArabic
            }).ToList();

            return Ok(result);
        }

        // GET: api/chatbot/faculties/search-by-name?name=engineering
        [HttpGet("universities/by-faculty-name")]
        public async Task<ActionResult<IEnumerable<UniversityBasicDto>>> GetUniversitiesByFacultyName([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { Message = "يرجى إدخال اسم الكلية" });
            }

            // Search in both Arabic and English names
            var universities = await _context.Faculties
                .Where(f => !f.IsDeleted && f.UniversityId != null &&
                           (f.NameArabic.Contains(name) || f.NameEnglish.Contains(name)))
                .Select(f => new
                {
                    f.University.Id,
                    f.University.NameArabic,
                    f.University.NameEnglish,
                    FacultyNameArabic = f.NameArabic,
                    FacultyNameEnglish = f.NameEnglish
                })
                .Distinct()
                .OrderBy(u => u.NameArabic)
                .ToListAsync();

            if (!universities.Any())
            {
                return NotFound(new { Message = "لم يتم العثور على جامعات تحتوي على هذه الكلية" });
            }

            var result = universities.Select(u => new UniversityBasicDto
            {
                Id = u.Id,
                NameArabic = u.NameArabic,
                NameEnglish = u.NameEnglish,
                FacultyNameArabic = u.FacultyNameArabic,
                FacultyNameEnglish = u.FacultyNameEnglish
            }).ToList();

            return Ok(result);
        }

        // POST: api/chatbot/faculties/compare
        [HttpPost("faculties/compare")]
        public async Task<ActionResult<FacultyComparisonDto>> CompareFaculties([FromBody] CompareFacultiesRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(request.FacultyName))
            {
                return BadRequest(new { Message = "يرجى إدخال اسم الكلية" });
            }

            if (request.UniversityId1 <= 0 || request.UniversityId2 <= 0)
            {
                return BadRequest(new { Message = "معرفات الجامعات غير صالحة" });
            }

            if (request.UniversityId1 == request.UniversityId2)
            {
                return BadRequest(new { Message = "يرجى اختيار جامعتين مختلفتين للمقارنة" });
            }

            // Get faculty from first university
            var faculty1 = await _context.Faculties
                .Include(f => f.University)
                .Include(f => f.SpecializationList.Where(s => !s.IsDeleted))
                .Include(f => f.JobOpportunities.Where(j => !j.IsDeleted))
                .Include(f => f.StudyPlanYears.Where(sp => !sp.IsDeleted))
                    .ThenInclude(sp => sp.Sections.Where(s => !s.IsDeleted))
                        .ThenInclude(s => s.AcademicMaterials.Where(am => !am.IsDeleted))
                .Include(f => f.StudyPlanYears.Where(sp => !sp.IsDeleted))
                    .ThenInclude(sp => sp.AcademicMaterials.Where(am => !am.IsDeleted))
                .FirstOrDefaultAsync(f => !f.IsDeleted &&
                                         f.UniversityId == request.UniversityId1 &&
                                         (f.NameArabic.Contains(request.FacultyName) ||
                                          f.NameEnglish.Contains(request.FacultyName)));

            // Get faculty from second university
            var faculty2 = await _context.Faculties
                .Include(f => f.University)
                .Include(f => f.SpecializationList.Where(s => !s.IsDeleted))
                .Include(f => f.JobOpportunities.Where(j => !j.IsDeleted))
                .Include(f => f.StudyPlanYears.Where(sp => !sp.IsDeleted))
                    .ThenInclude(sp => sp.Sections.Where(s => !s.IsDeleted))
                        .ThenInclude(s => s.AcademicMaterials.Where(am => !am.IsDeleted))
                .Include(f => f.StudyPlanYears.Where(sp => !sp.IsDeleted))
                    .ThenInclude(sp => sp.AcademicMaterials.Where(am => !am.IsDeleted))
                .FirstOrDefaultAsync(f => !f.IsDeleted &&
                                         f.UniversityId == request.UniversityId2 &&
                                         (f.NameArabic.Contains(request.FacultyName) ||
                                          f.NameEnglish.Contains(request.FacultyName)));

            if (faculty1 == null && faculty2 == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الكلية في أي من الجامعتين" });
            }

            var comparisonResult = new FacultyComparisonDto
            {
                FacultyName = request.FacultyName,
                University1 = faculty1 != null ? MapFacultyToComparisonDetail(faculty1) : null,
                University2 = faculty2 != null ? MapFacultyToComparisonDetail(faculty2) : null,
                Message = GenerateComparisonMessage(faculty1, faculty2)
            };

            return Ok(comparisonResult);
        }

        // GET: api/chatbot/faculties/{facultyId}/details
        [HttpGet("faculties/{facultyId}/details")]
        public async Task<ActionResult<FacultyDetailsDto>> GetFacultyDetails(int facultyId)
        {
            var faculty = await _context.Faculties
                .Include(f => f.University)
                .Include(f => f.SpecializationList.Where(s => !s.IsDeleted))
                .Include(f => f.JobOpportunities.Where(j => !j.IsDeleted))
                .Include(f => f.StudyPlanYears.Where(sp => !sp.IsDeleted))
                    .ThenInclude(sp => sp.Sections.Where(s => !s.IsDeleted))
                        .ThenInclude(s => s.AcademicMaterials.Where(am => !am.IsDeleted))
                .Include(f => f.StudyPlanYears.Where(sp => !sp.IsDeleted))
                    .ThenInclude(sp => sp.AcademicMaterials.Where(am => !am.IsDeleted))
                .FirstOrDefaultAsync(f => f.Id == facultyId && !f.IsDeleted);

            if (faculty == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الكلية" });
            }

            var result = new FacultyDetailsDto
            {
                Id = faculty.Id,
                NameArabic = faculty.NameArabic,
                NameEnglish = faculty.NameEnglish,
                Description = faculty.Description,
                UniversityNameArabic = faculty.University.NameArabic,
                UniversityNameEnglish = faculty.University.NameEnglish,
                StudentsNumber = faculty.StudentsNumber,
                DurationOfStudy = faculty.DurationOfStudy,
                ProgramsNumber = faculty.ProgramsNumber,
                Rank = faculty.Rank,
                RequireAcceptanceTests = faculty.RequireAcceptanceTests,
                Expenses = faculty.Expenses,
                Coordination = faculty.Coordination,
                GroupLink = faculty.GroupLink,
                Specializations = faculty.SpecializationList.Select(s => new SpecializationBasicDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    YearsNumber = s.YearsNumber,
                    Description = s.Description,
                    AcademicQualification = s.AcademicQualification
                }).ToList(),
                JobOpportunities = faculty.JobOpportunities.Select(j => new JobOpportunityBasicDto
                {
                    Id = j.Id,
                    Name = j.Name
                }).ToList(),
                StudyPlanYears = faculty.StudyPlanYears.OrderBy(sp => sp.YearNumber).Select(sp => new StudyPlanYearBasicDto
                {
                    Id = sp.Id,
                    YearName = sp.YearName,
                    YearNumber = sp.YearNumber,
                    Type = sp.Type,
                    Sections = sp.Sections.Select(s => new StudyPlanSectionBasicDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Code = s.Code,
                        CreditHours = s.CreditHours,
                        AcademicMaterials = s.AcademicMaterials.Select(am => new AcademicMaterialBasicDto
                        {
                            Id = am.Id,
                            Name = am.Name,
                            Code = am.Code,
                            Semester = am.Semester,
                            Type = am.Type,
                            CreditHours = am.CreditHours
                        }).ToList()
                    }).ToList(),
                    GeneralMaterials = sp.AcademicMaterials.Where(am => am.StudyPlanSectionId == null).Select(am => new AcademicMaterialBasicDto
                    {
                        Id = am.Id,
                        Name = am.Name,
                        Code = am.Code,
                        Semester = am.Semester,
                        Type = am.Type,
                        CreditHours = am.CreditHours
                    }).ToList()
                }).ToList()
            };

            return Ok(result);
        }

        #endregion

        #region Helper Methods

        private FacultyComparisonDetailDto MapFacultyToComparisonDetail(Faculty faculty)
        {
            return new FacultyComparisonDetailDto
            {
                FacultyId = faculty.Id,
                UniversityId = faculty.UniversityId ?? 0,
                UniversityNameArabic = faculty.University.NameArabic,
                UniversityNameEnglish = faculty.University.NameEnglish,
                NameArabic = faculty.NameArabic,
                NameEnglish = faculty.NameEnglish,
                Description = faculty.Description,
                StudentsNumber = faculty.StudentsNumber,
                DurationOfStudy = faculty.DurationOfStudy,
                ProgramsNumber = faculty.ProgramsNumber,
                Rank = faculty.Rank,
                RequireAcceptanceTests = faculty.RequireAcceptanceTests,
                Expenses = faculty.Expenses,
                Coordination = faculty.Coordination,
                GroupLink = faculty.GroupLink,
                SpecializationsCount = faculty.SpecializationList?.Count ?? 0,
                JobOpportunitiesCount = faculty.JobOpportunities?.Count ?? 0,
                StudyPlanYearsCount = faculty.StudyPlanYears?.Count ?? 0,
                TotalAcademicMaterials = CalculateTotalAcademicMaterials(faculty)
            };
        }

        private int CalculateTotalAcademicMaterials(Faculty faculty)
        {
            if (faculty.StudyPlanYears == null) return 0;

            return faculty.StudyPlanYears.Sum(sp =>
                (sp.AcademicMaterials?.Count ?? 0) +
                (sp.Sections?.Sum(s => s.AcademicMaterials?.Count ?? 0) ?? 0));
        }

        private string GenerateComparisonMessage(Faculty faculty1, Faculty faculty2)
        {
            if (faculty1 == null && faculty2 == null)
                return "الكلية غير موجودة في أي من الجامعتين";

            if (faculty1 == null)
                return $"الكلية موجودة فقط في {faculty2.University.NameArabic}";

            if (faculty2 == null)
                return $"الكلية موجودة فقط في {faculty1.University.NameArabic}";

            var differences = new List<string>();

            // Compare key metrics
            if (faculty1.Expenses != faculty2.Expenses)
                differences.Add($"المصروفات: {faculty1.Expenses} vs {faculty2.Expenses}");

            if (faculty1.Coordination != faculty2.Coordination)
                differences.Add($"التنسيق: {faculty1.Coordination} vs {faculty2.Coordination}");

            if (faculty1.StudentsNumber != faculty2.StudentsNumber)
                differences.Add($"عدد الطلاب: {faculty1.StudentsNumber} vs {faculty2.StudentsNumber}");

            if (faculty1.ProgramsNumber != faculty2.ProgramsNumber)
                differences.Add($"عدد البرامج: {faculty1.ProgramsNumber} vs {faculty2.ProgramsNumber}");

            if (faculty1.RequireAcceptanceTests != faculty2.RequireAcceptanceTests)
                differences.Add($"اختبارات قبول: {(faculty1.RequireAcceptanceTests ? "مطلوبة" : "غير مطلوبة")} vs {(faculty2.RequireAcceptanceTests ? "مطلوبة" : "غير مطلوبة")}");

            if (differences.Any())
            {
                return $"الاختلافات بين {faculty1.University.NameArabic} و {faculty2.University.NameArabic}: " + string.Join(" | ", differences);
            }

            return $"الكلية متشابهة في كل من {faculty1.University.NameArabic} و {faculty2.University.NameArabic} من حيث البيانات الأساسية";
        }
        private string GenerateCoordinationSummary(int coordination, int totalFaculties, int totalUniversities)
        {
            if (totalFaculties == 0)
                return $"لا توجد كليات تطابق تنسيق {coordination}";

            if (totalFaculties == 1)
                return $"يوجد كلية واحدة تطابق تنسيق {coordination} في {totalUniversities} جامعات";

            return $"يوجد {totalFaculties} كلية تطابق تنسيق {coordination} موزعة على {totalUniversities} جامعات";
        }

        private string GenerateStatsMessage(int coordination, int matchingFaculties, int totalFaculties)
        {
            var percentage = totalFaculties > 0
                ? Math.Round((double)matchingFaculties / totalFaculties * 100, 2)
                : 0;

            if (percentage >= 70)
                return $"تنسيقك {coordination} يؤهلك لـ {matchingFaculties} كلية من أصل {totalFaculties} بنسبة {percentage}%. فرصك ممتازة! 🎉";

            if (percentage >= 40)
                return $"تنسيقك {coordination} يؤهلك لـ {matchingFaculties} كلية من أصل {totalFaculties} بنسبة {percentage}%. فرصك جيدة 👍";

            if (percentage >= 10)
                return $"تنسيقك {coordination} يؤهلك لـ {matchingFaculties} كلية من أصل {totalFaculties} بنسبة {percentage}%. لا تزال لديك بعض الفرص 💪";

            return $"تنسيقك {coordination} يؤهلك لـ {matchingFaculties} كلية فقط من أصل {totalFaculties}. قد تحتاج للنظر في خيارات أخرى 🤔";
        }


        #endregion
        // GET: api/chatbot/faculties/match-coordination?coordination=85&universityType=Public
        [HttpGet("faculties/match-coordination")]
        public async Task<ActionResult<CoordinationMatchResponseDto>> GetFacultiesMatchingCoordination(
            [FromQuery] int coordination,
            [FromQuery] string? universityType = null)
        {
            if (coordination <= 0)
            {
                return BadRequest(new { Message = "يرجى إدخاد تنسيق صحيح" });
            }

            // Build the query
            var query = _context.Faculties
                .Include(f => f.University)
                .Where(f => !f.IsDeleted && f.Coordination <= coordination);

            // Filter by university type if provided
            if (!string.IsNullOrWhiteSpace(universityType))
            {
                query = query.Where(f => f.University.Type == universityType);
            }

            var faculties = await query
                .OrderBy(f => f.University.NameArabic)
                .ThenBy(f => f.NameArabic)
                .Select(f => new FacultyMatchDto
                {
                    FacultyId = f.Id,
                    FacultyNameArabic = f.NameArabic,
                    FacultyNameEnglish = f.NameEnglish,
                    Coordination = f.Coordination,
                    Expenses = f.Expenses,
                    DurationOfStudy = f.DurationOfStudy,
                    ProgramsNumber = f.ProgramsNumber,
                    RequireAcceptanceTests = f.RequireAcceptanceTests,
                    UniversityId = f.University.Id,
                    UniversityNameArabic = f.University.NameArabic,
                    UniversityNameEnglish = f.University.NameEnglish,
                    UniversityType = f.University.Type,
                    UniversityLogo = f.University.UniversityImage != null
                        ? $"{Request.Scheme}://{Request.Host}{f.University.UniversityImage}"
                        : null
                })
                .ToListAsync();

            if (!faculties.Any())
            {
                return NotFound(new { Message = "لا توجد كليات تطابق التنسيق المدخل" });
            }

            // Group by university and calculate statistics
            var universitiesWithCounts = faculties
                .GroupBy(f => new { f.UniversityId, f.UniversityNameArabic, f.UniversityNameEnglish, f.UniversityType, f.UniversityLogo })
                .Select(g => new UniversityCoordinationMatchDto
                {
                    UniversityId = g.Key.UniversityId,
                    UniversityNameArabic = g.Key.UniversityNameArabic,
                    UniversityNameEnglish = g.Key.UniversityNameEnglish,
                    UniversityType = g.Key.UniversityType,
                    UniversityLogo = g.Key.UniversityLogo,
                    MatchingFacultiesCount = g.Count(),
                    MinCoordination = g.Min(f => f.Coordination),
                    MaxCoordination = g.Max(f => f.Coordination),
                    AverageCoordination = (int)Math.Round(g.Average(f => f.Coordination)),
                    Faculties = g.OrderBy(f => f.Coordination).ThenBy(f => f.FacultyNameArabic).ToList()
                })
                .OrderByDescending(g => g.MatchingFacultiesCount)
                .ThenBy(g => g.UniversityNameArabic)
                .ToList();

            var response = new CoordinationMatchResponseDto
            {
                UserCoordination = coordination,
                UniversityType = universityType,
                TotalMatchingFaculties = faculties.Count,
                TotalUniversities = universitiesWithCounts.Count,
                Universities = universitiesWithCounts,
                Summary = GenerateCoordinationSummary(coordination, faculties.Count, universitiesWithCounts.Count)
            };

            return Ok(response);
        }

        // GET: api/chatbot/faculties/match-coordination/stats?coordination=85
        [HttpGet("faculties/match-coordination/stats")]
        public async Task<ActionResult<CoordinationStatsDto>> GetCoordinationStatistics([FromQuery] int coordination)
        {
            if (coordination <= 0)
            {
                return BadRequest(new { Message = "يرجى إدخال تنسيق صحيح" });
            }

            var stats = await _context.Faculties
                .Include(f => f.University)
                .Where(f => !f.IsDeleted)
                .GroupBy(f => f.University.Type)
                .Select(g => new CoordinationStatsByTypeDto
                {
                    UniversityType = g.Key,
                    TotalFaculties = g.Count(),
                    FacultiesMatchingCoordination = g.Count(f => f.Coordination <= coordination),
                    Percentage = g.Count() > 0
                        ? Math.Round((double)g.Count(f => f.Coordination <= coordination) / g.Count() * 100, 2)
                        : 0,
                    MinCoordination = g.Min(f => f.Coordination),
                    MaxCoordination = g.Max(f => f.Coordination),
                    AverageCoordination = (int)Math.Round(g.Average(f => f.Coordination)),
                    UniversitiesCount = g.Select(f => f.UniversityId).Distinct().Count()
                })
                .ToListAsync();

            var totalFaculties = stats.Sum(s => s.TotalFaculties);
            var totalMatching = stats.Sum(s => s.FacultiesMatchingCoordination);

            var result = new CoordinationStatsDto
            {
                UserCoordination = coordination,
                TotalFaculties = totalFaculties,
                TotalMatchingFaculties = totalMatching,
                OverallPercentage = totalFaculties > 0
                    ? Math.Round((double)totalMatching / totalFaculties * 100, 2)
                    : 0,
                StatsByType = stats,
                Message = GenerateStatsMessage(coordination, totalMatching, totalFaculties)
            };

            return Ok(result);
        }

        // GET: api/chatbot/universities/types
        [HttpGet("universities/types")]
        public async Task<ActionResult<IEnumerable<string>>> GetUniversityTypes()
        {
            var types = await _context.Universities
                .Where(u => !u.IsDeleted && u.Type != null)
                .Select(u => u.Type)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            return Ok(types);
        }
       
    }
    public class FacultyNameDto
    {
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
    }
    public class UniversityBasicDto
    {
        public int Id { get; set; }
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
        public string FacultyNameArabic { get; set; }
        public string FacultyNameEnglish { get; set; }
    }
    public class CompareFacultiesRequestDto
    {
        public string FacultyName { get; set; }
        public int UniversityId1 { get; set; }
        public int UniversityId2 { get; set; }
    }

    public class FacultyComparisonDto
    {
        public string FacultyName { get; set; }
        public FacultyComparisonDetailDto University1 { get; set; }
        public FacultyComparisonDetailDto University2 { get; set; }
        public string Message { get; set; }
    }

    public class FacultyComparisonDetailDto
    {
        public int FacultyId { get; set; }
        public int UniversityId { get; set; }
        public string UniversityNameArabic { get; set; }
        public string UniversityNameEnglish { get; set; }
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
        public string Description { get; set; }
        public int? StudentsNumber { get; set; }
        public string DurationOfStudy { get; set; }
        public int? ProgramsNumber { get; set; }
        public int? Rank { get; set; }
        public bool RequireAcceptanceTests { get; set; }
        public decimal Expenses { get; set; }
        public decimal Coordination { get; set; }
        public string? GroupLink { get; set; }
        public int SpecializationsCount { get; set; }
        public int JobOpportunitiesCount { get; set; }
        public int StudyPlanYearsCount { get; set; }
        public int TotalAcademicMaterials { get; set; }
    }
    public class FacultyDetailsDto
    {
        public int Id { get; set; }
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
        public string Description { get; set; }
        public string UniversityNameArabic { get; set; }
        public string UniversityNameEnglish { get; set; }
        public int? StudentsNumber { get; set; }
        public string DurationOfStudy { get; set; }
        public int? ProgramsNumber { get; set; }
        public int? Rank { get; set; }
        public bool RequireAcceptanceTests { get; set; }
        public decimal Expenses { get; set; }
        public decimal Coordination { get; set; }
        public string? GroupLink { get; set; }
        public List<SpecializationBasicDto> Specializations { get; set; }
        public List<JobOpportunityBasicDto> JobOpportunities { get; set; }
        public List<StudyPlanYearBasicDto> StudyPlanYears { get; set; }
    }

    public class SpecializationBasicDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int YearsNumber { get; set; }
        public string Description { get; set; }
        public string AcademicQualification { get; set; }
    }

    public class JobOpportunityBasicDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class StudyPlanYearBasicDto
    {
        public int Id { get; set; }
        public string YearName { get; set; }
        public int YearNumber { get; set; }
        public string Type { get; set; }
        public List<StudyPlanSectionBasicDto> Sections { get; set; }
        public List<AcademicMaterialBasicDto> GeneralMaterials { get; set; }
    }

    public class StudyPlanSectionBasicDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public int? CreditHours { get; set; }
        public List<AcademicMaterialBasicDto> AcademicMaterials { get; set; }
    }

    public class AcademicMaterialBasicDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public int Semester { get; set; }
        public string Type { get; set; }
        public int CreditHours { get; set; }
    }
    public class FacultyMatchDto
    {
        public int FacultyId { get; set; }
        public string FacultyNameArabic { get; set; }
        public string FacultyNameEnglish { get; set; }
        public decimal Coordination { get; set; }
        public decimal Expenses { get; set; }
        public string DurationOfStudy { get; set; }
        public int? ProgramsNumber { get; set; }
        public bool RequireAcceptanceTests { get; set; }
        public int UniversityId { get; set; }
        public string UniversityNameArabic { get; set; }
        public string UniversityNameEnglish { get; set; }
        public string UniversityType { get; set; }
        public string UniversityLogo { get; set; }
    }

    public class UniversityCoordinationMatchDto
    {
        public int UniversityId { get; set; }
        public string UniversityNameArabic { get; set; }
        public string UniversityNameEnglish { get; set; }
        public string UniversityType { get; set; }
        public string UniversityLogo { get; set; }
        public int MatchingFacultiesCount { get; set; }
        public decimal MinCoordination { get; set; }
        public decimal MaxCoordination { get; set; }
        public decimal AverageCoordination { get; set; }
        public List<FacultyMatchDto> Faculties { get; set; }
    }

    public class CoordinationMatchResponseDto
    {
        public int UserCoordination { get; set; }
        public string UniversityType { get; set; }
        public int TotalMatchingFaculties { get; set; }
        public int TotalUniversities { get; set; }
        public List<UniversityCoordinationMatchDto> Universities { get; set; }
        public string Summary { get; set; }
    }

    public class CoordinationStatsByTypeDto
    {
        public string UniversityType { get; set; }
        public int TotalFaculties { get; set; }
        public int FacultiesMatchingCoordination { get; set; }
        public double Percentage { get; set; }
        public decimal MinCoordination { get; set; }
        public decimal MaxCoordination { get; set; }
        public decimal AverageCoordination { get; set; }
        public int UniversitiesCount { get; set; }
    }

    public class CoordinationStatsDto
    {
        public int UserCoordination { get; set; }
        public int TotalFaculties { get; set; }
        public int TotalMatchingFaculties { get; set; }
        public double OverallPercentage { get; set; }
        public List<CoordinationStatsByTypeDto> StatsByType { get; set; }
        public string Message { get; set; }
    }
}