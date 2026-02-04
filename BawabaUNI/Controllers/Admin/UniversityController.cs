using BawabaUNI.Models.Data;
using BawabaUNI.Models.DTOs.Admin.University;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/[controller]")]
    [ApiController]
    public class UniversityController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public UniversityController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpPost("add")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AddUniversity([FromForm] UniversityFormModel model)
        {
            try
            {
                // تحقق من البيانات الأساسية
                if (string.IsNullOrEmpty(model.Type) ||
                    string.IsNullOrEmpty(model.NameArabic) ||
                    string.IsNullOrEmpty(model.NameEnglish))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "البيانات الأساسية للجامعة مطلوبة"
                    });
                }

                // 1. رفع صورة الجامعة (بدون تحقق قوي مؤقتاً)
                string universityImagePath = "/uploads/default/university.jpg";
                if (model.UniversityImage != null && model.UniversityImage.Length > 0)
                {
                    universityImagePath = await SaveFile(model.UniversityImage, "universities");
                }

                // 2. إنشاء الجامعة مع القيم الأساسية فقط أولاً
                var university = new University
                {
                    Type = model.Type,
                    NameArabic = model.NameArabic,
                    NameEnglish = model.NameEnglish,
                    IsTrending = model.IsTrending,
                    Description = model.Description ?? "لا يوجد وصف",
                    FoundingYear = model.FoundingYear > 0 ? model.FoundingYear : 2000,
                    UniversityImage = universityImagePath,
                    Email = model.Email ?? "no-email@example.com",
                    PhoneNumber = model.PhoneNumber ?? "0000000000",
                    Address = model.Address ?? "لا يوجد عنوان",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // 3. القيم الاختيارية
                if (model.StudentsNumber.HasValue && model.StudentsNumber > 0)
                    university.StudentsNumber = model.StudentsNumber;

                if (!string.IsNullOrEmpty(model.Location))
                    university.Location = model.Location;

                if (model.GlobalRanking.HasValue && model.GlobalRanking > 0)
                    university.GlobalRanking = model.GlobalRanking;

                if (!string.IsNullOrEmpty(model.Website))
                    university.Website = model.Website;

                if (!string.IsNullOrEmpty(model.FacebookPage))
                    university.FacebookPage = model.FacebookPage;

                if (!string.IsNullOrEmpty(model.City))
                    university.City = model.City;

                if (!string.IsNullOrEmpty(model.Governate))
                    university.Governate = model.Governate;

                if (!string.IsNullOrEmpty(model.PostalCode))
                    university.PostalCode = model.PostalCode;

                _context.Universities.Add(university);

                // 4. حاول حفظ الجامعة أولاً وحدها
                try
                {
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"University saved with ID: {university.Id}");
                }
                catch (DbUpdateException ex)
                {
                    Console.WriteLine($"Database Error: {ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"Inner: {ex.InnerException.Message}");

                    return StatusCode(500, new
                    {
                        success = false,
                        message = "فشل حفظ الجامعة في قاعدة البيانات",
                        error = ex.Message,
                        detail = ex.InnerException?.Message
                    });
                }

                var universityId = university.Id;
                var housingCount = 0;
                var documentCount = 0;

                // 5. أضف السكن (إذا نجح حفظ الجامعة)
                if (model.HousingNames != null && model.HousingNames.Any())
                {
                    for (int i = 0; i < model.HousingNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(model.HousingNames[i]))
                            continue;

                        var housingImagePath = "";

                        if (model.HousingImages != null && i < model.HousingImages.Count &&
                            model.HousingImages[i] != null && model.HousingImages[i].Length > 0)
                        {
                            housingImagePath = await SaveFile(model.HousingImages[i], "housing");
                        }

                        var housing = new HousingOption
                        {
                            Name = model.HousingNames[i],
                            PhoneNumber = model.HousingPhones?[i] ?? "0000000000",
                            Description = model.HousingDescriptions?[i] ?? "لا يوجد وصف",
                            ImagePath = housingImagePath,
                            UniversityId = universityId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.HousingOptions.Add(housing);
                        housingCount++;
                    }
                }

                // 6. أضف المستندات
                if (model.DocumentNames != null && model.DocumentNames.Any())
                {
                    for (int i = 0; i < model.DocumentNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(model.DocumentNames[i]))
                            continue;

                        var document = new DocumentRequired
                        {
                            DocumentName = model.DocumentNames[i],
                            Description = model.DocumentDescriptions?[i] ?? "",
                            UniversityId = universityId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.DocumentsRequired.Add(document);
                        documentCount++;
                    }
                }

                // 7. حفظ السكن والمستندات
                if (housingCount > 0 || documentCount > 0)
                {
                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateException ex)
                    {
                        Console.WriteLine($"Error saving housing/documents: {ex.Message}");
                        // لكن الجامعة تم حفظها بنجاح
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = "تم إضافة الجامعة بنجاح",
                    universityId,
                    housingCount,
                    documentCount,
                    universityImage = universityImagePath
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Error: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner StackTrace: {ex.InnerException.StackTrace}");
                }

                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ غير متوقع",
                    error = ex.Message,
                    detail = ex.InnerException?.Message
                });
            }
        }

        private async Task<string> SaveFile(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                return null;

            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!validExtensions.Contains(extension))
                return null;

            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", folder);

            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{folder}/{fileName}";
        }

        [HttpPut("update/{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateUniversityComplete(int id, [FromForm] UpdateCompleteUniversityRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. البحث عن الجامعة
                var university = await _context.Universities
                    .Include(u => u.HousingOptions)
                    .Include(u => u.DocumentsRequired)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (university == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الجامعة غير موجودة"
                    });
                }

                // 2. تحديث بيانات الجامعة
                university.Type = request.Type;
                university.NameArabic = request.NameArabic;
                university.NameEnglish = request.NameEnglish;
                university.IsTrending = request.IsTrending;
                university.Description = request.Description;
                university.FoundingYear = request.FoundingYear;
                university.StudentsNumber = request.StudentsNumber;
                university.Location = request.Location;
                university.GlobalRanking = request.GlobalRanking;
                university.Email = request.Email;
                university.Website = request.Website;
                university.PhoneNumber = request.PhoneNumber;
                university.FacebookPage = request.FacebookPage;
                university.Address = request.Address;
                university.City = request.City;
                university.Governate = request.Governate;
                university.PostalCode = request.PostalCode;
                university.UpdatedAt = DateTime.UtcNow;

                // 3. تحديث صورة الجامعة إذا أرسلت
                if (request.UniversityImage != null && request.UniversityImage.Length > 0)
                {
                    var newImagePath = await SaveFile(request.UniversityImage, "universities");
                    if (!string.IsNullOrEmpty(newImagePath))
                    {
                        DeleteOldFile(university.UniversityImage);
                        university.UniversityImage = newImagePath;
                    }
                }

                await _context.SaveChangesAsync();

                // 4. التعامل مع السكن
                var housingCount = 0;
                if (request.HousingNames != null && request.HousingNames.Any())
                {
                    // حذف السكن القديم
                    var existingHousing = await _context.HousingOptions
                        .Where(h => h.UniversityId == id)
                        .ToListAsync();

                    foreach (var housing in existingHousing)
                    {
                        DeleteOldFile(housing.ImagePath);
                    }
                    _context.HousingOptions.RemoveRange(existingHousing);

                    // إضافة السكن الجديد
                    for (int i = 0; i < request.HousingNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(request.HousingNames[i]))
                            continue;

                        var housingImagePath = "";
                        if (request.HousingImages != null && i < request.HousingImages.Count &&
                            request.HousingImages[i] != null && request.HousingImages[i].Length > 0)
                        {
                            housingImagePath = await SaveFile(request.HousingImages[i], "housing");
                        }

                        var housing = new HousingOption
                        {
                            Name = request.HousingNames[i],
                            PhoneNumber = request.HousingPhones?[i] ?? "0000000000",
                            Description = request.HousingDescriptions?[i] ?? "لا يوجد وصف",
                            ImagePath = housingImagePath,
                            UniversityId = id,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.HousingOptions.Add(housing);
                        housingCount++;
                    }
                }

                // 5. التعامل مع المستندات
                var documentCount = 0;
                if (request.DocumentNames != null && request.DocumentNames.Any())
                {
                    // حذف المستندات القديمة
                    var existingDocuments = await _context.DocumentsRequired
                        .Where(d => d.UniversityId == id)
                        .ToListAsync();
                    _context.DocumentsRequired.RemoveRange(existingDocuments);

                    // إضافة المستندات الجديدة
                    for (int i = 0; i < request.DocumentNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(request.DocumentNames[i]))
                            continue;

                        var document = new DocumentRequired
                        {
                            DocumentName = request.DocumentNames[i],
                            Description = request.DocumentDescriptions?[i] ?? "",
                            UniversityId = id,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.DocumentsRequired.Add(document);
                        documentCount++;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "تم تحديث الجامعة بنجاح",
                    universityId = id,
                    housingCount,
                    documentCount
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء التحديث",
                    error = ex.Message,
                    detail = ex.InnerException?.Message
                });
            }
        }



        // DTO للتعديل الكامل
        public class UpdateCompleteUniversityRequest
        {
            // بيانات الجامعة الأساسية
            [Required] public string Type { get; set; }
            [Required] public string NameArabic { get; set; }
            [Required] public string NameEnglish { get; set; }
            public bool IsTrending { get; set; }
            [Required] public string Description { get; set; }
            [Range(1000, 2100)] public int FoundingYear { get; set; }
            public int? StudentsNumber { get; set; }
            public string? Location { get; set; }
            public int? GlobalRanking { get; set; }
            public IFormFile? UniversityImage { get; set; } // اختياري للتحديث
            [Required][EmailAddress] public string Email { get; set; }
            public string? Website { get; set; }
            [Required] public string PhoneNumber { get; set; }
            public string? FacebookPage { get; set; }
            [Required] public string Address { get; set; }
            public string? City { get; set; }
            public string? Governate { get; set; }
            public string? PostalCode { get; set; }

            // السكن (arrays) - ستستبدل القديم بالجديد
            public List<string>? HousingNames { get; set; }
            public List<string>? HousingPhones { get; set; }
            public List<string>? HousingDescriptions { get; set; }
            public List<IFormFile>? HousingImages { get; set; }

            // المستندات (arrays) - ستستبدل القديم بالجديد
            public List<string>? DocumentNames { get; set; }
            public List<string>? DocumentDescriptions { get; set; }
        }

        private void DeleteOldFile(string filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && filePath.StartsWith("/uploads/"))
                {
                    var fullPath = Path.Combine(_env.WebRootPath, filePath.TrimStart('/'));

                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
            }
            catch
            {
                // تجاهل الأخطاء في حذف الملفات
            }
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUniversity(int id)
        {
            try
            {
                // جلب الجامعة مع العلاقات
                var university = await _context.Universities
                    .Include(u => u.HousingOptions)
                    .Include(u => u.DocumentsRequired)
                    .Include(u => u.Faculties)
                    .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

                if (university == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الجامعة غير موجودة"
                    });
                }

                // تطبيق Soft Delete (لكن المستخدم يرى أنه حذف)
                university.IsDeleted = true;
                university.DeletedAt = DateTime.UtcNow;
                university.UpdatedAt = DateTime.UtcNow;

                // نطبق Soft Delete على العلاقات أيضاً (اختياري)
                foreach (var housing in university.HousingOptions)
                {
                    housing.IsDeleted = true;
                    housing.DeletedAt = DateTime.UtcNow;
                    housing.UpdatedAt = DateTime.UtcNow;
                }

                foreach (var document in university.DocumentsRequired)
                {
                    document.IsDeleted = true;
                    document.DeletedAt = DateTime.UtcNow;
                    document.UpdatedAt = DateTime.UtcNow;
                }

                foreach (var faculty in university.Faculties)
                {
                    faculty.IsDeleted = true;
                    faculty.DeletedAt = DateTime.UtcNow;
                    faculty.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                // إرجاع رسالة "حذف" للمستخدم
                return Ok(new
                {
                    success = true,
                    message = "تم حذف الجامعة بنجاح",
                    data = new
                    {
                        universityId = id,
                        universityName = university.NameArabic,
                        action = "deleted",
                        timestamp = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء حذف الجامعة",
                    error = ex.Message
                });
            }
        }
        [HttpDelete("hard/{id}")]
        //[Authorize(Roles = "Admin,SuperAdmin")] // فقط للمسؤولين
        public async Task<IActionResult> HardDeleteUniversity(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var university = await _context.Universities
                    .Include(u => u.HousingOptions)
                    .Include(u => u.DocumentsRequired)
                    .Include(u => u.Faculties)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (university == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الجامعة غير موجودة"
                    });
                }

                // حذف الملفات
                DeleteOldFile(university.UniversityImage);
                foreach (var housing in university.HousingOptions)
                    DeleteOldFile(housing.ImagePath);

                // حذف من قاعدة البيانات
                _context.Universities.Remove(university);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "تم الحذف الفعلي للجامعة بنجاح",
                    deletedId = id
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء الحذف الفعلي",
                    error = ex.Message
                });
            }
        }

        [HttpGet("{id}/delete-info")]
        public async Task<IActionResult> GetUniversityDeleteInfo(int id)
        {
            try
            {
                var university = await _context.Universities
                    .Include(u => u.HousingOptions)
                    .Include(u => u.DocumentsRequired)
                    .Include(u => u.Faculties)
                    .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

                if (university == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الجامعة غير موجودة"
                    });
                }

                // حساب عدد الطلاب (افتراضي أو من قاعدة البيانات)
                var studentCount = university.StudentsNumber ?? 0;

                // حساب عدد الكورسات
                var courseCount = university.Faculties;

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        
                        university.NameArabic,
                        university.NameEnglish,
                        university.Type,
                        FacultiesCount = university.Faculties?.Count ?? 0,
                        StudentsCount = studentCount,
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء جلب البيانات",
                    error = ex.Message
                });
            }
        }

        [HttpGet("paged")]
        public async Task<ActionResult<PagedResult<UniversitySimpleDto>>> GetUniversitiesPaged(
     [FromQuery] string? name = null,
     [FromQuery] string? type = null,
     [FromQuery] string? sortBy = "NameAsc",
     [FromQuery] int pageNumber = 1,
     [FromQuery] int pageSize = 10)
        {
            // التحقق من صحة المدخلات
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            // Base query
            var query = _context.Universities
                .Include(u => u.Faculties)
                .Include(u => u.HousingOptions)
                .Where(u => !u.IsDeleted)
                .AsQueryable();

            // فلترة بالاسم (عربي أو إنجليزي)
            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(u =>
                    u.NameArabic.Contains(name) ||
                    u.NameEnglish.Contains(name));
            }

            // فلترة بالنوع فقط
            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(u => u.Type == type);
            }

            // التطبيق على الترتيب حسب sortBy
            query = sortBy.ToLower() switch
            {
                "namedesc" => query.OrderByDescending(u => u.NameArabic),
                "trendingfirst" => query.OrderByDescending(u => u.IsTrending).ThenBy(u => u.NameArabic),
                "oldestfirst" => query.OrderBy(u => u.FoundingYear).ThenBy(u => u.NameArabic),
                "newestfirst" => query.OrderByDescending(u => u.FoundingYear).ThenBy(u => u.NameArabic),
                "highestrating" => query.OrderBy(u => u.GlobalRanking).ThenBy(u => u.NameArabic),
                "moststudents" => query.OrderByDescending(u => u.StudentsNumber).ThenBy(u => u.NameArabic),
                "leaststudents" => query.OrderBy(u => u.StudentsNumber).ThenBy(u => u.NameArabic),
                "mostrequested" => query.OrderByDescending(u => u.Faculties.Count).ThenBy(u => u.NameArabic),
                "leastrequested" => query.OrderBy(u => u.Faculties.Count).ThenBy(u => u.NameArabic),
                _ => query.OrderBy(u => u.NameArabic) // Default: NameAsc
            };

            // حساب العدد الإجمالي
            var totalCount = await query.CountAsync();

            // تطبيق الـ Paging
            var universities = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UniversitySimpleDto
                {
                    NameArabic = u.NameArabic,
                    NameEnglish = u.NameEnglish,
                    Description = u.Description,
                    StudentsNumber = u.StudentsNumber,
                    AvailableHousingCount = u.HousingOptions
                        .Where(h =>  !h.IsDeleted)
                        .Count(),
                    FacultiesCount = u.Faculties
                        .Where(f => !f.IsDeleted)
                        .Count(),
                    FoundingYear = u.FoundingYear,
                    Location = u.Location,
                    City = u.City,
                    Governate = u.Governate,
                    Address = u.Address
                })
                .ToListAsync();

            // إنشاء الـ Response مع الـ Paging
            var result = new PagedResult<UniversitySimpleDto>
            {
                Data = universities,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return Ok(result);
        }

    }


   
   
}