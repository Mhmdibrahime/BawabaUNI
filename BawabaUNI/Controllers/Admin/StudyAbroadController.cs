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
    [Route("api/Admin/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class StudyAbroadController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public StudyAbroadController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        #region Add
        [HttpPost("add")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AddStudyAbroad([FromForm] StudyAbroadFormModel model)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrEmpty(model.NameArabic) ||
                    string.IsNullOrEmpty(model.NameEnglish) ||
                    string.IsNullOrEmpty(model.Description))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "البيانات الأساسية مطلوبة"
                    });
                }

                // Upload image if provided
                string imageUrl = "/uploads/default/studyabroad.jpg";
                if (model.ImageUrl != null && model.ImageUrl.Length > 0)
                {
                    imageUrl = await SaveFile(model.ImageUrl, "studyabroad");
                }

                // Create StudyAbroad entity
                var studyAbroad = new StudyAbroad
                {
                    NameArabic = model.NameArabic,
                    NameEnglish = model.NameEnglish,
                    Description = model.Description,
                    Licenses = model.Licenses ?? "لا يوجد",
                    Partnership = model.Partnership ?? "لا يوجد",
                    Services = model.Services ?? "لا يوجد",
                    Email = model.Email ?? "no-email@example.com",
                    Website = model.Website,
                    PhoneNumber = model.PhoneNumber ?? "0000000000",
                    WhatsAppNumber = model.WhatsAppNumber,
                    FacebookPage = model.FacebookPage,
                    ImageUrl = imageUrl,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.StudyAbroads.Add(studyAbroad);
                await _context.SaveChangesAsync();

                var studyAbroadId = studyAbroad.Id;
                var housingCount = 0;
                var documentCount = 0;
                var facultyCount = 0;

                // Add Housing Options
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

                        var housing = new HousingOptionForAbroad
                        {
                            Name = model.HousingNames[i],
                            PhoneNumber = model.HousingPhones?[i] ?? "0000000000",
                            Description = model.HousingDescriptions?[i] ?? "لا يوجد وصف",
                            ImagePath = housingImagePath,
                            StudyAbroadId = studyAbroadId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.HousingOptionsForAbroad.Add(housing);
                        housingCount++;
                    }
                }

                // Add Required Documents
                if (model.DocumentNames != null && model.DocumentNames.Any())
                {
                    for (int i = 0; i < model.DocumentNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(model.DocumentNames[i]))
                            continue;

                        var document = new DocumentRequiredForStudyAbroad
                        {
                            DocumentName = model.DocumentNames[i],
                            Description = model.DocumentDescriptions?[i] ?? "",
                            StudyAbroadId = studyAbroadId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.DocumentsRequiredForStudyAbroad.Add(document);
                        documentCount++;
                    }
                }

                // Add Faculties
                if (model.FacultyNames != null && model.FacultyNames.Any())
                {
                    for (int i = 0; i < model.FacultyNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(model.FacultyNames[i]))
                            continue;

                        var faculty = new FacultyForAbroad
                        {
                            NameArabic = model.FacultyNames[i],
                            NameEnglish = model.FacultyEnglishNames?[i] ?? model.FacultyNames[i],
                            Expenses = model.FacultyExpenses?[i] ?? 0,
                            Coordination = model.FacultyCoordination?[i] ?? 0,
                            ImageUrl = model.FacultyImages?[i] != null ? await SaveFile(model.FacultyImages[i], "faculties") : null,
                            StudyAbroadId = studyAbroadId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.FacultiesForAbroad.Add(faculty);
                        facultyCount++;
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "تم إضافة مكتب الدراسة بالخارج بنجاح",
                    studyAbroadId,
                    housingCount,
                    documentCount,
                    facultyCount,
                    imageUrl
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ غير متوقع",
                    error = ex.Message,
                    detail = ex.InnerException?.Message
                });
            }
        }
        #endregion

        #region Get All (Paged)
        [HttpGet("paged")]
        public async Task<ActionResult<PagedResult<StudyAbroadSimpleDto>>> GetStudyAbroadPaged(
            [FromQuery] string? name = null,
            [FromQuery] string? sortBy = "NameAsc",
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            // Validate inputs
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            // Base query
            var query = _context.StudyAbroads
                .Include(s => s.Faculties)
                .Include(s => s.HousingOptions)
                .Include(s => s.DocumentsRequired)
                .Where(s => !s.IsDeleted)
                .AsQueryable();

            // Filter by name
            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(s =>
                    s.NameArabic.Contains(name) ||
                    s.NameEnglish.Contains(name));
            }

            // Apply sorting
            query = sortBy.ToLower() switch
            {
                "namedesc" => query.OrderByDescending(s => s.NameArabic),
                "oldestfirst" => query.OrderBy(s => s.CreatedAt),
                "newestfirst" => query.OrderByDescending(s => s.CreatedAt),
                _ => query.OrderBy(s => s.NameArabic) // Default: NameAsc
            };

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply paging
            var studyAbroads = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new StudyAbroadSimpleDto
                {
                    Id = s.Id,
                    NameArabic = s.NameArabic,
                    NameEnglish = s.NameEnglish,
                    Description = s.Description,
                    ImageUrl = s.ImageUrl,
                    PhoneNumber = s.PhoneNumber,
                    Email = s.Email,
                    FacultiesCount = s.Faculties.Count(f => !f.IsDeleted),
                    HousingCount = s.HousingOptions.Count(h => !h.IsDeleted),
                    DocumentsCount = s.DocumentsRequired.Count(d => !d.IsDeleted),
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            // Create response
            var result = new PagedResult<StudyAbroadSimpleDto>
            {
                Data = studyAbroads,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return Ok(result);
        }
        #endregion

        #region Get By Id
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStudyAbroadById(int id)
        {
            try
            {
                var studyAbroad = await _context.StudyAbroads
                    .Include(s => s.HousingOptions.Where(h => !h.IsDeleted))
                    .Include(s => s.DocumentsRequired.Where(d => !d.IsDeleted))
                    .Include(s => s.Faculties.Where(f => !f.IsDeleted))
                    .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

                if (studyAbroad == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "مكتب الدراسة بالخارج غير موجود"
                    });
                }

                var studyAbroadDto = new StudyAbroadDetailDto
                {
                    Id = studyAbroad.Id,
                    NameArabic = studyAbroad.NameArabic,
                    NameEnglish = studyAbroad.NameEnglish,
                    Description = studyAbroad.Description,
                    Licenses = studyAbroad.Licenses,
                    Partnership = studyAbroad.Partnership,
                    Services = studyAbroad.Services,
                    ImageUrl = !string.IsNullOrEmpty(studyAbroad.ImageUrl)
                        ? $"{Request.Scheme}://{Request.Host}{studyAbroad.ImageUrl}"
                        : null,
                    Email = studyAbroad.Email,
                    Website = studyAbroad.Website,
                    PhoneNumber = studyAbroad.PhoneNumber,
                    WhatsAppNumber = studyAbroad.WhatsAppNumber,
                    FacebookPage = studyAbroad.FacebookPage,
                    CreatedAt = studyAbroad.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,

                    HousingOptions = studyAbroad.HousingOptions.Select(h => new HousingOptionForAbroadDto
                    {
                        Id = h.Id,
                        Name = h.Name,
                        PhoneNumber = h.PhoneNumber,
                        Description = h.Description,
                        ImagePath = !string.IsNullOrEmpty(h.ImagePath)
                            ? $"{Request.Scheme}://{Request.Host}{h.ImagePath}"
                            : null,
                        CreatedAt = h.CreatedAt
                    }).ToList(),

                    DocumentsRequired = studyAbroad.DocumentsRequired.Select(d => new DocumentRequiredForAbroadDto
                    {
                        Id = d.Id,
                        DocumentName = d.DocumentName,
                        Description = d.Description,
                        CreatedAt = d.CreatedAt
                    }).ToList(),

                    Faculties = studyAbroad.Faculties.Select(f => new FacultyForAbroadDto
                    {
                        Id = f.Id,
                        NameArabic = f.NameArabic,
                        NameEnglish = f.NameEnglish,
                        Expenses = f.Expenses,
                        Coordination = f.Coordination,
                        ImageUrl = !string.IsNullOrEmpty(f.ImageUrl)
                            ? $"{Request.Scheme}://{Request.Host}{f.ImageUrl}"
                            : null,
                        CreatedAt = f.CreatedAt
                    }).ToList()
                };

                return Ok(new
                {
                    success = true,
                    data = studyAbroadDto
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
        #endregion

        #region Update (with Expenses and Coordination)
        [HttpPut("update/{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateStudyAbroad(int id, [FromForm] UpdateStudyAbroadRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Find the study abroad entity
                var studyAbroad = await _context.StudyAbroads
                    .Include(s => s.HousingOptions)
                    .Include(s => s.DocumentsRequired)
                    .Include(s => s.Faculties)
                    .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

                if (studyAbroad == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "مكتب الدراسة بالخارج غير موجود"
                    });
                }

                // Update main entity
                studyAbroad.NameArabic = request.NameArabic;
                studyAbroad.NameEnglish = request.NameEnglish;
                studyAbroad.Description = request.Description;
                studyAbroad.Licenses = request.Licenses ?? "لا يوجد";
                studyAbroad.Partnership = request.Partnership ?? "لا يوجد";
                studyAbroad.Services = request.Services ?? "لا يوجد";
                studyAbroad.Email = request.Email ?? "no-email@example.com";
                studyAbroad.Website = request.Website;
                studyAbroad.PhoneNumber = request.PhoneNumber ?? "0000000000";
                studyAbroad.WhatsAppNumber = request.WhatsAppNumber;
                studyAbroad.FacebookPage = request.FacebookPage;
                studyAbroad.UpdatedAt = DateTime.UtcNow;

                // Update image if provided
                if (request.ImageUrl != null && request.ImageUrl.Length > 0)
                {
                    var newImagePath = await SaveFile(request.ImageUrl, "studyabroad");
                    if (!string.IsNullOrEmpty(newImagePath))
                    {
                        DeleteOldFile(studyAbroad.ImageUrl);
                        studyAbroad.ImageUrl = newImagePath;
                    }
                }

                await _context.SaveChangesAsync();

                // Handle Housing Options
                var housingCount = 0;

                // Delete selected housing
                if (request.DeletedHousingIds != null && request.DeletedHousingIds.Any())
                {
                    var housingToDelete = await _context.HousingOptionsForAbroad
                        .Where(h => h.StudyAbroadId == id && request.DeletedHousingIds.Contains(h.Id))
                        .ToListAsync();

                    foreach (var housing in housingToDelete)
                    {
                        DeleteOldFile(housing.ImagePath);
                    }
                    _context.HousingOptionsForAbroad.RemoveRange(housingToDelete);
                    await _context.SaveChangesAsync();
                }

                // Add new housing
                if (request.HousingNames != null && request.HousingNames.Any())
                {
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

                        var housing = new HousingOptionForAbroad
                        {
                            Name = request.HousingNames[i],
                            PhoneNumber = request.HousingPhones?[i] ?? "0000000000",
                            Description = request.HousingDescriptions?[i] ?? "لا يوجد وصف",
                            ImagePath = housingImagePath,
                            StudyAbroadId = id,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.HousingOptionsForAbroad.Add(housing);
                        housingCount++;
                    }
                }

                // Handle Documents
                var documentCount = 0;

                // Delete selected documents
                if (request.DeletedDocumentIds != null && request.DeletedDocumentIds.Any())
                {
                    var documentsToDelete = await _context.DocumentsRequiredForStudyAbroad
                        .Where(d => d.StudyAbroadId == id && request.DeletedDocumentIds.Contains(d.Id))
                        .ToListAsync();

                    _context.DocumentsRequiredForStudyAbroad.RemoveRange(documentsToDelete);
                    await _context.SaveChangesAsync();
                }

                // Add new documents
                if (request.DocumentNames != null && request.DocumentNames.Any())
                {
                    for (int i = 0; i < request.DocumentNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(request.DocumentNames[i]))
                            continue;

                        var document = new DocumentRequiredForStudyAbroad
                        {
                            DocumentName = request.DocumentNames[i],
                            Description = request.DocumentDescriptions?[i] ?? "",
                            StudyAbroadId = id,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.DocumentsRequiredForStudyAbroad.Add(document);
                        documentCount++;
                    }
                }

                // Handle Faculties (including Expenses and Coordination updates)
                var facultyCount = 0;

                // Delete selected faculties
                if (request.DeletedFacultyIds != null && request.DeletedFacultyIds.Any())
                {
                    var facultiesToDelete = await _context.FacultiesForAbroad
                        .Where(f => f.StudyAbroadId == id && request.DeletedFacultyIds.Contains(f.Id))
                        .ToListAsync();

                    foreach (var faculty in facultiesToDelete)
                    {
                        DeleteOldFile(faculty.ImageUrl);
                    }
                    _context.FacultiesForAbroad.RemoveRange(facultiesToDelete);
                    await _context.SaveChangesAsync();
                }

                // Update existing faculties (for expenses and coordination)
                if (request.UpdatedFaculties != null && request.UpdatedFaculties.Any())
                {
                    foreach (var updatedFaculty in request.UpdatedFaculties)
                    {
                        var faculty = await _context.FacultiesForAbroad
                            .FirstOrDefaultAsync(f => f.Id == updatedFaculty.Id && f.StudyAbroadId == id);

                        if (faculty != null)
                        {
                            faculty.Expenses = updatedFaculty.Expenses;
                            faculty.Coordination = updatedFaculty.Coordination;
                            faculty.NameArabic = updatedFaculty.NameArabic ?? faculty.NameArabic;
                            faculty.NameEnglish = updatedFaculty.NameEnglish ?? faculty.NameEnglish;
                            faculty.UpdatedAt = DateTime.UtcNow;

                            if (updatedFaculty.Image != null && updatedFaculty.Image.Length > 0)
                            {
                                DeleteOldFile(faculty.ImageUrl);
                                faculty.ImageUrl = await SaveFile(updatedFaculty.Image, "faculties");
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                // Add new faculties
                if (request.FacultyNames != null && request.FacultyNames.Any())
                {
                    for (int i = 0; i < request.FacultyNames.Count; i++)
                    {
                        if (string.IsNullOrEmpty(request.FacultyNames[i]))
                            continue;

                        var faculty = new FacultyForAbroad
                        {
                            NameArabic = request.FacultyNames[i],
                            NameEnglish = request.FacultyEnglishNames?[i] ?? request.FacultyNames[i],
                            Expenses = request.FacultyExpenses?[i] ?? 0,
                            Coordination = request.FacultyCoordination?[i] ?? 0,
                            ImageUrl = request.FacultyImages?[i] != null ? await SaveFile(request.FacultyImages[i], "faculties") : null,
                            StudyAbroadId = id,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.FacultiesForAbroad.Add(faculty);
                        facultyCount++;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "تم تحديث البيانات بنجاح",
                    studyAbroadId = id,
                    housingCount,
                    documentCount,
                    facultyCount,
                    updatedFacultyCount = request.UpdatedFaculties?.Count ?? 0,
                    deletedHousingCount = request.DeletedHousingIds?.Count ?? 0,
                    deletedDocumentCount = request.DeletedDocumentIds?.Count ?? 0,
                    deletedFacultyCount = request.DeletedFacultyIds?.Count ?? 0
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
        #endregion

        #region Soft Delete
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteStudyAbroad(int id)
        {
            try
            {
                var studyAbroad = await _context.StudyAbroads
                    .Include(s => s.HousingOptions)
                    .Include(s => s.DocumentsRequired)
                    .Include(s => s.Faculties)
                    .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

                if (studyAbroad == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "مكتب الدراسة بالخارج غير موجود"
                    });
                }

                // Apply soft delete
                studyAbroad.IsDeleted = true;
                studyAbroad.DeletedAt = DateTime.UtcNow;
                studyAbroad.UpdatedAt = DateTime.UtcNow;

                // Soft delete related entities
                foreach (var housing in studyAbroad.HousingOptions)
                {
                    housing.IsDeleted = true;
                    housing.DeletedAt = DateTime.UtcNow;
                    housing.UpdatedAt = DateTime.UtcNow;
                }

                foreach (var document in studyAbroad.DocumentsRequired)
                {
                    document.IsDeleted = true;
                    document.DeletedAt = DateTime.UtcNow;
                    document.UpdatedAt = DateTime.UtcNow;
                }

                foreach (var faculty in studyAbroad.Faculties)
                {
                    faculty.IsDeleted = true;
                    faculty.DeletedAt = DateTime.UtcNow;
                    faculty.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "تم حذف مكتب الدراسة بالخارج بنجاح",
                    data = new
                    {
                        studyAbroadId = id,
                        name = studyAbroad.NameArabic,
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
                    message = "حدث خطأ أثناء الحذف",
                    error = ex.Message
                });
            }
        }
        #endregion

       

       
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
                // Ignore file deletion errors
            }
        }
    }
    public class StudyAbroadFormModel
    {
        [Required]
        public string NameArabic { get; set; }

        [Required]
        public string NameEnglish { get; set; }

        [Required]
        public string Description { get; set; }

        public string? Licenses { get; set; }
        public string? Partnership { get; set; }
        public string? Services { get; set; }

        public IFormFile? ImageUrl { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Url]
        public string? Website { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        [Phone]
        public string? WhatsAppNumber { get; set; }

        [Url]
        public string? FacebookPage { get; set; }

        // Housing Options
        public List<string>? HousingNames { get; set; }
        public List<string>? HousingPhones { get; set; }
        public List<string>? HousingDescriptions { get; set; }
        public List<IFormFile>? HousingImages { get; set; }

        // Required Documents
        public List<string>? DocumentNames { get; set; }
        public List<string>? DocumentDescriptions { get; set; }

        // Faculties
        public List<string>? FacultyNames { get; set; }
        public List<string>? FacultyEnglishNames { get; set; }
        public List<decimal>? FacultyExpenses { get; set; }
        public List<decimal>? FacultyCoordination { get; set; }
        public List<IFormFile>? FacultyImages { get; set; }
    }
    public class UpdateStudyAbroadRequest
    {
        [Required]
        public string NameArabic { get; set; }

        [Required]
        public string NameEnglish { get; set; }

        [Required]
        public string Description { get; set; }

        public string? Licenses { get; set; }
        public string? Partnership { get; set; }
        public string? Services { get; set; }

        public IFormFile? ImageUrl { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Url]
        public string? Website { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        [Phone]
        public string? WhatsAppNumber { get; set; }

        [Url]
        public string? FacebookPage { get; set; }

        // Housing Options
        public List<string>? HousingNames { get; set; }
        public List<string>? HousingPhones { get; set; }
        public List<string>? HousingDescriptions { get; set; }
        public List<IFormFile>? HousingImages { get; set; }
        public List<int>? DeletedHousingIds { get; set; }

        // Required Documents
        public List<string>? DocumentNames { get; set; }
        public List<string>? DocumentDescriptions { get; set; }
        public List<int>? DeletedDocumentIds { get; set; }

        // Faculties - For updating existing ones
        public List<UpdatedFacultyItem>? UpdatedFaculties { get; set; }

        // Faculties - For adding new ones
        public List<string>? FacultyNames { get; set; }
        public List<string>? FacultyEnglishNames { get; set; }
        public List<decimal>? FacultyExpenses { get; set; }
        public List<decimal>? FacultyCoordination { get; set; }
        public List<IFormFile>? FacultyImages { get; set; }
        public List<int>? DeletedFacultyIds { get; set; }
    }

    public class UpdatedFacultyItem
    {
        public int Id { get; set; }
        public string? NameArabic { get; set; }
        public string? NameEnglish { get; set; }
        public decimal Expenses { get; set; }
        public decimal Coordination { get; set; }
        public IFormFile? Image { get; set; }
    }
    public class StudyAbroadSimpleDto
    {
        public int Id { get; set; }
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
        public string Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public int FacultiesCount { get; set; }
        public int HousingCount { get; set; }
        public int DocumentsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class StudyAbroadDetailDto
    {
        public int Id { get; set; }
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
        public string Description { get; set; }
        public string? Licenses { get; set; }
        public string? Partnership { get; set; }
        public string? Services { get; set; }
        public string? ImageUrl { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? PhoneNumber { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? FacebookPage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<HousingOptionForAbroadDto> HousingOptions { get; set; } = new();
        public List<DocumentRequiredForAbroadDto> DocumentsRequired { get; set; } = new();
        public List<FacultyForAbroadDto> Faculties { get; set; } = new();
    }

    public class HousingOptionForAbroadDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Description { get; set; }
        public string? ImagePath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class DocumentRequiredForAbroadDto
    {
        public int Id { get; set; }
        public string DocumentName { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
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
        public DateTime UpdatedAt { get; set; }
    }
    public class PagedResult<T>
    {
        public List<T> Data { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}