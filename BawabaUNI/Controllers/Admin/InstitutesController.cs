using BawabaUNI.Models.Data;
using BawabaUNI.Models.DTOs;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Controllers.Admin
{
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class InstitutesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public InstitutesController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        #region Add Institute with Housing Options

        [HttpPost("add")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AddInstitute([FromForm] InstituteFormModel model)
        {
            try
            {
                Console.WriteLine("=== بدء إضافة معهد ===");

                // 1. التحقق الأساسي
                if (string.IsNullOrEmpty(model.NameArabic) || string.IsNullOrEmpty(model.Description))
                {
                    return BadRequest(new { success = false, message = "البيانات الأساسية مطلوبة" });
                }

                if (string.IsNullOrEmpty(model.Type))
                {
                    return BadRequest(new { success = false, message = "نوع المعهد مطلوب (حكومي / خاص)" });
                }

                // 2. معالجة الصورة
                var imageUrl = "";
                if (model.Image != null)
                {
                    var imageLink = await SaveFile(model.Image, "institute-images");
                    if (!string.IsNullOrEmpty(imageLink))
                    {
                        imageUrl = imageLink;
                        Console.WriteLine($"📁 تم رفع صورة المعهد: {imageLink}");
                    }
                }

                // 3. إنشاء المعهد باستخدام Faculty entity مع UniversityId = null
                var institute = new Faculty
                {
                    NameArabic = model.NameArabic,
                    NameEnglish = model.NameEnglish ?? model.NameArabic,
                    Description = model.Description,
                    Type = model.Type, // "معهد حكومي", "معهد خاص", etc.
                    StudentsNumber = model.StudentsNumber,
                    DurationOfStudy = model.DurationOfStudy ?? "2 سنوات",
                    ProgramsNumber = model.ProgramsNumber,
                    RequireAcceptanceTests = model.RequireAcceptanceTests,
                    UniversityId = null, // معهد ليس تابعاً لجامعة
                    Coordination = model.Coordination,
                    Expenses = model.Expenses,
                    GroupLink = model.GroupLink,
                    InstitutePageLink = model.InstitutePageLink,
                    DescriptionOfStudyPlan = model.DescriptionOfStudyPlan,
                    Address = model.Address,
                    ImageUrl = imageUrl,

                    // حقول إضافية للمعهد
                    HasHousing = model.HasHousing,
                   
                };

                _context.Faculties.Add(institute);
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ تم إنشاء المعهد ID: {institute.Id}");

                var instituteId = institute.Id;

                // 4. إضافة خيارات السكن إذا كانت متوفرة
                if (model.HasHousing && model.HousingOptionNames != null && model.HousingOptionNames.Any())
                {
                    await AddHousingOptionsFromLists(instituteId, model);
                }

                // 5. إضافة التخصصات
                var specCount = 0;
                if (model.SpecializationNames != null)
                {
                    specCount = await AddSpecializations(instituteId, model);
                }

                // 6. إضافة خطة الدراسة
                var studyPlanStats = await AddStudyPlan(instituteId, model);

                // 7. إضافة فرص العمل
                var jobCount = 0;
                if (model.JobOpportunityNames != null)
                {
                    jobCount = await AddJobOpportunities(instituteId, model);
                }

                return Ok(new
                {
                    success = true,
                    message = "تم إضافة المعهد بنجاح",
                    instituteId,
                    type = model.Type,
                    hasHousing = model.HasHousing,
                    housingOptionsCount = model.HousingOptionNames?.Count ?? 0,
                    specializationCount = specCount,
                    yearCount = studyPlanStats.yearCount,
                    semesterCount = studyPlanStats.semesterCount,
                    sectionCount = studyPlanStats.sectionCount,
                    materialCount = studyPlanStats.materialCount,
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

        #endregion

        #region Get Institutes with Housing Options

        [HttpGet]
        public async Task<IActionResult> GetAllInstitutes([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _context.Faculties
                    .Where(f => f.UniversityId == null && !f.IsDeleted); // معاهد فقط (UniversityId = null)

                // فلترة حسب النوع إذا أرسل المستخدم فلتر
                // يمكن إضافة فلتر حسب Type (حكومي/خاص)

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var institutes = await query
                    .Include(i => i.SpecializationList.Where(s => !s.IsDeleted))
                    .Include(i => i.StudyPlanYears.Where(y => !y.IsDeleted))
                    .Include(i => i.JobOpportunities.Where(j => !j.IsDeleted))
                    .Include(i => i.FacultyHousingOption.Where(h => !h.IsDeleted))
                    .OrderBy(i => i.NameArabic)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(i => new
                    {
                        i.Id,
                        i.NameArabic,
                        i.NameEnglish,
                        i.Description,
                        i.Type, // نوع المعهد (حكومي/خاص)
                        i.StudentsNumber,
                        i.DurationOfStudy,
                        i.ProgramsNumber,
                        i.RequireAcceptanceTests,
                        i.Expenses,
                        i.Coordination,
                        i.GroupLink,
                        i.HasHousing,
                        i.ImageUrl,
                      

                        // إحصائيات
                        SpecializationsCount = i.SpecializationList.Count,
                        StudyYearsCount = i.StudyPlanYears.Count,
                        JobOpportunitiesCount = i.JobOpportunities.Count,
                        HousingOptionsCount = i.FacultyHousingOption.Count,

                        // بيانات خيارات السكن
                        HousingOptions = i.FacultyHousingOption.Select(h => new
                        {
                            h.Id,
                            h.Name,
                            h.PhoneNumber,
                            h.Description,
                            h.ImagePath
                        }).ToList(),

                        i.CreatedAt,
                        i.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        Institutes = institutes,
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

        [HttpGet("by-type/{type}")]
        public async Task<IActionResult> GetInstitutesByType(string type, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _context.Faculties
                    .Where(f => f.UniversityId == null && f.Type == type && !f.IsDeleted);

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var institutes = await query
                    .Include(i => i.FacultyHousingOption.Where(h => !h.IsDeleted))
                    .OrderBy(i => i.NameArabic)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(i => new
                    {
                        i.Id,
                        i.NameArabic,
                        i.NameEnglish,
                        i.Description,
                        i.Type,
                        i.HasHousing,
                        i.ImageUrl,
                        HousingOptionsCount = i.FacultyHousingOption.Count,
                        i.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        InstituteType = type,
                        Institutes = institutes,
                        Pagination = new
                        {
                            CurrentPage = page,
                            PageSize = pageSize,
                            TotalCount = totalCount,
                            TotalPages = totalPages
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{instituteId}")]
        public async Task<IActionResult> GetInstituteById(int instituteId)
        {
            try
            {
                Console.WriteLine($"🔍 جلب معهد ID: {instituteId} مع التفاصيل الكاملة");

                var institute = await _context.Faculties
                    .Include(i => i.SpecializationList.Where(s => !s.IsDeleted))
                    .Include(i => i.StudyPlanYears.Where(y => !y.IsDeleted))
                        .ThenInclude(y => y.AcademicMaterials.Where(m => !m.IsDeleted && m.StudyPlanSectionId == null))
                    .Include(i => i.StudyPlanYears.Where(y => !y.IsDeleted))
                        .ThenInclude(y => y.Sections.Where(s => !s.IsDeleted))
                            .ThenInclude(s => s.AcademicMaterials.Where(m => !m.IsDeleted))
                    .Include(i => i.StudyPlanYears.Where(y => !y.IsDeleted))
                        .ThenInclude(y => y.StudyPlanMedia.Where(m => !m.IsDeleted))
                    .Include(i => i.JobOpportunities.Where(j => !j.IsDeleted))
                    .Include(i => i.FacultyHousingOption.Where(h => !h.IsDeleted))
                    .FirstOrDefaultAsync(i => i.Id == instituteId && i.UniversityId == null && !i.IsDeleted);

                if (institute == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "المعهد غير موجود"
                    });
                }

                var response = new
                {
                    success = true,
                    data = new
                    {
                        // المعلومات الأساسية
                        BasicInfo = new
                        {
                            institute.Id,
                            institute.NameArabic,
                            institute.NameEnglish,
                            institute.Description,
                            institute.Type, // حكومي/خاص
                            institute.StudentsNumber,
                            institute.DurationOfStudy,
                            institute.ProgramsNumber,
                            institute.Rank,
                            institute.RequireAcceptanceTests,
                            institute.Expenses,
                            institute.Coordination,
                            institute.GroupLink,
                            institute.InstitutePageLink,
                            institute.HasHousing,
                            institute.ImageUrl,
                            institute.Address,
                            institute.DescriptionOfStudyPlan,
                          

                            institute.CreatedAt,
                            institute.UpdatedAt
                        },

                        // خيارات السكن
                        HousingOptions = institute.FacultyHousingOption.Select(h => new
                        {
                            h.Id,
                            h.Name,
                            h.PhoneNumber,
                            h.Description,
                            h.ImagePath,
                            h.CreatedAt
                        }).ToList(),

                        // التخصصات
                        Specializations = institute.SpecializationList.Select(s => new
                        {
                            s.Id,
                            s.Name,
                            s.YearsNumber,
                            s.Description,
                            s.AcademicQualification,
                            s.CreatedAt
                        }).ToList(),

                        // خطة الدراسة
                        StudyPlan = institute.StudyPlanYears.OrderBy(y => y.YearNumber).Select(y => new
                        {
                            YearInfo = new
                            {
                                y.Id,
                                y.YearName,
                                y.YearNumber,
                                y.Type,
                                y.CreatedAt
                            },
                            GeneralMaterials = y.AcademicMaterials
                                .Where(m => m.StudyPlanSectionId == null)
                                .Select(m => new
                                {
                                    m.Id,
                                    m.Name,
                                    m.Code,
                                    m.Semester,
                                    m.Type,
                                    m.CreditHours
                                })
                                .OrderBy(m => m.Semester)
                                .ToList(),
                            Sections = y.Sections.OrderBy(s => s.Name).Select(s => new
                            {
                                SectionInfo = new
                                {
                                    s.Id,
                                    s.Name,
                                    s.Code,
                                    s.CreditHours
                                },
                                Materials = s.AcademicMaterials.Select(m => new
                                {
                                    m.Id,
                                    m.Name,
                                    m.Code,
                                    m.Semester,
                                    m.Type,
                                    m.CreditHours
                                }).ToList()
                            }).ToList(),
                            Media = y.StudyPlanMedia.Select(m => new
                            {
                                m.Id,
                                m.MediaType,
                                m.MediaLink,
                                m.VisitLink
                            }).ToList()
                        }).ToList(),

                        // فرص العمل
                        JobOpportunities = institute.JobOpportunities.Select(j => new
                        {
                            j.Id,
                            j.Name,
                            j.CreatedAt
                        }).ToList(),

                        // الإحصائيات
                        Statistics = new
                        {
                            SpecializationsCount = institute.SpecializationList.Count,
                            StudyYearsCount = institute.StudyPlanYears.Count,
                            TotalSections = institute.StudyPlanYears.Sum(y => y.Sections.Count),
                            TotalMaterials = institute.StudyPlanYears.Sum(y =>
                                y.AcademicMaterials.Count(m => m.StudyPlanSectionId == null) +
                                y.Sections.Sum(s => s.AcademicMaterials.Count)),
                            JobOpportunitiesCount = institute.JobOpportunities.Count,
                            HousingOptionsCount = institute.FacultyHousingOption.Count,
                            HasHousing = institute.FacultyHousingOption.Any(),
                            HasStudyPlan = institute.StudyPlanYears.Any(),
                            HasSpecializations = institute.SpecializationList.Any(),
                            HasJobs = institute.JobOpportunities.Any()
                        }
                    }
                };

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

        #endregion

        #region Update Institute
        [HttpPut("{instituteId}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateInstitute(int instituteId, [FromForm] InstituteFormModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                Console.WriteLine($"✏️ تعديل المعهد ID: {instituteId}");

                if (string.IsNullOrEmpty(model.NameArabic) || string.IsNullOrEmpty(model.Description))
                {
                    return BadRequest(new { success = false, message = "البيانات الأساسية مطلوبة" });
                }

                if (string.IsNullOrEmpty(model.Type))
                {
                    return BadRequest(new { success = false, message = "نوع المعهد مطلوب" });
                }

                var institute = await _context.Faculties
                    .Include(i => i.FacultyHousingOption)
                    .FirstOrDefaultAsync(i => i.Id == instituteId && i.UniversityId == null);

                if (institute == null)
                    return NotFound(new { success = false, message = "المعهد غير موجود" });

                // معالجة الصورة الرئيسية
                if (model.Image != null)
                {
                    if (!string.IsNullOrEmpty(institute.ImageUrl))
                    {
                        await DeleteFile(institute.ImageUrl);
                    }
                    var imageUrl = await SaveFile(model.Image, "institute-images");
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        institute.ImageUrl = imageUrl;
                    }
                }

                // تحديث البيانات الأساسية
                institute.NameArabic = model.NameArabic;
                institute.NameEnglish = model.NameEnglish ?? model.NameArabic;
                institute.Description = model.Description;
                institute.Type = model.Type;
                institute.StudentsNumber = model.StudentsNumber;
                institute.DurationOfStudy = model.DurationOfStudy ?? "2 سنوات";
                institute.ProgramsNumber = model.ProgramsNumber;
                institute.RequireAcceptanceTests = model.RequireAcceptanceTests;
                institute.Expenses = model.Expenses;
                institute.Coordination = model.Coordination;
                institute.DescriptionOfStudyPlan = model.DescriptionOfStudyPlan;
                institute.Address = model.Address;
                institute.GroupLink = model.GroupLink;
                institute.InstitutePageLink = model.InstitutePageLink;
                institute.HasHousing = model.HasHousing;
                institute.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // ========== 1. 🗑️ حذف التخصصات المحددة ==========
                var deletedSpecializationsCount = 0;
                if (model.DeletedSpecializationIds != null && model.DeletedSpecializationIds.Any())
                {
                    var specsToDelete = await _context.Specializations
                        .Where(s => model.DeletedSpecializationIds.Contains(s.Id) && s.FacultyId == instituteId)
                        .ToListAsync();

                    _context.Specializations.RemoveRange(specsToDelete);
                    deletedSpecializationsCount = specsToDelete.Count;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ تم حذف {deletedSpecializationsCount} تخصص");
                }

                // ========== 2. 🗑️ حذف فرص العمل المحددة ==========
                var deletedJobsCount = 0;
                if (model.DeletedJobOpportunityIds != null && model.DeletedJobOpportunityIds.Any())
                {
                    var jobsToDelete = await _context.JobOpportunities
                        .Where(j => model.DeletedJobOpportunityIds.Contains(j.Id) && j.FacultyId == instituteId)
                        .ToListAsync();

                    _context.JobOpportunities.RemoveRange(jobsToDelete);
                    deletedJobsCount = jobsToDelete.Count;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ تم حذف {deletedJobsCount} فرصة عمل");
                }

                // ========== 3. 🗑️ حذف السنوات المحددة (مع كل محتوياتها) ==========
                var deletedYearsCount = 0;
                if (model.DeletedYearIds != null && model.DeletedYearIds.Any())
                {
                    foreach (var yearId in model.DeletedYearIds)
                    {
                        await DeleteStudyYearWithAllContents(yearId, instituteId);
                        deletedYearsCount++;
                    }
                    Console.WriteLine($"✅ تم حذف {deletedYearsCount} سنة");
                }

                // ========== 4. 🗑️ حذف الوسائط المحددة ==========
                var deletedMediaCount = 0;
                if (model.DeletedMediaIds != null && model.DeletedMediaIds.Any())
                {
                    var mediaToDelete = await _context.StudyPlanMedia
                        .Include(m => m.StudyPlanYear)
                        .Where(m => model.DeletedMediaIds.Contains(m.Id) &&
                                   m.StudyPlanYear.FacultyId == instituteId)
                        .ToListAsync();

                    foreach (var media in mediaToDelete)
                    {
                        if (!string.IsNullOrEmpty(media.MediaLink))
                        {
                            await DeleteFile(media.MediaLink);
                            Console.WriteLine($"🗑️ تم حذف ملف الوسائط: {media.MediaLink}");
                        }
                    }

                    _context.StudyPlanMedia.RemoveRange(mediaToDelete);
                    deletedMediaCount = mediaToDelete.Count;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ تم حذف {deletedMediaCount} وسائط");
                }

                // ========== 5. 🗑️ حذف السكن المحدد ==========
                var deletedHousingCount = 0;
                if (model.DeletedHousingIds != null && model.DeletedHousingIds.Any())
                {
                    var housingToDelete = await _context.FacultyHousingOptions
                        .Where(h => h.FacultyId == instituteId && model.DeletedHousingIds.Contains(h.Id))
                        .ToListAsync();

                    foreach (var housing in housingToDelete)
                    {
                        if (!string.IsNullOrEmpty(housing.ImagePath))
                        {
                            await DeleteFile(housing.ImagePath);
                        }
                    }
                    _context.FacultyHousingOptions.RemoveRange(housingToDelete);
                    deletedHousingCount = housingToDelete.Count;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ تم حذف {deletedHousingCount} خيار سكن");
                }

                // ========== 6. ✅ تحديث أو إضافة البيانات الجديدة (المنطق الجديد) ==========
                Console.WriteLine("🔄 تحديث وإضافة البيانات الجديدة...");
                var result = await UpdateOrAddInstituteData(instituteId, model);

                // تحديث HasHousing بناءً على السكن الجديد المضاف
                var remainingHousingCount = await _context.FacultyHousingOptions
                    .CountAsync(h => h.FacultyId == instituteId && !h.IsDeleted);
                institute.HasHousing = remainingHousingCount > 0;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                Console.WriteLine($"🎉 تم تحديث المعهد بنجاح!");

                return Ok(new
                {
                    success = true,
                    message = "تم تحديث المعهد بنجاح",
                    instituteId,
                    statistics = result,
                    deletedSpecializationsCount,
                    deletedJobsCount,
                    deletedYearsCount,
                    deletedMediaCount,
                    deletedHousingCount,
                    housingAdded = model.HousingOptionNames?.Count(h => !string.IsNullOrEmpty(h)) ?? 0
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ فشل التحديث: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "فشل تحديث المعهد",
                    error = ex.Message
                });
            }
        }
        // تحديث أو إضافة البيانات الجديدة (المنطق الجديد - نفس الكليات)
        private async Task<object> UpdateOrAddInstituteData(int instituteId, InstituteFormModel model)
        {
            var specCount = 0;
            var yearCount = 0;
            var semesterCount = 0;
            var materialCount = 0;
            var sectionCount = 0;
            var jobCount = 0;
            var housingCount = 0;

            // 1. إضافة التخصصات الجديدة
            if (model.SpecializationNames != null)
            {
                for (int i = 0; i < model.SpecializationNames.Count; i++)
                {
                    if (string.IsNullOrEmpty(model.SpecializationNames[i])) continue;

                    var spec = new Specialization
                    {
                        Name = model.SpecializationNames[i],
                        YearsNumber = i < model.SpecializationYearsNumbers.Count ?
                            model.SpecializationYearsNumbers[i] : 2,
                        Description = i < model.SpecializationDescriptions.Count ?
                            model.SpecializationDescriptions[i] : "",
                        AcademicQualification = "",
                        FacultyId = instituteId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Specializations.Add(spec);
                    specCount++;
                }

                if (specCount > 0)
                {
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ تم إضافة {specCount} تخصص جديد");
                }
            }

            // 2. إضافة فرص العمل الجديدة
            if (model.JobOpportunityNames != null)
            {
                for (int i = 0; i < model.JobOpportunityNames.Count; i++)
                {
                    if (string.IsNullOrEmpty(model.JobOpportunityNames[i])) continue;

                    var job = new JobOpportunity
                    {
                        Name = model.JobOpportunityNames[i],
                        FacultyId = instituteId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.JobOpportunities.Add(job);
                    jobCount++;
                }

                if (jobCount > 0)
                {
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ تم إضافة {jobCount} فرصة عمل جديدة");
                }
            }

            // 3. إضافة السكن الجديد
            if (model.HousingOptionNames != null)
            {
                for (int i = 0; i < model.HousingOptionNames.Count; i++)
                {
                    if (string.IsNullOrEmpty(model.HousingOptionNames[i])) continue;

                    string imagePath = null;
                    if (model.HousingOptionImages != null && i < model.HousingOptionImages.Count &&
                        model.HousingOptionImages[i] != null && model.HousingOptionImages[i].Length > 0)
                    {
                        imagePath = await SaveFile(model.HousingOptionImages[i], "housing-options");
                        Console.WriteLine($"📁 تم رفع صورة السكن: {imagePath}");
                    }

                    string phoneNumber = model.HousingOptionPhoneNumbers != null && i < model.HousingOptionPhoneNumbers.Count
                        ? model.HousingOptionPhoneNumbers[i]
                        : "";

                    string description = model.HousingOptionDescriptions != null && i < model.HousingOptionDescriptions.Count
                        ? model.HousingOptionDescriptions[i]
                        : "";

                    var housing = new FacultyHousingOption
                    {
                        Name = model.HousingOptionNames[i],
                        PhoneNumber = phoneNumber,
                        Description = description,
                        ImagePath = imagePath,
                        FacultyId = instituteId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.FacultyHousingOptions.Add(housing);
                    housingCount++;
                }

                if (housingCount > 0)
                {
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ تم إضافة {housingCount} خيار سكن جديد");
                }
            }

            // 4. تحديث أو إضافة السنوات (نفس لوجيك الكليات - باستخدام YearNumbers)
            if (model.YearNumbers != null && model.YearNumbers.Count > 0)
            {
                for (int i = 0; i < model.YearNumbers.Count; i++)
                {
                    var yearNumber = model.YearNumbers[i];
                    var yearName = i < model.YearNames.Count ? model.YearNames[i] : $"السنة {yearNumber}";
                    var hasSpecialization = i < model.YearHasSpecialization.Count && model.YearHasSpecialization[i];

                    // 🔍 البحث عن سنة موجودة بنفس الرقم
                    var existingYear = await _context.StudyPlanYears
                        .FirstOrDefaultAsync(y => y.FacultyId == instituteId && y.YearNumber == yearNumber && !y.IsDeleted);

                    StudyPlanYear studyPlanYear;

                    if (existingYear != null)
                    {
                        // ✅ تحديث السنة الموجودة
                        studyPlanYear = existingYear;
                        studyPlanYear.YearName = yearName;
                        studyPlanYear.Type = hasSpecialization ? "Specialized" : "General";
                        studyPlanYear.UpdatedAt = DateTime.UtcNow;

                        Console.WriteLine($"✅ تم تحديث السنة الموجودة: السنة رقم {yearNumber} - {studyPlanYear.YearName}");

                        // مسح البيانات القديمة المرتبطة بالسنة
                        await ClearYearData(studyPlanYear.Id);
                    }
                    else
                    {
                        // ✅ إضافة سنة جديدة
                        // التحقق من عدم وجود اسم مكرر
                        string finalYearName = yearName;
                        int suffix = 1;
                        while (await _context.StudyPlanYears.AnyAsync(y => y.FacultyId == instituteId && y.YearName == finalYearName))
                        {
                            finalYearName = $"{yearName} ({suffix})";
                            suffix++;
                        }

                        studyPlanYear = new StudyPlanYear
                        {
                            YearName = finalYearName,
                            YearNumber = yearNumber,
                            Type = hasSpecialization ? "Specialized" : "General",
                            FacultyId = instituteId,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.StudyPlanYears.Add(studyPlanYear);
                        await _context.SaveChangesAsync();

                        Console.WriteLine($"✅ تم إضافة سنة جديدة: السنة رقم {yearNumber} - {studyPlanYear.YearName}");
                    }

                    var studyPlanYearId = studyPlanYear.Id;
                    yearCount++;

                    // 📁 إضافة الوسائط لهذه السنة
                    if (model.MediaTypes != null && model.MediaYearIndices != null)
                    {
                        for (int mediaIndex = 0; mediaIndex < model.MediaYearIndices.Count; mediaIndex++)
                        {
                            if (model.MediaYearIndices[mediaIndex] == i &&
                                mediaIndex < model.MediaTypes.Count &&
                                !string.IsNullOrEmpty(model.MediaTypes[mediaIndex]))
                            {
                                string mediaLink = "";

                                if (model.MediaFiles != null && mediaIndex < model.MediaFiles.Count &&
                                    model.MediaFiles[mediaIndex] != null && model.MediaFiles[mediaIndex].Length > 0)
                                {
                                    mediaLink = await SaveFile(model.MediaFiles[mediaIndex], "studyplan-media");
                                    Console.WriteLine($"📁 تم رفع ملف جديد: {mediaLink}");
                                }

                                var media = new StudyPlanMedia
                                {
                                    MediaType = model.MediaTypes[mediaIndex],
                                    MediaLink = mediaLink,
                                    VisitLink = model.MediaVisitLinks?[mediaIndex] ?? "",
                                    StudyPlanYearId = studyPlanYearId,
                                    CreatedAt = DateTime.UtcNow
                                };

                                _context.StudyPlanMedia.Add(media);
                                Console.WriteLine($"✅ تم إضافة وسائط للسنة رقم {yearNumber}");
                            }
                        }
                    }

                    // 📚 إضافة الفصول والمواد العامة
                    if (model.SemesterNames != null && model.SemesterYearIndices != null)
                    {
                        for (int semIndex = 0; semIndex < model.SemesterYearIndices.Count; semIndex++)
                        {
                            if (model.SemesterYearIndices[semIndex] == i &&
                                semIndex < model.SemesterNames.Count &&
                                !string.IsNullOrEmpty(model.SemesterNames[semIndex]))
                            {
                                semesterCount++;

                                if (model.SemesterMaterialNames != null &&
                                    model.SemesterMaterialSemesterIndices != null)
                                {
                                    for (int matIndex = 0; matIndex < model.SemesterMaterialSemesterIndices.Count; matIndex++)
                                    {
                                        if (model.SemesterMaterialSemesterIndices[matIndex] == semIndex &&
                                            matIndex < model.SemesterMaterialNames.Count &&
                                            !string.IsNullOrEmpty(model.SemesterMaterialNames[matIndex]))
                                        {
                                            string materialCode = matIndex < model.SemesterMaterialCodes.Count &&
                                                               !string.IsNullOrEmpty(model.SemesterMaterialCodes[matIndex])
                                                ? model.SemesterMaterialCodes[matIndex]
                                                : $"MAT-{yearNumber}-{semIndex + 1}-{matIndex + 1}";

                                            var material = new AcademicMaterial
                                            {
                                                Name = model.SemesterMaterialNames[matIndex],
                                                Code = materialCode,
                                                Semester = semIndex + 1,
                                                Type = "Mandatory",
                                                CreditHours = 3,
                                                StudyPlanYearId = studyPlanYearId,
                                                StudyPlanSectionId = null,
                                                CreatedAt = DateTime.UtcNow
                                            };

                                            _context.AcademicMaterials.Add(material);
                                            materialCount++;
                                            Console.WriteLine($"📚 تم إضافة مادة عامة: {material.Name} (السنة {yearNumber}, الفصل {material.Semester})");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 📂 إضافة الأقسام والمواد المتخصصة
                    if (model.SectionNames != null && model.SectionYearIndices != null)
                    {
                        for (int secIndex = 0; secIndex < model.SectionYearIndices.Count; secIndex++)
                        {
                            if (model.SectionYearIndices[secIndex] == i &&
                                secIndex < model.SectionNames.Count &&
                                !string.IsNullOrEmpty(model.SectionNames[secIndex]))
                            {
                                string sectionCode = secIndex < model.SectionCodes.Count &&
                                                   !string.IsNullOrEmpty(model.SectionCodes[secIndex])
                                    ? model.SectionCodes[secIndex]
                                    : $"SEC-{yearNumber}-{secIndex + 1}";

                                var section = new StudyPlanSection
                                {
                                    Name = model.SectionNames[secIndex],
                                    Code = sectionCode,
                                    StudyPlanYearId = studyPlanYearId,
                                    CreatedAt = DateTime.UtcNow
                                };

                                _context.StudyPlanSections.Add(section);
                                await _context.SaveChangesAsync();

                                var sectionId = section.Id;
                                sectionCount++;
                                Console.WriteLine($"✅ تم إنشاء قسم جديد: {section.Name} (السنة {yearNumber})");

                                if (model.SectionMaterialNames != null &&
                                    model.SectionMaterialSectionIndices != null)
                                {
                                    for (int matIndex = 0; matIndex < model.SectionMaterialSectionIndices.Count; matIndex++)
                                    {
                                        if (model.SectionMaterialSectionIndices[matIndex] == secIndex &&
                                            matIndex < model.SectionMaterialNames.Count &&
                                            !string.IsNullOrEmpty(model.SectionMaterialNames[matIndex]))
                                        {
                                            int materialSemester = 1;
                                            if (model.SectionMaterialSemesterIndices != null &&
                                                matIndex < model.SectionMaterialSemesterIndices.Count)
                                            {
                                                materialSemester = model.SectionMaterialSemesterIndices[matIndex] + 1;
                                            }

                                            string materialCode = matIndex < model.SectionMaterialCodes.Count &&
                                                               !string.IsNullOrEmpty(model.SectionMaterialCodes[matIndex])
                                                ? model.SectionMaterialCodes[matIndex]
                                                : $"MAT-SEC-{yearNumber}-{secIndex + 1}-{matIndex + 1}";

                                            var material = new AcademicMaterial
                                            {
                                                Name = model.SectionMaterialNames[matIndex],
                                                Code = materialCode,
                                                Semester = materialSemester,
                                                Type = "Mandatory",
                                                CreditHours = 3,
                                                StudyPlanYearId = null,
                                                StudyPlanSectionId = sectionId,
                                                CreatedAt = DateTime.UtcNow
                                            };

                                            _context.AcademicMaterials.Add(material);
                                            materialCount++;
                                            Console.WriteLine($"📚 تم إضافة مادة متخصصة: {material.Name} (القسم: {section.Name})");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                }
            }

            return new
            {
                specializations = specCount,
                studyYears = yearCount,
                semesters = semesterCount,
                sections = sectionCount,
                materials = materialCount,
                jobOpportunities = jobCount,
                housingOptions = housingCount
            };
        }
        // حذف جميع البيانات المرتبطة بسنة دراسية (قبل تحديثها)
        private async Task ClearYearData(int studyPlanYearId)
        {
            Console.WriteLine($"🗑️ حذف البيانات القديمة للسنة ID: {studyPlanYearId}");

            

            // 2. حذف مواد الأقسام
            var sectionMaterials = await _context.AcademicMaterials
                .Where(m => m.StudyPlanSection != null && m.StudyPlanSection.StudyPlanYearId == studyPlanYearId)
                .ToListAsync();
            _context.AcademicMaterials.RemoveRange(sectionMaterials);

            // 3. حذف الأقسام
            var sections = await _context.StudyPlanSections
                .Where(s => s.StudyPlanYearId == studyPlanYearId)
                .ToListAsync();
            _context.StudyPlanSections.RemoveRange(sections);

            // 4. حذف مواد السنة العامة
            var yearMaterials = await _context.AcademicMaterials
                .Where(m => m.StudyPlanYearId == studyPlanYearId)
                .ToListAsync();
            _context.AcademicMaterials.RemoveRange(yearMaterials);

            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ تم حذف جميع البيانات القديمة للسنة");
        }

        // 🆕 حذف سنة دراسية مع كل محتوياتها (للمعهد)
        private async Task DeleteStudyYearWithAllContents(int studyYearId, int instituteId)
        {
            Console.WriteLine($"🗑️ حذف السنة الدراسية ID: {studyYearId}");

            var studyYear = await _context.StudyPlanYears
                .FirstOrDefaultAsync(y => y.Id == studyYearId && y.FacultyId == instituteId);

            if (studyYear == null)
            {
                Console.WriteLine($"⚠️ السنة {studyYearId} غير موجودة");
                return;
            }

            try
            {
                // 1. حذف وسائط السنة
                var mediaList = await _context.StudyPlanMedia
                    .Where(m => m.StudyPlanYearId == studyYearId)
                    .ToListAsync();

                foreach (var media in mediaList)
                {
                    if (!string.IsNullOrEmpty(media.MediaLink))
                    {
                        await DeleteFile(media.MediaLink);
                    }
                }
                _context.StudyPlanMedia.RemoveRange(mediaList);

                // 2. حذف مواد الأقسام
                var sectionMaterials = await _context.AcademicMaterials
                    .Where(m => m.StudyPlanSection != null && m.StudyPlanSection.StudyPlanYearId == studyYearId)
                    .ToListAsync();
                _context.AcademicMaterials.RemoveRange(sectionMaterials);

                // 3. حذف الأقسام
                var sections = await _context.StudyPlanSections
                    .Where(s => s.StudyPlanYearId == studyYearId)
                    .ToListAsync();
                _context.StudyPlanSections.RemoveRange(sections);

                // 4. حذف مواد السنة
                var yearMaterials = await _context.AcademicMaterials
                    .Where(m => m.StudyPlanYearId == studyYearId)
                    .ToListAsync();
                _context.AcademicMaterials.RemoveRange(yearMaterials);

                // 5. حذف السنة
                _context.StudyPlanYears.Remove(studyYear);

                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ تم حذف السنة {studyYear.YearName} بالكامل");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطأ في حذف السنة: {ex.Message}");
                throw;
            }
        }
        // 🆕 إضافة بيانات المعهد الجديدة (كل اللي في الموديل)
        private async Task<object> AddInstituteDataExactlyLikeAdd(int instituteId, InstituteFormModel model)
        {
            var specCount = 0;
            var yearCount = 0;
            var semesterCount = 0;
            var materialCount = 0;
            var sectionCount = 0;
            var jobCount = 0;
            var housingCount = 0;

            // 1. إضافة التخصصات (كل اللي في الموديل)
            if (model.SpecializationNames != null)
            {
                for (int i = 0; i < model.SpecializationNames.Count; i++)
                {
                    if (string.IsNullOrEmpty(model.SpecializationNames[i])) continue;

                    var spec = new Specialization
                    {
                        Name = model.SpecializationNames[i],
                        YearsNumber = i < model.SpecializationYearsNumbers.Count ?
                            model.SpecializationYearsNumbers[i] : 2,
                        Description = i < model.SpecializationDescriptions.Count ?
                            model.SpecializationDescriptions[i] : "",
                        AcademicQualification = "",
                        FacultyId = instituteId,
                        CreatedAt = DateTime.UtcNow
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

            // 2. إضافة فرص العمل (كل اللي في الموديل)
            if (model.JobOpportunityNames != null)
            {
                for (int i = 0; i < model.JobOpportunityNames.Count; i++)
                {
                    if (string.IsNullOrEmpty(model.JobOpportunityNames[i])) continue;

                    var job = new JobOpportunity
                    {
                        Name = model.JobOpportunityNames[i],
                        FacultyId = instituteId,
                        CreatedAt = DateTime.UtcNow
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

            // 3. إضافة السكن (كل اللي في الموديل)
            if (model.HousingOptionNames != null)
            {
                for (int i = 0; i < model.HousingOptionNames.Count; i++)
                {
                    if (string.IsNullOrEmpty(model.HousingOptionNames[i])) continue;

                    string imagePath = null;
                    if (model.HousingOptionImages != null && i < model.HousingOptionImages.Count &&
                        model.HousingOptionImages[i] != null && model.HousingOptionImages[i].Length > 0)
                    {
                        imagePath = await SaveFile(model.HousingOptionImages[i], "housing-options");
                    }

                    string phoneNumber = model.HousingOptionPhoneNumbers != null && i < model.HousingOptionPhoneNumbers.Count
                        ? model.HousingOptionPhoneNumbers[i]
                        : "";

                    string description = model.HousingOptionDescriptions != null && i < model.HousingOptionDescriptions.Count
                        ? model.HousingOptionDescriptions[i]
                        : "";

                    var housing = new FacultyHousingOption
                    {
                        Name = model.HousingOptionNames[i],
                        PhoneNumber = phoneNumber,
                        Description = description,
                        ImagePath = imagePath,
                        FacultyId = instituteId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.FacultyHousingOptions.Add(housing);
                    housingCount++;
                }

                if (housingCount > 0)
                {
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ تم إضافة {housingCount} خيار سكن");
                }
            }

            // 4. إضافة السنوات (كل اللي في الموديل)
            if (model.YearNames != null && model.YearNames.Count > 0)
            {
                for (int yearIndex = 0; yearIndex < model.YearNames.Count; yearIndex++)
                {
                    if (string.IsNullOrEmpty(model.YearNames[yearIndex])) continue;

                    string yearName = model.YearNames[yearIndex];
                    int suffix = 1;
                    string finalYearName = yearName;

                    while (await _context.StudyPlanYears.AnyAsync(y => y.FacultyId == instituteId && y.YearName == finalYearName))
                    {
                        finalYearName = $"{yearName} ({suffix})";
                        suffix++;
                    }

                    var studyPlanYear = new StudyPlanYear
                    {
                        YearName = finalYearName,
                        YearNumber = yearIndex + 1,
                        Type = (yearIndex < model.YearHasSpecialization.Count &&
                               model.YearHasSpecialization[yearIndex]) ? "Specialized" : "General",
                        FacultyId = instituteId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.StudyPlanYears.Add(studyPlanYear);
                    await _context.SaveChangesAsync();

                    var studyPlanYearId = studyPlanYear.Id;
                    yearCount++;

                    Console.WriteLine($"✅ تم إنشاء السنة: {studyPlanYear.YearName}");

                    // إضافة الوسائط (كل اللي في الموديل)
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
                                    Console.WriteLine($"📁 تم رفع ملف جديد: {mediaLink}");
                                }

                                var media = new StudyPlanMedia
                                {
                                    MediaType = model.MediaTypes[mediaIndex],
                                    MediaLink = mediaLink,
                                    VisitLink = model.MediaVisitLinks?[mediaIndex] ?? "",
                                    StudyPlanYearId = studyPlanYearId,
                                    CreatedAt = DateTime.UtcNow
                                };

                                _context.StudyPlanMedia.Add(media);
                                Console.WriteLine($"✅ تم إضافة وسائط للسنة {yearIndex + 1}");
                            }
                        }
                    }

                    // إضافة الفصول والمواد
                    if (model.SemesterNames != null && model.SemesterYearIndices != null)
                    {
                        for (int semIndex = 0; semIndex < model.SemesterYearIndices.Count; semIndex++)
                        {
                            if (model.SemesterYearIndices[semIndex] == yearIndex &&
                                semIndex < model.SemesterNames.Count &&
                                !string.IsNullOrEmpty(model.SemesterNames[semIndex]))
                            {
                                semesterCount++;

                                if (model.SemesterMaterialNames != null &&
                                    model.SemesterMaterialSemesterIndices != null)
                                {
                                    for (int matIndex = 0; matIndex < model.SemesterMaterialSemesterIndices.Count; matIndex++)
                                    {
                                        if (model.SemesterMaterialSemesterIndices[matIndex] == semIndex &&
                                            matIndex < model.SemesterMaterialNames.Count &&
                                            !string.IsNullOrEmpty(model.SemesterMaterialNames[matIndex]))
                                        {
                                            string materialCode = matIndex < model.SemesterMaterialCodes.Count &&
                                                               !string.IsNullOrEmpty(model.SemesterMaterialCodes[matIndex])
                                                ? model.SemesterMaterialCodes[matIndex]
                                                : $"MAT-{yearIndex + 1}-{semIndex + 1}-{matIndex + 1}";

                                            var material = new AcademicMaterial
                                            {
                                                Name = model.SemesterMaterialNames[matIndex],
                                                Code = materialCode,
                                                Semester = semIndex + 1,
                                                Type = "Mandatory",
                                                CreditHours = 3,
                                                StudyPlanYearId = studyPlanYearId,
                                                StudyPlanSectionId = null,
                                                CreatedAt = DateTime.UtcNow
                                            };

                                            _context.AcademicMaterials.Add(material);
                                            materialCount++;
                                            Console.WriteLine($"📚 تم إضافة مادة: {material.Name}");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // إضافة الأقسام والمواد
                    if (model.SectionNames != null && model.SectionYearIndices != null)
                    {
                        for (int secIndex = 0; secIndex < model.SectionYearIndices.Count; secIndex++)
                        {
                            if (model.SectionYearIndices[secIndex] == yearIndex &&
                                secIndex < model.SectionNames.Count &&
                                !string.IsNullOrEmpty(model.SectionNames[secIndex]))
                            {
                                string sectionCode = secIndex < model.SectionCodes.Count &&
                                                   !string.IsNullOrEmpty(model.SectionCodes[secIndex])
                                    ? model.SectionCodes[secIndex]
                                    : $"SEC-{yearIndex + 1}-{secIndex + 1}";

                                var section = new StudyPlanSection
                                {
                                    Name = model.SectionNames[secIndex],
                                    Code = sectionCode,
                                    StudyPlanYearId = studyPlanYearId,
                                    CreatedAt = DateTime.UtcNow
                                };

                                _context.StudyPlanSections.Add(section);
                                await _context.SaveChangesAsync();

                                var sectionId = section.Id;
                                sectionCount++;
                                Console.WriteLine($"✅ تم إنشاء قسم: {section.Name}");

                                if (model.SectionMaterialNames != null &&
                                    model.SectionMaterialSectionIndices != null)
                                {
                                    for (int matIndex = 0; matIndex < model.SectionMaterialSectionIndices.Count; matIndex++)
                                    {
                                        if (model.SectionMaterialSectionIndices[matIndex] == secIndex &&
                                            matIndex < model.SectionMaterialNames.Count &&
                                            !string.IsNullOrEmpty(model.SectionMaterialNames[matIndex]))
                                        {
                                            int materialSemester = 1;
                                            if (model.SectionMaterialSemesterIndices != null &&
                                                matIndex < model.SectionMaterialSemesterIndices.Count)
                                            {
                                                materialSemester = model.SectionMaterialSemesterIndices[matIndex] + 1;
                                            }

                                            string materialCode = matIndex < model.SectionMaterialCodes.Count &&
                                                               !string.IsNullOrEmpty(model.SectionMaterialCodes[matIndex])
                                                ? model.SectionMaterialCodes[matIndex]
                                                : $"MAT-SEC-{yearIndex + 1}-{secIndex + 1}-{matIndex + 1}";

                                            var material = new AcademicMaterial
                                            {
                                                Name = model.SectionMaterialNames[matIndex],
                                                Code = materialCode,
                                                Semester = materialSemester,
                                                Type = "Mandatory",
                                                CreditHours = 3,
                                                StudyPlanYearId = null,
                                                StudyPlanSectionId = sectionId,
                                                CreatedAt = DateTime.UtcNow
                                            };

                                            _context.AcademicMaterials.Add(material);
                                            materialCount++;
                                            Console.WriteLine($"📚 تم إضافة مادة للقسم: {material.Name}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                }
            }

            return new
            {
                specializations = specCount,
                studyYears = yearCount,
                semesters = semesterCount,
                sections = sectionCount,
                materials = materialCount,
                jobOpportunities = jobCount,
                housingOptions = housingCount
            };
        }
        [HttpPut("{instituteId}/expenses-coordination")]
        public async Task<IActionResult> UpdateExpensesAndCoordinationRequired(
    int instituteId,

    [FromBody] UpdateExpensesCoordinationRequiredDto model)
        {
            try
            {
                // التحقق من صحة البيانات
                if (model == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "يرجى إدخال المصروفات والتنسيق بشكل صحيح"
                    });
                }

                var faculty = await _context.Faculties
                    .FirstOrDefaultAsync(f => f.Id == instituteId && !f.IsDeleted);

                if (faculty == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الكلية غير موجودة"
                    });
                }

                faculty.Expenses = model.Expenses;
                faculty.Coordination = model.Coordination;
                faculty.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "تم تحديث المصروفات والتنسيق بنجاح",
                    data = new
                    {
                        instituteId,
                        facultyName = faculty.NameArabic,
                        expenses = faculty.Expenses,
                        coordination = faculty.Coordination
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        #endregion
        #region Delete Institute

        [HttpDelete("{instituteId}")]
        public async Task<IActionResult> DeleteInstitute(int instituteId)
        {
            try
            {
                Console.WriteLine($"🗑️ محاولة حذف المعهد {instituteId}");

                var institute = await _context.Faculties
                    .Include(i => i.SpecializationList)
                    .Include(i => i.StudyPlanYears)
                        .ThenInclude(y => y.AcademicMaterials)
                    .Include(i => i.StudyPlanYears)
                        .ThenInclude(y => y.Sections)
                            .ThenInclude(s => s.AcademicMaterials)
                    .Include(i => i.StudyPlanYears)
                        .ThenInclude(y => y.StudyPlanMedia)
                    .Include(i => i.JobOpportunities)
                    .Include(i => i.FacultyHousingOption)
                    .FirstOrDefaultAsync(i => i.Id == instituteId && i.UniversityId == null && !i.IsDeleted);

                if (institute == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "المعهد غير موجود"
                    });
                }

                // Soft Delete
                institute.IsDeleted = true;
                institute.DeletedAt = DateTime.UtcNow;
                institute.UpdatedAt = DateTime.UtcNow;

                // حذف التخصصات
                foreach (var spec in institute.SpecializationList.Where(s => !s.IsDeleted))
                {
                    spec.IsDeleted = true;
                    spec.DeletedAt = DateTime.UtcNow;
                }

                // حذف سنوات الدراسة والبيانات المرتبطة
                foreach (var year in institute.StudyPlanYears.Where(y => !y.IsDeleted))
                {
                    year.IsDeleted = true;
                    year.DeletedAt = DateTime.UtcNow;

                    foreach (var material in year.AcademicMaterials.Where(m => !m.IsDeleted))
                    {
                        material.IsDeleted = true;
                        material.DeletedAt = DateTime.UtcNow;
                    }

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

                    foreach (var media in year.StudyPlanMedia.Where(m => !m.IsDeleted))
                    {
                        media.IsDeleted = true;
                        media.DeletedAt = DateTime.UtcNow;
                    }
                }

                // حذف فرص العمل
                foreach (var job in institute.JobOpportunities.Where(j => !j.IsDeleted))
                {
                    job.IsDeleted = true;
                    job.DeletedAt = DateTime.UtcNow;
                }

                // حذف خيارات السكن
                foreach (var housing in institute.FacultyHousingOption.Where(h => !h.IsDeleted))
                {
                    housing.IsDeleted = true;
                    housing.DeletedAt = DateTime.UtcNow;

                    // حذف الصور المرتبطة بخيارات السكن إذا وجدت
                    if (!string.IsNullOrEmpty(housing.ImagePath))
                    {
                        await DeleteFile(housing.ImagePath);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "تم حذف المعهد بنجاح",
                    instituteId,
                    instituteName = institute.NameArabic
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطأ في الحذف: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء حذف المعهد"
                });
            }
        }

        #endregion

        #region Housing Options Management

        //[HttpPost("{instituteId}/housing-options")]
        //public async Task<IActionResult> AddHousingOptionsToInstitute(int instituteId, [FromBody] List<HousingOptionFormModel> housingOptions)
        //{
        //    try
        //    {
        //        var institute = await _context.Faculties
        //            .FirstOrDefaultAsync(i => i.Id == instituteId && i.UniversityId == null && !i.IsDeleted);

        //        if (institute == null)
        //        {
        //            return NotFound(new { success = false, message = "المعهد غير موجود" });
        //        }

        //        await AddHousingOptions(instituteId, housingOptions);
        //        institute.HasHousing = true;
        //        await _context.SaveChangesAsync();

        //        return Ok(new
        //        {
        //            success = true,
        //            message = "تم إضافة خيارات السكن بنجاح",
        //            count = housingOptions.Count
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, message = ex.Message });
        //    }
        //}

        //[HttpPut("{instituteId}/housing-options/{housingOptionId}")]
        //public async Task<IActionResult> UpdateHousingOption(int instituteId, int housingOptionId, [FromBody] HousingOptionFormModel model)
        //{
        //    try
        //    {
        //        var housingOption = await _context.FacultyHousingOptions
        //            .FirstOrDefaultAsync(h => h.Id == housingOptionId && h.FacultyId == instituteId && !h.IsDeleted);

        //        if (housingOption == null)
        //        {
        //            return NotFound(new { success = false, message = "خيار السكن غير موجود" });
        //        }

        //        housingOption.Name = model.Name;
        //        housingOption.PhoneNumber = model.PhoneNumber;
        //        housingOption.Description = model.Description;

        //        // تحديث الصورة إذا تم توفير مسار جديد
        //        if (!string.IsNullOrEmpty(model.ImagePath))
        //        {
        //            // حذف الصورة القديمة
        //            if (!string.IsNullOrEmpty(housingOption.ImagePath))
        //            {
        //                await DeleteFile(housingOption.ImagePath);
        //            }
        //            housingOption.ImagePath = model.ImagePath;
        //        }

        //        housingOption.UpdatedAt = DateTime.UtcNow;

        //        await _context.SaveChangesAsync();

        //        return Ok(new
        //        {
        //            success = true,
        //            message = "تم تحديث خيار السكن بنجاح"
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, message = ex.Message });
        //    }
        //}

        //[HttpDelete("{instituteId}/housing-options/{housingOptionId}")]
        //public async Task<IActionResult> DeleteHousingOption(int instituteId, int housingOptionId)
        //{
        //    try
        //    {
        //        var housingOption = await _context.FacultyHousingOptions
        //            .FirstOrDefaultAsync(h => h.Id == housingOptionId && h.FacultyId == instituteId && !h.IsDeleted);

        //        if (housingOption == null)
        //        {
        //            return NotFound(new { success = false, message = "خيار السكن غير موجود" });
        //        }

        //        // Soft delete
        //        housingOption.IsDeleted = true;
        //        housingOption.DeletedAt = DateTime.UtcNow;

        //        // حذف الصورة
        //        if (!string.IsNullOrEmpty(housingOption.ImagePath))
        //        {
        //            await DeleteFile(housingOption.ImagePath);
        //        }

        //        await _context.SaveChangesAsync();

        //        // التحقق إذا كان المعهد لا يزال لديه خيارات سكن
        //        var hasAnyHousing = await _context.FacultyHousingOptions
        //            .AnyAsync(h => h.FacultyId == instituteId && !h.IsDeleted);

        //        if (!hasAnyHousing)
        //        {
        //            var institute = await _context.Faculties.FindAsync(instituteId);
        //            if (institute != null)
        //            {
        //                institute.HasHousing = false;
        //                await _context.SaveChangesAsync();
        //            }
        //        }

        //        return Ok(new
        //        {
        //            success = true,
        //            message = "تم حذف خيار السكن بنجاح"
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, message = ex.Message });
        //    }
        //}

        //[HttpGet("{instituteId}/housing-options")]
        //public async Task<IActionResult> GetInstituteHousingOptions(int instituteId)
        //{
        //    try
        //    {
        //        var housingOptions = await _context.FacultyHousingOptions
        //            .Where(h => h.FacultyId == instituteId && !h.IsDeleted)
        //            .Select(h => new
        //            {
        //                h.Id,
        //                h.Name,
        //                h.PhoneNumber,
        //                h.Description,
        //                h.ImagePath,
        //                h.CreatedAt,
        //                h.UpdatedAt
        //            })
        //            .ToListAsync();

        //        return Ok(new
        //        {
        //            success = true,
        //            data = housingOptions
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, message = ex.Message });
        //    }
        //}

        #endregion

        #region Statistics and Search

        //[HttpGet("stats")]
        //public async Task<IActionResult> GetInstitutesStats()
        //{
        //    try
        //    {
        //        var stats = await _context.Faculties
        //            .Where(f => f.UniversityId == null && !f.IsDeleted)
        //            .GroupBy(f => 1)
        //            .Select(g => new
        //            {
        //                TotalInstitutes = g.Count(),
        //                TotalStudents = g.Sum(f => f.StudentsNumber ?? 0),
        //                TotalPrograms = g.Sum(f => f.ProgramsNumber ?? 0),

        //                // إحصائيات حسب النوع
        //                ByType = g.Where(f => !string.IsNullOrEmpty(f.Type))
        //                    .GroupBy(f => f.Type)
        //                    .Select(t => new
        //                    {
        //                        Type = t.Key,
        //                        Count = t.Count()
        //                    })
        //                    .ToList(),

        //                // إحصائيات حسب وجود السكن
        //                InstitutesWithHousing = g.Count(f => f.HasHousing == true),
        //                InstitutesWithoutHousing = g.Count(f => f.HasHousing == false || f.HasHousing == null),

                        

        //                // إجمالي التخصصات
        //                TotalSpecializations = g.SelectMany(f => f.SpecializationList.Where(s => !s.IsDeleted)).Count(),

        //                // إجمالي خيارات السكن
        //                TotalHousingOptions = g.SelectMany(f => f.FacultyHousingOption.Where(h => !h.IsDeleted)).Count(),

        //                // أحدث المعاهد
        //                LatestInstitutes = g.OrderByDescending(f => f.CreatedAt)
        //                    .Take(5)
        //                    .Select(f => new
        //                    {
        //                        f.Id,
        //                        f.NameArabic,
        //                        f.Type,
        //                        f.CreatedAt
        //                    })
        //                    .ToList()
        //            })
        //            .FirstOrDefaultAsync();

        //        return Ok(new
        //        {
        //            success = true,
        //            data = stats ?? new
        //            {
        //                TotalInstitutes = 0,
        //                TotalStudents = 0,
        //                TotalPrograms = 0,
        //                ByType = new List<object>(),
        //                InstitutesWithHousing = 0,
        //                InstitutesWithoutHousing = 0,
        //                TotalSpecializations = 0,
        //                TotalHousingOptions = 0,
        //                LatestInstitutes = new List<object>()
        //            }
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, message = ex.Message });
        //    }
        //}

        [HttpGet("search")]
        public async Task<IActionResult> SearchInstitutes(
            [FromQuery] string? search = null,
            [FromQuery] string? type = null,
            [FromQuery] string? instituteType = null,
            [FromQuery] bool? hasHousing = null,
            [FromQuery] bool? requireAcceptanceTests = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _context.Faculties
                    .Where(f => f.UniversityId == null && !f.IsDeleted)
                    .Include(f => f.FacultyHousingOption.Where(h => !h.IsDeleted))
                    .AsQueryable();

                // تطبيق عوامل البحث
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(f =>
                        f.NameArabic.Contains(search) ||
                        f.NameEnglish.Contains(search) ||
                        f.Description.Contains(search) );
                }

                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(f => f.Type == type);
                }

                

                if (hasHousing.HasValue)
                {
                    query = query.Where(f => f.HasHousing == hasHousing.Value);
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
                    
                    default:
                        query = query.OrderBy(f => f.NameArabic);
                        break;
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var institutes = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(f => new
                    {
                        f.Id,
                        f.NameArabic,
                        f.NameEnglish,
                        f.Description,
                        f.Type,
                        f.HasHousing,
                        f.StudentsNumber,
                        f.DurationOfStudy,
                        f.ProgramsNumber,
                        f.RequireAcceptanceTests,
                        f.Expenses,
                        f.Coordination,
                       
                        f.ImageUrl,
                        HousingOptionsCount = f.FacultyHousingOption.Count,
                        f.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        SearchQuery = search,
                        Filters = new
                        {
                            Type = type,
                            InstituteType = instituteType,
                            HasHousing = hasHousing,
                            RequireAcceptanceTests = requireAcceptanceTests
                        },
                        Institutes = institutes,
                        Pagination = new
                        {
                            CurrentPage = page,
                            PageSize = pageSize,
                            TotalCount = totalCount,
                            TotalPages = totalPages
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task AddHousingOptionsFromLists(int instituteId, InstituteFormModel model)
        {
            int housingCount = 0;

            for (int i = 0; i < model.HousingOptionNames.Count; i++)
            {
                if (string.IsNullOrEmpty(model.HousingOptionNames[i])) continue;

                string imagePath = null;

                // Handle image upload if provided
                if (model.HousingOptionImages != null && i < model.HousingOptionImages.Count &&
                    model.HousingOptionImages[i] != null && model.HousingOptionImages[i].Length > 0)
                {
                    imagePath = await SaveFile(model.HousingOptionImages[i], "housing-options");
                    Console.WriteLine($"📁 تم رفع صورة السكن {i + 1}: {imagePath}");
                }

                string phoneNumber = model.HousingOptionPhoneNumbers != null && i < model.HousingOptionPhoneNumbers.Count
                    ? model.HousingOptionPhoneNumbers[i]
                    : "";

                string description = model.HousingOptionDescriptions != null && i < model.HousingOptionDescriptions.Count
                    ? model.HousingOptionDescriptions[i]
                    : "";

                var housingOption = new FacultyHousingOption
                {
                    Name = model.HousingOptionNames[i],
                    PhoneNumber = phoneNumber,
                    Description = description,
                    ImagePath = imagePath,
                    FacultyId = instituteId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.FacultyHousingOptions.Add(housingOption);
                housingCount++;
            }

            if (housingCount > 0)
            {
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ تم إضافة {housingCount} خيار سكن");
            }
        }

        private async Task<int> AddSpecializations(int instituteId, InstituteFormModel model)
        {
            var specCount = 0;
            for (int i = 0; i < model.SpecializationNames.Count; i++)
            {
                if (string.IsNullOrEmpty(model.SpecializationNames[i])) continue;

                var spec = new Specialization
                {
                    Name = model.SpecializationNames[i],
                    YearsNumber = i < model.SpecializationYearsNumbers.Count ?
                        model.SpecializationYearsNumbers[i] : 2,
                    Description = i < model.SpecializationDescriptions.Count ?
                        model.SpecializationDescriptions[i] : "",
                    AcademicQualification = "",
                    FacultyId = instituteId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Specializations.Add(spec);
                specCount++;
            }

            if (specCount > 0)
            {
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ تم إضافة {specCount} تخصص");
            }

            return specCount;
        }

        private async Task<(int yearCount, int semesterCount, int sectionCount, int materialCount)> AddStudyPlan(int instituteId, InstituteFormModel model)
        {
            int yearCount = 0, semesterCount = 0, materialCount = 0, sectionCount = 0;

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
                        FacultyId = instituteId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.StudyPlanYears.Add(studyPlanYear);
                    await _context.SaveChangesAsync();

                    var studyPlanYearId = studyPlanYear.Id;
                    yearCount++;

                    // إضافة الوسائط
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
                                        VisitLink = model.MediaVisitLinks?[mediaIndex] ?? "",
                                        StudyPlanYearId = studyPlanYearId,
                                        CreatedAt = DateTime.UtcNow
                                    };

                                    _context.StudyPlanMedia.Add(media);
                                }
                            }
                        }
                    }

                    // إضافة الفصول الدراسية والمواد
                    if (model.SemesterNames != null && model.SemesterYearIndices != null)
                    {
                        for (int semIndex = 0; semIndex < model.SemesterYearIndices.Count; semIndex++)
                        {
                            if (model.SemesterYearIndices[semIndex] == yearIndex &&
                                semIndex < model.SemesterNames.Count &&
                                !string.IsNullOrEmpty(model.SemesterNames[semIndex]))
                            {
                                semesterCount++;

                                if (model.SemesterMaterialNames != null &&
                                    model.SemesterMaterialSemesterIndices != null)
                                {
                                    for (int matIndex = 0; matIndex < model.SemesterMaterialSemesterIndices.Count; matIndex++)
                                    {
                                        if (model.SemesterMaterialSemesterIndices[matIndex] == semIndex &&
                                            matIndex < model.SemesterMaterialNames.Count &&
                                            !string.IsNullOrEmpty(model.SemesterMaterialNames[matIndex]))
                                        {
                                            string materialCode = matIndex < model.SemesterMaterialCodes.Count &&
                                                               !string.IsNullOrEmpty(model.SemesterMaterialCodes[matIndex])
                                                ? model.SemesterMaterialCodes[matIndex]
                                                : $"MAT-{yearIndex + 1}-{semIndex + 1}-{matIndex + 1}";

                                            var material = new AcademicMaterial
                                            {
                                                Name = model.SemesterMaterialNames[matIndex],
                                                Code = materialCode,
                                                Semester = semIndex + 1,
                                                Type = "Mandatory",
                                                CreditHours = 3,
                                                StudyPlanYearId = studyPlanYearId,
                                                StudyPlanSectionId = null,
                                                CreatedAt = DateTime.UtcNow
                                            };

                                            _context.AcademicMaterials.Add(material);
                                            materialCount++;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // إضافة الأقسام
                    if (model.SectionNames != null && model.SectionYearIndices != null)
                    {
                        for (int secIndex = 0; secIndex < model.SectionYearIndices.Count; secIndex++)
                        {
                            if (model.SectionYearIndices[secIndex] == yearIndex &&
                                secIndex < model.SectionNames.Count &&
                                !string.IsNullOrEmpty(model.SectionNames[secIndex]))
                            {
                                string sectionCode = secIndex < model.SectionCodes.Count &&
                                                   !string.IsNullOrEmpty(model.SectionCodes[secIndex])
                                    ? model.SectionCodes[secIndex]
                                    : $"SEC-{yearIndex + 1}-{secIndex + 1}";

                                var section = new StudyPlanSection
                                {
                                    Name = model.SectionNames[secIndex],
                                    Code = sectionCode,
                                    StudyPlanYearId = studyPlanYearId,
                                    CreatedAt = DateTime.UtcNow
                                };

                                _context.StudyPlanSections.Add(section);
                                await _context.SaveChangesAsync();

                                var sectionId = section.Id;
                                sectionCount++;

                                // إضافة مواد القسم
                                if (model.SectionMaterialNames != null &&
                                    model.SectionMaterialSectionIndices != null)
                                {
                                    for (int matIndex = 0; matIndex < model.SectionMaterialSectionIndices.Count; matIndex++)
                                    {
                                        if (model.SectionMaterialSectionIndices[matIndex] == secIndex &&
                                            matIndex < model.SectionMaterialNames.Count &&
                                            !string.IsNullOrEmpty(model.SectionMaterialNames[matIndex]))
                                        {
                                            int materialSemester = 1;
                                            if (model.SectionMaterialSemesterIndices != null &&
                                                matIndex < model.SectionMaterialSemesterIndices.Count)
                                            {
                                                materialSemester = model.SectionMaterialSemesterIndices[matIndex] + 1;
                                            }

                                            string materialCode = matIndex < model.SectionMaterialCodes.Count &&
                                                               !string.IsNullOrEmpty(model.SectionMaterialCodes[matIndex])
                                                ? model.SectionMaterialCodes[matIndex]
                                                : $"MAT-SEC-{yearIndex + 1}-{secIndex + 1}-{matIndex + 1}";

                                            var material = new AcademicMaterial
                                            {
                                                Name = model.SectionMaterialNames[matIndex],
                                                Code = materialCode,
                                                Semester = materialSemester,
                                                Type = "Mandatory",
                                                CreditHours = 3,
                                                StudyPlanYearId = null,
                                                StudyPlanSectionId = sectionId,
                                                CreatedAt = DateTime.UtcNow
                                            };

                                            _context.AcademicMaterials.Add(material);
                                            materialCount++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                }
            }

            return (yearCount, semesterCount, sectionCount, materialCount);
        }

        private async Task<int> AddJobOpportunities(int instituteId, InstituteFormModel model)
        {
            var jobCount = 0;
            for (int i = 0; i < model.JobOpportunityNames.Count; i++)
            {
                if (string.IsNullOrEmpty(model.JobOpportunityNames[i])) continue;

                var job = new JobOpportunity
                {
                    Name = model.JobOpportunityNames[i],
                    FacultyId = instituteId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.JobOpportunities.Add(job);
                jobCount++;
            }

            if (jobCount > 0)
            {
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ تم إضافة {jobCount} فرصة عمل");
            }

            return jobCount;
        }

        private async Task HardDeleteAllInstituteDataExceptMediaAndHousing(int instituteId)
        {
            Console.WriteLine($"🔥 حذف فعلي لجميع بيانات المعهد {instituteId} (باستثناء الوسائط والسكن)");

            try
            {
                // 1. حذف مواد الأقسام
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM AcademicMaterials WHERE StudyPlanSectionId IN (SELECT Id FROM StudyPlanSections WHERE StudyPlanYearId IN (SELECT Id FROM StudyPlanYears WHERE FacultyId = {0}))",
                    instituteId);

                // 2. حذف مواد السنوات المباشرة
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM AcademicMaterials WHERE StudyPlanYearId IN (SELECT Id FROM StudyPlanYears WHERE FacultyId = {0})",
                    instituteId);

                // 3. حذف الأقسام
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM StudyPlanSections WHERE StudyPlanYearId IN (SELECT Id FROM StudyPlanYears WHERE FacultyId = {0})",
                    instituteId);

                // 4. حذف السنوات الدراسية (ملاحظة: لا نحذف StudyPlanMedia هنا)
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM StudyPlanYears WHERE FacultyId = {0}",
                    instituteId);

                // 5. حذف التخصصات
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM Specializations WHERE FacultyId = {0}",
                    instituteId);

                // 6. حذف فرص العمل
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM JobOpportunities WHERE FacultyId = {0}",
                    instituteId);

                Console.WriteLine("✅ تم الحذف الفعلي لجميع البيانات (باستثناء الوسائط والسكن)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطأ في الحذف: {ex.Message}");
                throw;
            }
        }
        private async Task<string> SaveFile(IFormFile file, string folder)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return null;

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

                var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", folder);

                if (!Directory.Exists(uploadsPath))
                    Directory.CreateDirectory(uploadsPath);

                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                Console.WriteLine($"تم حفظ الملف: {fileName}");
                return $"/uploads/{folder}/{fileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطأ في حفظ الملف: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> DeleteFile(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return false;

                var fileName = Path.GetFileName(fileUrl);
                var folder = fileUrl.Contains("institute-images") ? "institute-images" :
                            fileUrl.Contains("studyplan-media") ? "studyplan-media" : "";

                if (string.IsNullOrEmpty(folder))
                    return false;

                var filePath = Path.Combine(_env.WebRootPath, "uploads", folder, fileName);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    Console.WriteLine($"✅ تم حذف الملف: {filePath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطأ في حذف الملف: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
    public class InstituteFormModel
    {
        // المعلومات الأساسية (موجودة في Faculty)
        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        public string NameArabic { get; set; }

        public string NameEnglish { get; set; }

        [Required(ErrorMessage = "الوصف مطلوب")]
        public string Description { get; set; }

        [Required(ErrorMessage = "نوع المعهد مطلوب")]
        public string Type { get; set; } // "معهد حكومي", "معهد خاص"

        public int? StudentsNumber { get; set; }
        public string DurationOfStudy { get; set; }
        public int? ProgramsNumber { get; set; }
        public bool RequireAcceptanceTests { get; set; }
        public int Expenses { get; set; }
        public int Coordination { get; set; }
        public string? GroupLink { get; set; }
        public string? InstitutePageLink { get; set; }

        public string Address { get; set; }
        public string? DescriptionOfStudyPlan { get; set; }
        public IFormFile? Image { get; set; }

        // حقول إضافية للمعهد
        public bool HasHousing { get; set; }



        // خيارات السكن - كقوائم مفهرسة (مثل باقي الحقول)
        public List<string>? HousingOptionNames { get; set; }
        public List<string>? HousingOptionPhoneNumbers { get; set; }
        public List<string>? HousingOptionDescriptions { get; set; }
        public List<IFormFile>? HousingOptionImages { get; set; }
        public List<int>? DeletedHousingIds { get; set; }  // ✅ IDs السكن المحذوف


        // التخصصات
        public List<string>? SpecializationNames { get; set; }
        public List<int>? SpecializationYearsNumbers { get; set; }
        public List<string>? SpecializationDescriptions { get; set; }

        // خطة الدراسة - السنوات (أضف YearNumbers)
        public List<int>? YearNumbers { get; set; }      // الأرقام الحقيقية للسنوات (1,2,3,4...)
        public List<string>? YearNames { get; set; }
        public List<bool>? YearHasSpecialization { get; set; }

        // الوسائط
        public List<string>? MediaTypes { get; set; }
        public List<IFormFile>? MediaFiles { get; set; }
        public List<string>? MediaVisitLinks { get; set; }
        public List<int>? MediaYearIndices { get; set; }

        // الفصول الدراسية
        public List<string>? SemesterNames { get; set; }
        public List<int>? SemesterYearIndices { get; set; }

        // مواد الفصول
        public List<string>? SemesterMaterialNames { get; set; }
        public List<string>? SemesterMaterialCodes { get; set; }
        public List<int>? SemesterMaterialSemesterIndices { get; set; }

        // الأقسام
        public List<string>? SectionNames { get; set; }
        public List<string>? SectionCodes { get; set; }
        public List<int>? SectionYearIndices { get; set; }

        // مواد الأقسام
        public List<string>? SectionMaterialNames { get; set; }
        public List<string>? SectionMaterialCodes { get; set; }
        public List<int>? SectionMaterialSectionIndices { get; set; }
        public List<int>? SectionMaterialSemesterIndices { get; set; }

        // فرص العمل
        public List<string>? JobOpportunityNames { get; set; }

        // 🆕 للحذف فقط
        public List<int>? DeletedSpecializationIds { get; set; }
        public List<int>? DeletedJobOpportunityIds { get; set; }
        public List<int>? DeletedYearIds { get; set; }
        public List<int>? DeletedMediaIds { get; set; }
    }

    public class HousingOptionFormModel
    {
        [Required(ErrorMessage = "اسم السكن مطلوب")]
        public string Name { get; set; }

        [Phone(ErrorMessage = "رقم الهاتف غير صحيح")]
        public string PhoneNumber { get; set; }

        public string Description { get; set; }

        // Change this from string to IFormFile
        public IFormFile Image { get; set; }

        
    }
}
