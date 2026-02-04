using BawabaUNI.Models.Data;
using BawabaUNI.Models.DTOs;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BawabaUNI.Controllers
{
    [ApiController]
    [Route("api/Admin/[controller]")]
    public class FacultiesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public FacultiesController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpPost("add/{universityId}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AddFaculty(int universityId, [FromForm] FacultyFormModel model)
        {
            try
            {
                Console.WriteLine("=== بدء إضافة كلية ===");

                // 1. التحقق الأساسي
                if (string.IsNullOrEmpty(model.NameArabic) || string.IsNullOrEmpty(model.Description))
                {
                    return BadRequest(new { success = false, message = "البيانات الأساسية مطلوبة" });
                }

                // 2. التحقق من الجامعة
                var universityExists = await _context.Universities
                    .AnyAsync(u => u.Id == universityId && !u.IsDeleted);
                if (!universityExists)
                    return BadRequest(new { success = false, message = "الجامعة غير موجودة" });

                // 3. إنشاء الكلية
                var faculty = new Faculty
                {
                    NameArabic = model.NameArabic,
                    NameEnglish = model.NameEnglish ?? model.NameArabic,
                    Description = model.Description,
                    StudentsNumber = model.StudentsNumber,
                    DurationOfStudy = model.DurationOfStudy ?? "4 سنوات",
                    ProgramsNumber = model.ProgramsNumber,
                    Specializations = model.Specializations ?? "لا يوجد",
                    RequireAcceptanceTests = model.RequireAcceptanceTests,
                    UniversityId = universityId
                };

                _context.Faculties.Add(faculty);
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ تم إنشاء الكلية ID: {faculty.Id}");

                var facultyId = faculty.Id;

                // 4. إضافة التخصصات
                var specCount = 0;
                if (model.SpecializationNames != null)
                {
                    for (int i = 0; i < model.SpecializationNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(model.SpecializationNames[i])) continue;

                        var spec = new Specialization
                        {
                            Name = model.SpecializationNames[i],
                            YearsNumber = i < model.SpecializationYearsNumbers.Count ?
                                model.SpecializationYearsNumbers[i] : 4,
                            Description = i < model.SpecializationDescriptions.Count ?
                                model.SpecializationDescriptions[i] : "",
                            AcademicQualification = "",
                            FacultyId = facultyId
                        };

                        _context.Specializations.Add(spec);
                        specCount++;
                    }

                    if (specCount > 0)
                    {
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"✅ تم إضافة {specCount} تخصص");
                    }
                }

                // 5. إضافة خطة الدراسة (مع الكود اليدوي)
                var yearCount = 0;
                var semesterCount = 0;
                var materialCount = 0;

                if (model.YearNames != null && model.YearNames.Count > 0)
                {
                    for (int yearIndex = 0; yearIndex < model.YearNames.Count; yearIndex++)
                    {
                        if (string.IsNullOrEmpty(model.YearNames[yearIndex])) continue;

                        // إنشاء السنة الدراسية
                        var studyPlanYear = new StudyPlanYear
                        {
                            YearName = model.YearNames[yearIndex],
                            YearNumber = yearIndex + 1,
                            Type = (yearIndex < model.YearHasSpecialization.Count &&
                                   model.YearHasSpecialization[yearIndex]) ? "Specialized" : "General",
                            FacultyId = facultyId
                        };

                        _context.StudyPlanYears.Add(studyPlanYear);
                        await _context.SaveChangesAsync(); // للحصول على ID

                        var studyPlanYearId = studyPlanYear.Id;
                        yearCount++;
                        Console.WriteLine($"✅ تم إنشاء السنة: {studyPlanYear.YearName} (ID: {studyPlanYearId})");

                        // 5.1 إضافة الوسائط لهذه السنة
                        if (model.MediaTypes != null && model.MediaYearIndices != null)
                        {
                            for (int mediaIndex = 0; mediaIndex < model.MediaYearIndices.Count; mediaIndex++)
                            {
                                if (model.MediaYearIndices[mediaIndex] == yearIndex &&
                                    mediaIndex < model.MediaTypes.Count &&
                                    !string.IsNullOrEmpty(model.MediaTypes[mediaIndex]))
                                {
                                    string mediaLink = "";

                                    if (model.MediaFiles != null && mediaIndex < model.MediaFiles.Count &&
                                        model.MediaFiles[mediaIndex] != null && model.MediaFiles[mediaIndex].Length > 0)
                                    {
                                        mediaLink = await SaveFile(model.MediaFiles[mediaIndex], "studyplan-media");
                                        Console.WriteLine($"📁 تم رفع ملف: {mediaLink}");
                                    }

                                    if (!string.IsNullOrEmpty(mediaLink))
                                    {
                                        var media = new StudyPlanMedia
                                        {
                                            MediaType = model.MediaTypes[mediaIndex],
                                            MediaLink = mediaLink,
                                            VisitLink = "",
                                            StudyPlanYearId = studyPlanYearId
                                        };

                                        _context.StudyPlanMedia.Add(media);
                                        Console.WriteLine($"✅ تم إضافة وسائط للسنة {yearIndex + 1}");
                                    }
                                }
                            }
                        }

                        // 5.2 إضافة الفصول الدراسية لهذه السنة
                        if (model.SemesterNames != null && model.SemesterYearIndices != null)
                        {
                            for (int semIndex = 0; semIndex < model.SemesterYearIndices.Count; semIndex++)
                            {
                                if (model.SemesterYearIndices[semIndex] == yearIndex &&
                                    semIndex < model.SemesterNames.Count &&
                                    !string.IsNullOrEmpty(model.SemesterNames[semIndex]))
                                {
                                    // هذا الفصل ينتمي لهذه السنة
                                    semesterCount++;

                                    // هل هذا الفصل به أقسام؟
                                    bool hasSections = false;

                                    // 5.3 إضافة أقسام هذا الفصل (مع الكود اليدوي)
                                    if (model.SectionNames != null && model.SectionSemesterIndices != null)
                                    {
                                        for (int secIndex = 0; secIndex < model.SectionSemesterIndices.Count; secIndex++)
                                        {
                                            if (model.SectionSemesterIndices[secIndex] == semIndex &&
                                                secIndex < model.SectionNames.Count &&
                                                !string.IsNullOrEmpty(model.SectionNames[secIndex]))
                                            {
                                                hasSections = true;

                                                // الحصول على الكود اليدوي أو استخدام كود افتراضي
                                                string sectionCode = secIndex < model.SectionCodes.Count &&
                                                                   !string.IsNullOrEmpty(model.SectionCodes[secIndex])
                                                    ? model.SectionCodes[secIndex]
                                                    : $"SEC-{yearIndex + 1}-{secIndex + 1}";

                                                // إنشاء القسم
                                                var section = new StudyPlanSection
                                                {
                                                    Name = model.SectionNames[secIndex],
                                                    Code = sectionCode, // ⬅️ الكود اليدوي
                                                    StudyPlanYearId = studyPlanYearId
                                                };

                                                _context.StudyPlanSections.Add(section);
                                                await _context.SaveChangesAsync(); // للحصول على ID

                                                var sectionId = section.Id;
                                                Console.WriteLine($"✅ تم إنشاء قسم: {section.Name} (Code: {section.Code})");

                                                // 5.4 إضافة مواد هذا القسم (مع الكود اليدوي)
                                                if (model.SectionMaterialNames != null &&
                                                    model.SectionMaterialSectionIndices != null)
                                                {
                                                    for (int matIndex = 0; matIndex < model.SectionMaterialSectionIndices.Count; matIndex++)
                                                    {
                                                        if (model.SectionMaterialSectionIndices[matIndex] == secIndex &&
                                                            matIndex < model.SectionMaterialNames.Count &&
                                                            !string.IsNullOrEmpty(model.SectionMaterialNames[matIndex]))
                                                        {
                                                            // الحصول على الكود اليدوي أو استخدام كود افتراضي
                                                            string materialCode = matIndex < model.SectionMaterialCodes.Count &&
                                                                               !string.IsNullOrEmpty(model.SectionMaterialCodes[matIndex])
                                                                ? model.SectionMaterialCodes[matIndex]
                                                                : $"MAT-{yearIndex + 1}-{secIndex + 1}-{matIndex + 1}";

                                                            var material = new AcademicMaterial
                                                            {
                                                                Name = model.SectionMaterialNames[matIndex],
                                                                Code = materialCode, // ⬅️ الكود اليدوي
                                                                Semester = semIndex + 1,
                                                                Type = "Mandatory",
                                                                CreditHours = 3,
                                                                StudyPlanYearId = null,
                                                                StudyPlanSectionId = sectionId
                                                            };

                                                            _context.AcademicMaterials.Add(material);
                                                            materialCount++;
                                                            Console.WriteLine($"📚 تم إضافة مادة للقسم: {material.Name} (Code: {material.Code})");
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // 5.5 إذا كان الفصل بدون أقسام، إضافة المواد مباشرة (مع الكود اليدوي)
                                    if (!hasSections && model.SemesterMaterialNames != null &&
                                        model.SemesterMaterialSemesterIndices != null)
                                    {
                                        for (int matIndex = 0; matIndex < model.SemesterMaterialSemesterIndices.Count; matIndex++)
                                        {
                                            if (model.SemesterMaterialSemesterIndices[matIndex] == semIndex &&
                                                matIndex < model.SemesterMaterialNames.Count &&
                                                !string.IsNullOrEmpty(model.SemesterMaterialNames[matIndex]))
                                            {
                                                // الحصول على الكود اليدوي أو استخدام كود افتراضي
                                                string materialCode = matIndex < model.SemesterMaterialCodes.Count &&
                                                                   !string.IsNullOrEmpty(model.SemesterMaterialCodes[matIndex])
                                                    ? model.SemesterMaterialCodes[matIndex]
                                                    : $"MAT-{yearIndex + 1}-{semIndex + 1}-{matIndex + 1}";

                                                var material = new AcademicMaterial
                                                {
                                                    Name = model.SemesterMaterialNames[matIndex],
                                                    Code = materialCode, // ⬅️ الكود اليدوي
                                                    Semester = semIndex + 1,
                                                    Type = "Mandatory",
                                                    CreditHours = 3,
                                                    StudyPlanYearId = studyPlanYearId,
                                                    StudyPlanSectionId = null
                                                };

                                                _context.AcademicMaterials.Add(material);
                                                materialCount++;
                                                Console.WriteLine($"📚 تم إضافة مادة للفصل: {material.Name} (Code: {material.Code})");
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // حفظ البيانات بعد كل سنة
                        await _context.SaveChangesAsync();
                    }
                }

                // 6. إضافة فرص العمل
                var jobCount = 0;
                if (model.JobOpportunityNames != null)
                {
                    for (int i = 0; i < model.JobOpportunityNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(model.JobOpportunityNames[i])) continue;

                        var job = new JobOpportunity
                        {
                            Name = model.JobOpportunityNames[i],
                            FacultyId = facultyId
                        };

                        _context.JobOpportunities.Add(job);
                        jobCount++;
                    }

                    if (jobCount > 0)
                    {
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"✅ تم إضافة {jobCount} فرصة عمل");
                    }
                }

                // الحفظ النهائي
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "تم إضافة الكلية بنجاح",
                    facultyId,
                    specializationCount = specCount,
                    yearCount,
                    semesterCount,
                    materialCount,
                    jobCount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطأ: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ غير متوقع",
                    error = ex.Message
                });
            }
        }
        private async Task<string> SaveFile(IFormFile file, string folder)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return null;

                // تحديد الصيغ المسموح بها
                var validExtensions = new[]
                {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".mp4", ".avi", ".mov", ".wmv", ".mp3", ".wav"
        };

                var extension = Path.GetExtension(file.FileName).ToLower();

                if (!validExtensions.Contains(extension))
                {
                    Console.WriteLine($"امتداد ملف غير صالح: {extension}");
                    return null;
                }

                // إنشاء مجلد التحميل إذا لم يكن موجوداً
                var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", folder);

                if (!Directory.Exists(uploadsPath))
                    Directory.CreateDirectory(uploadsPath);

                // إنشاء اسم فريد للملف
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsPath, fileName);

                // حفظ الملف على السيرفر
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                Console.WriteLine($"تم حفظ الملف: {fileName} في {filePath}");
                return $"/uploads/{folder}/{fileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطأ في حفظ الملف: {ex.Message}");
                return null;
            }
        }

        // 📌 GET: api/faculties/by-university/{universityId}
        [HttpGet("by-university/{universityId}")]
        public async Task<IActionResult> GetFacultiesByUniversity(int universityId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                // التحقق من وجود الجامعة
                var university = await _context.Universities
                    .FirstOrDefaultAsync(u => u.Id == universityId && !u.IsDeleted);

                if (university == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الجامعة غير موجودة"
                    });
                }

                // حساب التخطيط
                var totalCount = await _context.Faculties
                    .Where(f => f.UniversityId == universityId && !f.IsDeleted)
                    .CountAsync();

                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // جلب الكليات مع بيانات أساسية
                var faculties = await _context.Faculties
                    .Where(f => f.UniversityId == universityId && !f.IsDeleted)
                    .Include(f => f.SpecializationList.Where(s => !s.IsDeleted))
                    .Include(f => f.StudyPlanYears.Where(y => !y.IsDeleted))
                    .Include(f => f.JobOpportunities.Where(j => !j.IsDeleted))
                    .OrderBy(f => f.NameArabic)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(f => new
                    {
                        f.Id,
                        f.NameArabic,
                        f.NameEnglish,
                        f.Description,
                        f.StudentsNumber,
                        f.DurationOfStudy,
                        f.ProgramsNumber,
                        f.Specializations,
                        f.RequireAcceptanceTests,
                        f.UniversityId,

                        // إحصائيات
                        SpecializationsCount = f.SpecializationList.Count,
                        StudyYearsCount = f.StudyPlanYears.Count,
                        JobOpportunitiesCount = f.JobOpportunities.Count,

                        // بيانات إضافية مختصرة
                        FirstTwoSpecializations = f.SpecializationList
                            .Take(2)
                            .Select(s => new { s.Id, s.Name })
                            .ToList(),

                        FirstThreeJobs = f.JobOpportunities
                            .Take(3)
                            .Select(j => new { j.Id, j.Name })
                            .ToList(),

                        f.CreatedAt,
                        f.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        University = new
                        {
                            university.Id,
                            university.NameArabic,
                            university.NameEnglish
                        },
                        Faculties = faculties,
                        Pagination = new
                        {
                            CurrentPage = page,
                            PageSize = pageSize,
                            TotalCount = totalCount,
                            TotalPages = totalPages,
                            HasPrevious = page > 1,
                            HasNext = page < totalPages
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء جلب البيانات"
                });
            }
        }

        // 📌 GET: api/faculties/university/{universityId}/search
        [HttpGet("university/{universityId}/search")]
        public async Task<IActionResult> SearchFacultiesInUniversity(
            int universityId,
            [FromQuery] string? search = null,
            [FromQuery] bool? hasSpecializations = null,
            [FromQuery] bool? requireAcceptanceTests = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // التحقق من وجود الجامعة
                var universityExists = await _context.Universities
                    .AnyAsync(u => u.Id == universityId && !u.IsDeleted);

                if (!universityExists)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الجامعة غير موجودة"
                    });
                }

                // بناء الاستعلام
                var query = _context.Faculties
                    .Where(f => f.UniversityId == universityId && !f.IsDeleted)
                    .Include(f => f.SpecializationList.Where(s => !s.IsDeleted))
                    .Include(f => f.StudyPlanYears.Where(y => !y.IsDeleted))
                    .AsQueryable();

                // تطبيق عوامل البحث
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(f =>
                        f.NameArabic.Contains(search) ||
                        f.NameEnglish.Contains(search) ||
                        f.Description.Contains(search));
                }

                if (hasSpecializations.HasValue)
                {
                    if (hasSpecializations.Value)
                    {
                        query = query.Where(f => f.SpecializationList.Any());
                    }
                    else
                    {
                        query = query.Where(f => !f.SpecializationList.Any());
                    }
                }

                if (requireAcceptanceTests.HasValue)
                {
                    query = query.Where(f => f.RequireAcceptanceTests == requireAcceptanceTests.Value);
                }

                // التصنيف
                switch (sortBy.ToLower())
                {
                    case "name":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(f => f.NameArabic)
                            : query.OrderBy(f => f.NameArabic);
                        break;

                    case "students":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(f => f.StudentsNumber)
                            : query.OrderBy(f => f.StudentsNumber);
                        break;

                    case "programs":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(f => f.ProgramsNumber)
                            : query.OrderBy(f => f.ProgramsNumber);
                        break;

                    default:
                        query = query.OrderBy(f => f.NameArabic);
                        break;
                }

                // حساب التخطيط
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // تطبيق التخطيط
                var faculties = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(f => new
                    {
                        f.Id,
                        f.NameArabic,
                        f.NameEnglish,
                        f.Description,
                        f.StudentsNumber,
                        f.DurationOfStudy,
                        f.ProgramsNumber,
                        f.Specializations,
                        f.RequireAcceptanceTests,

                        SpecializationsCount = f.SpecializationList.Count,
                        StudyYearsCount = f.StudyPlanYears.Count,

                        HasStudyPlan = f.StudyPlanYears.Any(),
                        HasJobs = f.JobOpportunities.Any(j => !j.IsDeleted),

                        f.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        UniversityId = universityId,
                        SearchQuery = search,
                        Filters = new
                        {
                            HasSpecializations = hasSpecializations,
                            RequireAcceptanceTests = requireAcceptanceTests
                        },
                        Sort = new
                        {
                            By = sortBy,
                            Order = sortOrder
                        },
                        Faculties = faculties,
                        Pagination = new
                        {
                            CurrentPage = page,
                            PageSize = pageSize,
                            TotalCount = totalCount,
                            TotalPages = totalPages,
                            HasPrevious = page > 1,
                            HasNext = page < totalPages
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء البحث"
                });
            }
        }

        // 📌 GET: api/faculties/university/{universityId}/stats
        [HttpGet("university/{universityId}/stats")]
        public async Task<IActionResult> GetUniversityFacultiesStats(int universityId)
        {
            try
            {
                // التحقق من وجود الجامعة
                var university = await _context.Universities
                    .FirstOrDefaultAsync(u => u.Id == universityId && !u.IsDeleted);

                if (university == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الجامعة غير موجودة"
                    });
                }

                // جلب الإحصائيات
                var stats = await _context.Faculties
                    .Where(f => f.UniversityId == universityId && !f.IsDeleted)
                    .GroupBy(f => 1) // تجميع جميع السجلات
                    .Select(g => new
                    {
                        TotalFaculties = g.Count(),
                        TotalStudents = g.Sum(f => f.StudentsNumber ?? 0),
                        TotalPrograms = g.Sum(f => f.ProgramsNumber ?? 0),
                        AverageDuration = g.Average(f =>
                            f.DurationOfStudy != null && f.DurationOfStudy.Contains("سنوات")
                            ? int.Parse(f.DurationOfStudy.Replace("سنوات", "").Trim())
                            : 4),

                        FacultiesWithAcceptanceTests = g.Count(f => f.RequireAcceptanceTests),
                        FacultiesWithoutAcceptanceTests = g.Count(f => !f.RequireAcceptanceTests),

                        // التخصصات
                        TotalSpecializations = g.SelectMany(f => f.SpecializationList.Where(s => !s.IsDeleted)).Count(),

                        // أكثر الكليات طلاباً
                        TopFacultiesByStudents = g
                            .Where(f => f.StudentsNumber.HasValue)
                            .OrderByDescending(f => f.StudentsNumber)
                            .Take(5)
                            .Select(f => new
                            {
                                f.Id,
                                f.NameArabic,
                                f.StudentsNumber
                            })
                            .ToList(),

                        // أحدث الكليات
                        LatestFaculties = g
                            .OrderByDescending(f => f.CreatedAt)
                            .Take(5)
                            .Select(f => new
                            {
                                f.Id,
                                f.NameArabic,
                                f.CreatedAt
                            })
                            .ToList()
                    })
                    .FirstOrDefaultAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        University = new
                        {
                            university.Id,
                            university.NameArabic,
                            university.NameEnglish
                        },
                        Statistics = stats 
                        //{
                        //    TotalFaculties = 0,
                        //    TotalStudents = 0,
                        //    TotalPrograms = 0,
                        //    AverageDuration = 0,
                        //    FacultiesWithAcceptanceTests = 0,
                        //    FacultiesWithoutAcceptanceTests = 0,
                        //    TotalSpecializations = 0,
                        //    TopFacultiesByStudents = new List<object>(),
                        //    LatestFaculties = new List<object>()
                        //}
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء جلب الإحصائيات"
                });
            }
        }

        // 📌 GET: api/faculties/university/{universityId}/complete
        [HttpGet("university/{universityId}/complete")]
        public async Task<IActionResult> GetUniversityFacultiesWithCompleteDetails(int universityId)
        {
            try
            {
                Console.WriteLine($"🔍 جلب كليات الجامعة ID: {universityId} مع التفاصيل الكاملة");

                // جلب الجامعة مع كل كلياتها وتفاصيلها
                var university = await _context.Universities
                    .Include(u => u.Faculties.Where(f => !f.IsDeleted))
                        .ThenInclude(f => f.SpecializationList.Where(s => !s.IsDeleted))
                    .Include(u => u.Faculties)
                        .ThenInclude(f => f.StudyPlanYears.Where(y => !y.IsDeleted))
                            .ThenInclude(y => y.AcademicMaterials.Where(m => !m.IsDeleted && m.StudyPlanSectionId == null))
                    .Include(u => u.Faculties)
                        .ThenInclude(f => f.StudyPlanYears.Where(y => !y.IsDeleted))
                            .ThenInclude(y => y.Sections.Where(s => !s.IsDeleted))
                                .ThenInclude(s => s.AcademicMaterials.Where(m => !m.IsDeleted))
                    .Include(u => u.Faculties)
                        .ThenInclude(f => f.StudyPlanYears.Where(y => !y.IsDeleted))
                            .ThenInclude(y => y.StudyPlanMedia.Where(m => !m.IsDeleted))
                    .Include(u => u.Faculties)
                        .ThenInclude(f => f.JobOpportunities.Where(j => !j.IsDeleted))
                    .FirstOrDefaultAsync(u => u.Id == universityId && !u.IsDeleted);

                if (university == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الجامعة غير موجودة"
                    });
                }

                Console.WriteLine($"✅ تم العثور على الجامعة: {university.NameArabic}");

                // بناء الاستجابة التفصيلية
                var response = new
                {
                    success = true,
                    data = new
                    {
                        University = new
                        {
                            university.Id,
                            university.NameArabic,
                            university.NameEnglish,
                            university.Description,
                           

                        },
                        Faculties = university.Faculties.Select(f => new
                        {
                            // 🔹 المعلومات الأساسية للكلية
                            BasicInfo = new
                            {
                                f.Id,
                                f.NameArabic,
                                f.NameEnglish,
                                f.Description,
                                f.StudentsNumber,
                                f.DurationOfStudy,
                                f.ProgramsNumber,
                                f.Rank,
                                f.Specializations,
                                f.RequireAcceptanceTests,
                               
                                f.CreatedAt,
                                f.UpdatedAt
                            },

                            // 🔹 التخصصات
                            Specializations = f.SpecializationList.Select(s => new
                            {
                                s.Id,
                                s.Name,
                                s.YearsNumber,
                                s.Description,
                                s.AcademicQualification,
                                s.CreatedAt
                            }).ToList(),

                            // 🔹 خطة الدراسة الكاملة
                            StudyPlan = f.StudyPlanYears.OrderBy(y => y.YearNumber).Select(y => new
                            {
                                // معلومات السنة الدراسية
                                YearInfo = new
                                {
                                    y.Id,
                                    y.YearName,
                                    y.YearNumber,
                                    y.Type, // General أو Specialized
                                    y.CreatedAt
                                },

                                // 🔸 المواد العامة (للسنوات العامة)
                                GeneralMaterials = y.AcademicMaterials
                                    .Where(m => m.StudyPlanSectionId == null) // مواد بدون قسم
                                    .Select(m => new
                                    {
                                        m.Id,
                                        m.Name,
                                        m.Code,
                                        m.Semester,
                                        m.Type,
                                        m.CreditHours,
                                        IsSpecialized = false,
                                        SectionId = (int?)null,
                                        SectionName = (string?)null
                                    })
                                    .OrderBy(m => m.Semester)
                                    .ThenBy(m => m.Name)
                                    .ToList(),

                                // 🔸 الأقسام والمواد المتخصصة (للسنوات المتخصصة)
                                Sections = y.Sections.OrderBy(s => s.Name).Select(s => new
                                {
                                    SectionInfo = new
                                    {
                                        s.Id,
                                        s.Name,
                                        s.Code,
                                        s.CreditHours,
                                        s.CreatedAt
                                    },

                                    // مواد القسم
                                    Materials = s.AcademicMaterials.Select(m => new
                                    {
                                        m.Id,
                                        m.Name,
                                        m.Code,
                                        m.Semester,
                                        m.Type,
                                        m.CreditHours,
                                        IsSpecialized = true,
                                        SectionId = s.Id,
                                        SectionName = s.Name
                                    })
                                    .OrderBy(m => m.Semester)
                                    .ThenBy(m => m.Name)
                                    .ToList()
                                }).ToList(),

                                // 🔸 الوسائط المرتبطة بالسنة
                                Media = y.StudyPlanMedia.Select(m => new
                                {
                                    m.Id,
                                    m.MediaType,
                                    m.MediaLink,
                                    m.VisitLink,
                                    m.CreatedAt
                                }).ToList()
                            }).ToList(),

                            // 🔹 فرص العمل
                            JobOpportunities = f.JobOpportunities.Select(j => new
                            {
                                j.Id,
                                j.Name,
                                j.CreatedAt
                            }).ToList(),

                            // 🔹 الإحصائيات
                            Statistics = new
                            {
                                SpecializationsCount = f.SpecializationList.Count,
                                StudyYearsCount = f.StudyPlanYears.Count,
                                TotalSections = f.StudyPlanYears.Sum(y => y.Sections.Count),
                                TotalGeneralMaterials = f.StudyPlanYears.Sum(y => y.AcademicMaterials.Count(m => m.StudyPlanSectionId == null)),
                                TotalSpecializedMaterials = f.StudyPlanYears.Sum(y => y.Sections.Sum(s => s.AcademicMaterials.Count)),
                                TotalMedia = f.StudyPlanYears.Sum(y => y.StudyPlanMedia.Count),
                                JobOpportunitiesCount = f.JobOpportunities.Count,
                                HasStudyPlan = f.StudyPlanYears.Any(),
                                HasSpecializations = f.SpecializationList.Any(),
                                HasJobs = f.JobOpportunities.Any()
                            },

                            // 🔹 ملخص سريع
                            Summary = new
                            {
                                TotalMaterials = f.StudyPlanYears.Sum(y =>
                                    y.AcademicMaterials.Count(m => m.StudyPlanSectionId == null) +
                                    y.Sections.Sum(s => s.AcademicMaterials.Count)),
                                TotalCredits = f.StudyPlanYears.Sum(y =>
                                    y.AcademicMaterials.Where(m => m.StudyPlanSectionId == null).Sum(m => m.CreditHours) +
                                    y.Sections.Sum(s => s.AcademicMaterials.Sum(m => m.CreditHours)))
                            }
                        })
                        .OrderBy(f => f.BasicInfo.NameArabic)
                        .ToList(),

                        // 🔹 إحصائيات عامة للجامعة
                        UniversitySummary = new
                        {
                            TotalFaculties = university.Faculties.Count,
                            TotalSpecializations = university.Faculties.Sum(f => f.SpecializationList.Count),
                            TotalStudyYears = university.Faculties.Sum(f => f.StudyPlanYears.Count),
                            TotalMaterials = university.Faculties.Sum(f => f.StudyPlanYears.Sum(y =>
                                y.AcademicMaterials.Count(m => m.StudyPlanSectionId == null) +
                                y.Sections.Sum(s => s.AcademicMaterials.Count))),
                            TotalJobOpportunities = university.Faculties.Sum(f => f.JobOpportunities.Count),
                            TotalStudents = university.Faculties.Sum(f => f.StudentsNumber ?? 0),
                            FacultiesWithStudyPlan = university.Faculties.Count(f => f.StudyPlanYears.Any()),
                            FacultiesWithSpecializations = university.Faculties.Count(f => f.SpecializationList.Any()),
                            FacultiesWithJobs = university.Faculties.Count(f => f.JobOpportunities.Any())
                        }
                    }
                };

                Console.WriteLine($"✅ تم جلب {university.Faculties.Count} كلية مع التفاصيل الكاملة");
                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطأ: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء جلب البيانات",
                    error = ex.Message
                });
            }
        }

        // 📌 DELETE: api/faculties/{facultyId}/university/{universityId}
        [HttpDelete("{facultyId}/university/{universityId}")]
        public async Task<IActionResult> DeleteFaculty(int facultyId, int universityId)
        {
            try
            {
                Console.WriteLine($"🗑️ محاولة حذف الكلية {facultyId} من الجامعة {universityId}");

                // البحث عن الكلية مع التحقق من أنها تابعة للجامعة
                var faculty = await _context.Faculties
                    .Include(f => f.SpecializationList)
                    .Include(f => f.StudyPlanYears)
                        .ThenInclude(y => y.AcademicMaterials)
                    .Include(f => f.StudyPlanYears)
                        .ThenInclude(y => y.Sections)
                            .ThenInclude(s => s.AcademicMaterials)
                    .Include(f => f.StudyPlanYears)
                        .ThenInclude(y => y.StudyPlanMedia)
                    .Include(f => f.JobOpportunities)
                    .FirstOrDefaultAsync(f => f.Id == facultyId && f.UniversityId == universityId && !f.IsDeleted);

                if (faculty == null)
                {
                    Console.WriteLine($"❌ الكلية غير موجودة أو لا تنتمي للجامعة");
                    return NotFound(new
                    {
                        success = false,
                        message = "الكلية غير موجودة أو لا تنتمي لهذه الجامعة"
                    });
                }

                Console.WriteLine($"✅ تم العثور على الكلية: {faculty.NameArabic}");

                // حذف ناعم Soft Delete
                faculty.IsDeleted = true;
                faculty.DeletedAt = DateTime.UtcNow;
                faculty.UpdatedAt = DateTime.UtcNow;

                // حذف التخصصات المرتبطة
                foreach (var specialization in faculty.SpecializationList.Where(s => !s.IsDeleted))
                {
                    specialization.IsDeleted = true;
                    specialization.DeletedAt = DateTime.UtcNow;
                }

                // حذف سنوات الدراسة والبيانات المرتبطة
                foreach (var year in faculty.StudyPlanYears.Where(y => !y.IsDeleted))
                {
                    year.IsDeleted = true;
                    year.DeletedAt = DateTime.UtcNow;

                    // حذف مواد السنة العامة
                    foreach (var material in year.AcademicMaterials.Where(m => !m.IsDeleted))
                    {
                        material.IsDeleted = true;
                        material.DeletedAt = DateTime.UtcNow;
                    }

                    // حذف الأقسام وموادها
                    foreach (var section in year.Sections.Where(s => !s.IsDeleted))
                    {
                        section.IsDeleted = true;
                        section.DeletedAt = DateTime.UtcNow;

                        foreach (var material in section.AcademicMaterials.Where(m => !m.IsDeleted))
                        {
                            material.IsDeleted = true;
                            material.DeletedAt = DateTime.UtcNow;
                        }
                    }

                    // حذف الوسائط
                    foreach (var media in year.StudyPlanMedia.Where(m => !m.IsDeleted))
                    {
                        media.IsDeleted = true;
                        media.DeletedAt = DateTime.UtcNow;
                    }
                }

                // حذف فرص العمل
                foreach (var job in faculty.JobOpportunities.Where(j => !j.IsDeleted))
                {
                    job.IsDeleted = true;
                    job.DeletedAt = DateTime.UtcNow;
                }

                // حفظ التغييرات
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ تم حذف الكلية بنجاح (Soft Delete)");

                return Ok(new
                {
                    success = true,
                    message = "تم حذف الكلية بنجاح",
                    facultyId,
                    universityId,
                    facultyName = faculty.NameArabic,
                    deletedAt = DateTime.UtcNow,
                    isSoftDelete = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطأ في الحذف: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء حذف الكلية",
                    error = ex.Message
                });
            }
        }

        // 📌 DELETE: api/faculties/{facultyId}/university/{universityId}/permanent
        [HttpDelete("{facultyId}/university/{universityId}/permanent")]
        public async Task<IActionResult> PermanentDeleteFaculty(int facultyId, int universityId)
        {
            try
            {
                Console.WriteLine($"⚠️ محاولة حذف نهائي للكلية {facultyId} من الجامعة {universityId}");

                // التحقق من الصلاحيات (يمكنك إضافة Authorization هنا)
                // if (!User.IsInRole("Admin")) return Forbid();

                // البحث عن الكلية مع كل بياناتها
                var faculty = await _context.Faculties
                    .Include(f => f.SpecializationList)
                    .Include(f => f.StudyPlanYears)
                        .ThenInclude(y => y.AcademicMaterials)
                    .Include(f => f.StudyPlanYears)
                        .ThenInclude(y => y.Sections)
                            .ThenInclude(s => s.AcademicMaterials)
                    .Include(f => f.StudyPlanYears)
                        .ThenInclude(y => y.StudyPlanMedia)
                    .Include(f => f.JobOpportunities)
                    .FirstOrDefaultAsync(f => f.Id == facultyId && f.UniversityId == universityId);

                if (faculty == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الكلية غير موجودة أو لا تنتمي لهذه الجامعة"
                    });
                }

                Console.WriteLine($"✅ تم العثور على الكلية: {faculty.NameArabic}");

                // حفظ بعض المعلومات للإرجاع
                var facultyName = faculty.NameArabic;
                var createdAt = faculty.CreatedAt;

                // حذف البيانات المرتبطة
                foreach (var year in faculty.StudyPlanYears)
                {
                    // حذف مواد السنة
                    _context.AcademicMaterials.RemoveRange(year.AcademicMaterials);

                    // حذف أقسام السنة وموادها
                    foreach (var section in year.Sections)
                    {
                        _context.AcademicMaterials.RemoveRange(section.AcademicMaterials);
                        _context.StudyPlanSections.Remove(section);
                    }

                    // حذف وسائط السنة
                    _context.StudyPlanMedia.RemoveRange(year.StudyPlanMedia);

                    _context.StudyPlanYears.Remove(year);
                }

                // حذف التخصصات
                _context.Specializations.RemoveRange(faculty.SpecializationList);

                // حذف فرص العمل
                _context.JobOpportunities.RemoveRange(faculty.JobOpportunities);

                // حذف الكلية
                _context.Faculties.Remove(faculty);

                // حفظ التغييرات
                await _context.SaveChangesAsync();

                Console.WriteLine($"⚠️ تم الحذف النهائي للكلية");

                return Ok(new
                {
                    success = true,
                    message = "تم حذف الكلية نهائياً بنجاح",
                    facultyId,
                    universityId,
                    facultyName,
                    createdAt,
                    deletedAt = DateTime.UtcNow,
                    isPermanentDelete = true,
                    deletedItems = new
                    {
                        faculty = 1,
                        specializations = faculty.SpecializationList.Count,
                        studyYears = faculty.StudyPlanYears.Count,
                        jobOpportunities = faculty.JobOpportunities.Count
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطأ في الحذف النهائي: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء الحذف النهائي",
                    error = ex.Message
                });
            }
        }

        // 📌 PUT: api/faculties/{facultyId}/university/{universityId}
        // 📌 PUT: api/faculties/{facultyId}/university/{universityId}
        [HttpPut("{facultyId}/university/{universityId}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateFaculty(int facultyId, int universityId, [FromForm] FacultyFormModel model)
        {
            try
            {
                Console.WriteLine($"✏️ محاولة تعديل الكلية {facultyId} في الجامعة {universityId}");

                // 1. التحقق الأساسي
                if (string.IsNullOrEmpty(model.NameArabic) || string.IsNullOrEmpty(model.Description))
                {
                    return BadRequest(new { success = false, message = "البيانات الأساسية مطلوبة" });
                }

                // 2. البحث عن الكلية مع كل بياناتها
                var faculty = await _context.Faculties
                    .Include(f => f.SpecializationList.Where(s => !s.IsDeleted))
                    .Include(f => f.StudyPlanYears.Where(y => !y.IsDeleted))
                        .ThenInclude(y => y.AcademicMaterials.Where(m => !m.IsDeleted))
                    .Include(f => f.StudyPlanYears)
                        .ThenInclude(y => y.Sections.Where(s => !s.IsDeleted))
                            .ThenInclude(s => s.AcademicMaterials.Where(m => !m.IsDeleted))
                    .Include(f => f.StudyPlanYears)
                        .ThenInclude(y => y.StudyPlanMedia.Where(m => !m.IsDeleted))
                    .Include(f => f.JobOpportunities.Where(j => !j.IsDeleted))
                    .FirstOrDefaultAsync(f => f.Id == facultyId && f.UniversityId == universityId && !f.IsDeleted);

                if (faculty == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الكلية غير موجودة أو لا تنتمي لهذه الجامعة"
                    });
                }

                Console.WriteLine($"✅ تم العثور على الكلية: {faculty.NameArabic}");

                // 3. تحديث بيانات الكلية الأساسية (مثل Add)
                faculty.NameArabic = model.NameArabic;
                faculty.NameEnglish = !string.IsNullOrEmpty(model.NameEnglish) ? model.NameEnglish : model.NameArabic;
                faculty.Description = model.Description;
                faculty.StudentsNumber = model.StudentsNumber;
                faculty.DurationOfStudy = !string.IsNullOrEmpty(model.DurationOfStudy) ? model.DurationOfStudy : "4 سنوات";
                faculty.ProgramsNumber = model.ProgramsNumber;
                faculty.Specializations = !string.IsNullOrEmpty(model.Specializations) ? model.Specializations : "لا يوجد";
                faculty.RequireAcceptanceTests = model.RequireAcceptanceTests;
                faculty.UpdatedAt = DateTime.UtcNow;

                // 4. حذف البيانات القديمة (Soft Delete)
                var specCount = await SoftDeleteSpecializations(faculty);
                var yearCount = await SoftDeleteStudyPlan(faculty);
                var jobCount = await SoftDeleteJobOpportunities(faculty);

                Console.WriteLine($"🗑️ تم حذف: {specCount} تخصص، {yearCount} سنة دراسية، {jobCount} فرصة عمل");

                // 5. إضافة البيانات الجديدة (تماماً مثل Add)
                var newSpecCount = 0;
                var newYearCount = 0;
                var newSemesterCount = 0;
                var newMaterialCount = 0;
                var newJobCount = 0;

                // 5.1 إضافة التخصصات الجديدة
                if (model.SpecializationNames != null && model.SpecializationNames.Any())
                {
                    for (int i = 0; i < model.SpecializationNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(model.SpecializationNames[i])) continue;

                        var spec = new Specialization
                        {
                            Name = model.SpecializationNames[i],
                            YearsNumber = (model.SpecializationYearsNumbers != null && i < model.SpecializationYearsNumbers.Count)
                                ? model.SpecializationYearsNumbers[i] : 4,
                            Description = (model.SpecializationDescriptions != null && i < model.SpecializationDescriptions.Count)
                                ? model.SpecializationDescriptions[i] : "",
                            AcademicQualification = "",
                            FacultyId = facultyId
                        };

                        _context.Specializations.Add(spec);
                        newSpecCount++;
                    }
                }

                // 5.2 إضافة خطة الدراسة الجديدة (تماماً مثل Add)
                if (model.YearNames != null && model.YearNames.Count > 0)
                {
                    for (int yearIndex = 0; yearIndex < model.YearNames.Count; yearIndex++)
                    {
                        string yearName = model.YearNames[yearIndex];
                        if (string.IsNullOrEmpty(yearName)) continue;

                        bool hasSpecialization = false;
                        if (model.YearHasSpecialization != null && yearIndex < model.YearHasSpecialization.Count)
                        {
                            hasSpecialization = model.YearHasSpecialization[yearIndex];
                        }

                        // إنشاء السنة الدراسية
                        var studyPlanYear = new StudyPlanYear
                        {
                            YearName = yearName,
                            YearNumber = yearIndex + 1,
                            Type = hasSpecialization ? "Specialized" : "General",
                            FacultyId = facultyId
                        };

                        _context.StudyPlanYears.Add(studyPlanYear);
                        await _context.SaveChangesAsync();

                        var studyPlanYearId = studyPlanYear.Id;
                        newYearCount++;

                        // 🔹 إضافة الوسائط لهذه السنة
                        if (model.MediaTypes != null && model.MediaYearIndices != null)
                        {
                            for (int mediaIndex = 0; mediaIndex < model.MediaYearIndices.Count; mediaIndex++)
                            {
                                if (model.MediaYearIndices[mediaIndex] == yearIndex &&
                                    mediaIndex < model.MediaTypes.Count &&
                                    !string.IsNullOrEmpty(model.MediaTypes[mediaIndex]))
                                {
                                    string mediaLink = "";

                                    if (model.MediaFiles != null && mediaIndex < model.MediaFiles.Count &&
                                        model.MediaFiles[mediaIndex] != null && model.MediaFiles[mediaIndex].Length > 0)
                                    {
                                        mediaLink = await SaveFile(model.MediaFiles[mediaIndex], "studyplan-media");
                                    }

                                    if (!string.IsNullOrEmpty(mediaLink))
                                    {
                                        var media = new StudyPlanMedia
                                        {
                                            MediaType = model.MediaTypes[mediaIndex],
                                            MediaLink = mediaLink,
                                            VisitLink = "",
                                            StudyPlanYearId = studyPlanYearId
                                        };

                                        _context.StudyPlanMedia.Add(media);
                                    }
                                }
                            }
                        }

                        // 🔹 إضافة الفصول الدراسية لهذه السنة
                        if (model.SemesterNames != null && model.SemesterYearIndices != null)
                        {
                            for (int semIndex = 0; semIndex < model.SemesterYearIndices.Count; semIndex++)
                            {
                                if (model.SemesterYearIndices[semIndex] == yearIndex &&
                                    semIndex < model.SemesterNames.Count &&
                                    !string.IsNullOrEmpty(model.SemesterNames[semIndex]))
                                {
                                    newSemesterCount++;
                                    bool hasSections = false;

                                    // 🔹 إضافة أقسام هذا الفصل
                                    if (model.SectionNames != null && model.SectionSemesterIndices != null)
                                    {
                                        for (int secIndex = 0; secIndex < model.SectionSemesterIndices.Count; secIndex++)
                                        {
                                            if (model.SectionSemesterIndices[secIndex] == semIndex &&
                                                secIndex < model.SectionNames.Count &&
                                                !string.IsNullOrEmpty(model.SectionNames[secIndex]))
                                            {
                                                hasSections = true;

                                                // إنشاء القسم
                                                var section = new StudyPlanSection
                                                {
                                                    Name = model.SectionNames[secIndex],
                                                    Code = $"SEC-{yearIndex + 1}-{semIndex + 1}-{secIndex + 1}",
                                                    StudyPlanYearId = studyPlanYearId
                                                };

                                                _context.StudyPlanSections.Add(section);
                                                await _context.SaveChangesAsync();

                                                var sectionId = section.Id;

                                                // 🔹 إضافة مواد هذا القسم
                                                if (model.SectionMaterialNames != null &&
                                                    model.SectionMaterialSectionIndices != null)
                                                {
                                                    for (int matIndex = 0; matIndex < model.SectionMaterialSectionIndices.Count; matIndex++)
                                                    {
                                                        if (model.SectionMaterialSectionIndices[matIndex] == secIndex &&
                                                            matIndex < model.SectionMaterialNames.Count &&
                                                            !string.IsNullOrEmpty(model.SectionMaterialNames[matIndex]))
                                                        {
                                                            var material = new AcademicMaterial
                                                            {
                                                                Name = model.SectionMaterialNames[matIndex],
                                                                Code = $"MAT-{yearIndex + 1}-{semIndex + 1}-{secIndex + 1}-{matIndex + 1}",
                                                                Semester = semIndex + 1,
                                                                Type = "Mandatory",
                                                                CreditHours = 3,
                                                                StudyPlanYearId = null,
                                                                StudyPlanSectionId = sectionId
                                                            };

                                                            _context.AcademicMaterials.Add(material);
                                                            newMaterialCount++;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // 🔹 إذا كان الفصل بدون أقسام، إضافة المواد مباشرة
                                    if (!hasSections && model.SemesterMaterialNames != null &&
                                        model.SemesterMaterialSemesterIndices != null)
                                    {
                                        for (int matIndex = 0; matIndex < model.SemesterMaterialSemesterIndices.Count; matIndex++)
                                        {
                                            if (model.SemesterMaterialSemesterIndices[matIndex] == semIndex &&
                                                matIndex < model.SemesterMaterialNames.Count &&
                                                !string.IsNullOrEmpty(model.SemesterMaterialNames[matIndex]))
                                            {
                                                var material = new AcademicMaterial
                                                {
                                                    Name = model.SemesterMaterialNames[matIndex],
                                                    Code = $"MAT-{yearIndex + 1}-{semIndex + 1}-{matIndex + 1}",
                                                    Semester = semIndex + 1,
                                                    Type = "Mandatory",
                                                    CreditHours = 3,
                                                    StudyPlanYearId = studyPlanYearId,
                                                    StudyPlanSectionId = null
                                                };

                                                _context.AcademicMaterials.Add(material);
                                                newMaterialCount++;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                // 5.3 إضافة فرص العمل الجديدة
                if (model.JobOpportunityNames != null)
                {
                    for (int i = 0; i < model.JobOpportunityNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(model.JobOpportunityNames[i])) continue;

                        var job = new JobOpportunity
                        {
                            Name = model.JobOpportunityNames[i],
                            FacultyId = facultyId
                        };

                        _context.JobOpportunities.Add(job);
                        newJobCount++;
                    }
                }

                // 6. حفظ كل التغييرات
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ تم التحديث: {newSpecCount} تخصص، {newYearCount} سنة، {newMaterialCount} مادة، {newJobCount} فرصة عمل");

                return Ok(new
                {
                    success = true,
                    message = "تم تحديث الكلية بنجاح",
                    facultyId,
                    universityId,
                    updatedAt = faculty.UpdatedAt,
                    deleted = new
                    {
                        specializations = specCount,
                        studyYears = yearCount,
                        jobOpportunities = jobCount
                    },
                    added = new
                    {
                        specializations = newSpecCount,
                        studyYears = newYearCount,
                        semesters = newSemesterCount,
                        materials = newMaterialCount,
                        jobOpportunities = newJobCount
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطأ في التحديث: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء تحديث الكلية",
                    error = ex.Message
                });
            }
        }

        // 🔧 دوال Soft Delete (مماثلة لما في Add لكن بالحذف الناعم)
        private async Task<int> SoftDeleteSpecializations(Faculty faculty)
        {
            int count = 0;
            var existingSpecs = faculty.SpecializationList.ToList();

            foreach (var spec in existingSpecs)
            {
                spec.IsDeleted = true;
                spec.DeletedAt = DateTime.UtcNow;
                count++;
            }

            return count;
        }

        private async Task<int> SoftDeleteStudyPlan(Faculty faculty)
        {
            int count = 0;
            var existingYears = faculty.StudyPlanYears.ToList();

            foreach (var year in existingYears)
            {
                year.IsDeleted = true;
                year.DeletedAt = DateTime.UtcNow;
                count++;

                foreach (var material in year.AcademicMaterials)
                {
                    material.IsDeleted = true;
                    material.DeletedAt = DateTime.UtcNow;
                }

                foreach (var section in year.Sections)
                {
                    section.IsDeleted = true;
                    section.DeletedAt = DateTime.UtcNow;

                    foreach (var material in section.AcademicMaterials)
                    {
                        material.IsDeleted = true;
                        material.DeletedAt = DateTime.UtcNow;
                    }
                }

                foreach (var media in year.StudyPlanMedia)
                {
                    media.IsDeleted = true;
                    media.DeletedAt = DateTime.UtcNow;
                }
            }

            return count;
        }

        private async Task<int> SoftDeleteJobOpportunities(Faculty faculty)
        {
            int count = 0;
            var existingJobs = faculty.JobOpportunities.ToList();

            foreach (var job in existingJobs)
            {
                job.IsDeleted = true;
                job.DeletedAt = DateTime.UtcNow;
                count++;
            }

            return count;
        }

        // 🔧 كلاس النتائج
        public class UpdateResult
        {
            public int Deleted { get; set; }
            public int Added { get; set; }
        }

        public class StudyPlanUpdateResult
        {
            public int DeletedYears { get; set; }
            public int AddedYears { get; set; }
            public int DeletedMaterials { get; set; }
            public int AddedMaterials { get; set; }
            public int DeletedSections { get; set; }
            public int AddedSections { get; set; }
            public int DeletedMedia { get; set; }
            public int AddedMedia { get; set; }
        }

        // 📌 GET: api/faculties/university/{universityId}/summary
        [HttpGet("university/{universityId}/summary")]
        public async Task<IActionResult> GetUniversityWithFacultiesSummary(int universityId)
        {
            try
            {
                Console.WriteLine($"🔍 جلب معلومات الجامعة {universityId} مع كلياتها");

                // جلب الجامعة مع معلوماتها الكاملة
                var university = await _context.Universities
                    .Include(u => u.HousingOptions.Where(f => !f.IsDeleted))
                    .Include(u => u.DocumentsRequired.Where(f => !f.IsDeleted))
                    .Include(u => u.Faculties.Where(f => !f.IsDeleted))
                        .ThenInclude(f => f.SpecializationList.Where(s => !s.IsDeleted))
                    .Include(u => u.Faculties)
                        .ThenInclude(f => f.StudyPlanYears.Where(y => !y.IsDeleted))
                    .Include(u => u.Faculties)
                        .ThenInclude(f => f.JobOpportunities.Where(j => !j.IsDeleted))
                    .FirstOrDefaultAsync(u => u.Id == universityId && !u.IsDeleted);

                if (university == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الجامعة غير موجودة"
                    });
                }

                Console.WriteLine($"✅ تم العثور على الجامعة: {university.NameArabic}");

                // بناء الاستجابة
                var response = new
                {
                    success = true,
                    data = new
                    {
                        // 🔹 معلومات الجامعة الأساسية
                        UniversityInfo = new
                        {
                            university.Id,
                            university.NameArabic,
                            university.NameEnglish,
                            university.Description,
                            university.StudentsNumber,
                            university.FoundingYear,
                            university.Location,
                            university.Address,
                            university.GlobalRanking,
                            university.Type, // حكومية، خاصة، دولية
                            university.CreatedAt, // تاريخ إضافة الجامعة
                         

                            // 🔹 معلومات الاتصال
                            ContactInfo = new
                            {
                                university.PhoneNumber,
                                university.Email,
                                university.Website
                               
                            },

                            // 🔹 خيارات السكن
                            Accommodation = new
                            {
                                university.HousingOptions.Count
                                
                            },

                            // 🔹 الموقع الجغرافي
                            LocationInfo = new
                            {
                                university.Address,
                             
                                university.City
                              
                            },

                            // 🔹 وسائل التواصل الاجتماعي
                            SocialMedia = new
                            {
                                university.FacebookPage,
                                university.Website,
                                university.Email,
                                university.PhoneNumber
                               
                            },

                            // 🔹 الشعار والصور
                            Media = new
                            {
                                university.UniversityImage,
                            
                            },

                            // 🔹 الاعتمادات والشهادات
                            
                        },

                        // 🔹 إحصائيات الجامعة
                        UniversityStats = new
                        {
                            TotalFaculties = university.Faculties.Count,
                            TotalPrograms = university.Faculties.Sum(f => f.ProgramsNumber ?? 0),
                            TotalSpecializations = university.Faculties.Sum(f => f.SpecializationList.Count),
                            TotalStudyYears = university.Faculties.Sum(f => f.StudyPlanYears.Count),
                            TotalJobOpportunities = university.Faculties.Sum(f => f.JobOpportunities.Count),

                            // متوسطات
                            AverageStudentsPerFaculty = university.Faculties.Any()
                                ? Math.Round(university.Faculties.Average(f => f.StudentsNumber ?? 0), 0)
                                : 0,

                            AverageDuration = university.Faculties.Any()
                                ? GetAverageDuration(university.Faculties)
                                : "0 سنوات"
                        },

                        // 🔹 قائمة الكليات مع معلومات موجزة
                        Faculties = university.Faculties
                            .OrderBy(f => f.NameArabic)
                            .Select(f => new
                            {
                                f.Id,
                                f.NameArabic,
                                f.NameEnglish,
                                f.Description,
                                f.StudentsNumber,
                                f.DurationOfStudy,
                                f.ProgramsNumber,
                                f.Rank,
                                f.Specializations,
                                f.RequireAcceptanceTests,
                              
                                f.CreatedAt,

                                // 🔹 معلومات موجزة عن التخصصات
                                SpecializationsString = f.Specializations, // هذا string
                                SpecializationsList = f.SpecializationList
                                 .Where(s => !s.IsDeleted)
                                 .Take(3)
                                 .Select(s => new
                                 {
                                     s.Id,
                                     s.Name,
                                     s.YearsNumber
                                 })
                                 .ToList(),
                                
                                SpecializationsCount = f.SpecializationList.Count,

                                // 🔹 معلومات موجزة عن خطة الدراسة
                                StudyPlanInfo = new
                                {
                                    HasStudyPlan = f.StudyPlanYears.Any(),
                                    YearsCount = f.StudyPlanYears.Count,
                                    FirstYearName = f.StudyPlanYears
                                        .OrderBy(y => y.YearNumber)
                                        .Select(y => y.YearName)
                                        .FirstOrDefault()
                                },

                                // 🔹 معلومات موجزة عن فرص العمل
                                JobOpportunitiesInfo = new
                                {
                                    HasJobs = f.JobOpportunities.Any(),
                                    Count = f.JobOpportunities.Count,
                                    TopJobs = f.JobOpportunities
                                        .Take(2)
                                        .Select(j => new { j.Id, j.Name })
                                        .ToList()
                                },

                                // 🔹 إحصائيات الكلية
                                Stats = new
                                {
                                    HasSpecializations = f.SpecializationList.Any(),
                                    HasStudyPlan = f.StudyPlanYears.Any(),
                                    HasJobs = f.JobOpportunities.Any(),
                                    IsActive = true // يمكنك إضافة منطق للنشاط
                                }
                            })
                            .ToList(),

                        // 🔹 تقسيم الكليات حسب النوع (إذا كان لديك أنواع)
                        FacultiesByCategory = new
                        {
                            Engineering = university.Faculties
                                .Where(f => f.NameArabic.Contains("هندسة") || f.NameEnglish.Contains("Engineering"))
                                .Select(f => new { f.Id, f.NameArabic })
                                .ToList(),

                            Medical = university.Faculties
                                .Where(f => f.NameArabic.Contains("طب") || f.NameArabic.Contains("صحة") ||
                                           f.NameEnglish.Contains("Medical") || f.NameEnglish.Contains("Health"))
                                .Select(f => new { f.Id, f.NameArabic })
                                .ToList(),

                            Scientific = university.Faculties
                                .Where(f => f.NameArabic.Contains("علوم") || f.NameEnglish.Contains("Science"))
                                .Select(f => new { f.Id, f.NameArabic })
                                .ToList(),

                            Humanities = university.Faculties
                                .Where(f => !f.NameArabic.Contains("هندسة") && !f.NameArabic.Contains("طب") &&
                                           !f.NameArabic.Contains("علوم") && !f.NameEnglish.Contains("Engineering") &&
                                           !f.NameEnglish.Contains("Medical") && !f.NameEnglish.Contains("Science"))
                                .Select(f => new { f.Id, f.NameArabic })
                                .ToList()
                        },

                        // 🔹 ملاحظات إضافية
                        Notes = new
                        {
                            LastUpdated = university.UpdatedAt,
                            DataAccuracy = "محدثة حتى " + DateTime.Now.ToString("yyyy/MM/dd"),
                            Source = "قاعدة بيانات النظام",
                            Version = "1.0"
                        }
                    }
                };

                Console.WriteLine($"✅ تم جلب معلومات الجامعة مع {university.Faculties.Count} كلية");
                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطأ: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء جلب البيانات",
                    error = ex.Message
                });
            }
        }

        // 🔧 دالة مساعدة لحساب متوسط مدة الدراسة
        private string GetAverageDuration(ICollection<Faculty> faculties)
        {
            if (!faculties.Any()) return "0 سنوات";

            int totalYears = 0;
            int count = 0;

            foreach (var faculty in faculties)
            {
                if (!string.IsNullOrEmpty(faculty.DurationOfStudy))
                {
                    // استخراج الأرقام من النص (مثل: "4 سنوات" → 4)
                    var match = System.Text.RegularExpressions.Regex.Match(faculty.DurationOfStudy, @"\d+");
                    if (match.Success && int.TryParse(match.Value, out int years))
                    {
                        totalYears += years;
                        count++;
                    }
                }
            }

            return count > 0 ? $"{Math.Round((double)totalYears / count, 1)} سنوات" : "غير محدد";
        }


    }
}