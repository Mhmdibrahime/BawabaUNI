using BawabaUNI.Models.Data;
using BawabaUNI.Models.DTOs.User;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BawabaUNI.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourseController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CourseController(AppDbContext context , UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public class CourseResponseDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public decimal? Discount { get; set; }
            public decimal FinalPrice { get; set; }
            public int LessonsNumber { get; set; }
            public int? HoursNumber { get; set; }
            public string PosterImage { get; set; }
            public string Classification { get; set; }
            public string InstructorName { get; set; }
            public string InstructorImage { get; set; }
            public string InstructorDescription { get; set; }
            public DateTime CreatedDate { get; set; }
            public List<string> LessonsLearned { get; set; }
            public bool HasDiscount { get; set; }
            public decimal DiscountPercentage { get; set; }
            public decimal Savings { get; set; }
            public string ShortDescription { get; set; }
        }

        public class CourseDetailDto : CourseResponseDto
        {
            public List<LessonDto> LessonsLearned { get; set; }
            public List<VideoDto> Videos { get; set; }
            public int StudentsCount { get; set; }
            public double AverageRating { get; set; }
        }

        public class LessonDto
        {
            public int Id { get; set; }
            public string PointName { get; set; }
        }

        public class VideoDto
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public int Duration { get; set; }
            public string VideoUrl { get; set; }
        }
        [HttpGet("courses")]
        public async Task<IActionResult> GetAllCourses(
    [FromQuery] string search = null,
    [FromQuery] string classification = null,
    [FromQuery] string instructor = null,
    [FromQuery] decimal? minPrice = null,
    [FromQuery] decimal? maxPrice = null,
    [FromQuery] bool? hasDiscount = null,
    [FromQuery] string sortBy = "newest",
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.Courses
                    .Where(u => !u.IsDeleted)
                    .Include(c => c.LessonsLearned)
                    .AsQueryable();

                // البحث بالنص
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();
                    query = query.Where(c =>
                        c.NameArabic.ToLower().Contains(search) ||
                        c.NameEnglish.ToLower().Contains(search) ||
                        c.Description.ToLower().Contains(search) ||
                        c.Classification.ToLower().Contains(search) ||
                        c.InstructorName.ToLower().Contains(search));
                }

                // البحث بالتصنيف
                if (!string.IsNullOrWhiteSpace(classification))
                {
                    classification = classification.Trim();
                    query = query.Where(c => c.Classification.Contains(classification));
                }

                // البحث بالمدرب
                if (!string.IsNullOrWhiteSpace(instructor))
                {
                    instructor = instructor.Trim();
                    query = query.Where(c => c.InstructorName.Contains(instructor));
                }

                // فلترة حسب السعر الأدنى
                if (minPrice.HasValue)
                {
                    query = query.Where(c => c.Price >= minPrice.Value);
                }

                // فلترة حسب السعر الأعلى
                if (maxPrice.HasValue)
                {
                    query = query.Where(c => c.Price <= maxPrice.Value);
                }

                // فلترة حسب وجود خصم
                if (hasDiscount.HasValue)
                {
                    if (hasDiscount.Value)
                    {
                        query = query.Where(c => c.Discount.HasValue && c.Discount > 0);
                    }
                    else
                    {
                        query = query.Where(c => !c.Discount.HasValue || c.Discount == 0);
                    }
                }

                // الترتيب حسب الاختيار
                switch (sortBy.ToLower())
                {
                    case "newest":
                        query = query.OrderByDescending(c => c.CreatedAt);
                        break;
                    case "price_asc":
                        query = query.OrderBy(c => c.Price);
                        break;
                    case "price_desc":
                        query = query.OrderByDescending(c => c.Price);
                        break;
                    case "discount":
                        query = query.OrderByDescending(c => c.Discount ?? 0);
                        break;
                    case "name":
                        query = query.OrderBy(c => c.NameArabic);
                        break;
                    case "popular":
                        // افترض أن لديك حقل StudentsCount أو يمكنك حساب عدد الطلاب
                        query = query.OrderByDescending(c => c.LessonsNumber);
                        break;
                    default:
                        query = query.OrderByDescending(c => c.CreatedAt);
                        break;
                }

                // حساب العدد الإجمالي
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // التجزئة
                var courses = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new CourseResponseDto
                    {
                        Id = c.Id,
                        NameArabic = c.NameArabic,
                        NameEnglish = c.NameEnglish,
                        Description = c.Description,
                        Price = c.Price,
                        Discount = c.Discount,
                        FinalPrice = c.Discount.HasValue ?
                            c.Price - (c.Price * c.Discount.Value / 100) : c.Price,
                        LessonsNumber = c.LessonsNumber,
                        HoursNumber = c.HoursNumber,
                        PosterImage = c.PosterImage,
                        Classification = c.Classification,
                        InstructorName = c.InstructorName,
                        InstructorImage = c.InstructorImage,
                        InstructorDescription = c.InstructorDescription,
                        CreatedDate = c.CreatedAt,
                        LessonsLearned = c.LessonsLearned.Select(ll => ll.PointName).ToList(),
                        // حقول محسوبة
                        HasDiscount = c.Discount.HasValue && c.Discount > 0,
                        DiscountPercentage = c.Discount ?? 0,
                        Savings = c.Discount.HasValue ? c.Price * c.Discount.Value / 100 : 0,
                        ShortDescription = c.Description.Length > 150 ?
                            c.Description.Substring(0, 150) + "..." : c.Description
                    })
                    .ToListAsync();

                // استجابة مع بيانات التجزئة
                var response = new
                {
                    Success = true,
                    Message = "تم جلب الدورات بنجاح",
                    Data = new
                    {
                        Courses = courses,
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
                            Classification = classification,
                            Instructor = instructor,
                            MinPrice = minPrice,
                            MaxPrice = maxPrice,
                            HasDiscount = hasDiscount
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
                    Message = "حدث خطأ أثناء جلب الدورات",
                    Error = ex.Message
                });
            }
        }

        // 2. الحصول على دورة واحدة بالكامل حسب ID
        [HttpGet("courses/{id}")]
        public async Task<IActionResult> GetCourseById(int id)
        {
            try
            {
                var course = await _context.Courses
                    .Where(u => !u.IsDeleted)
                    .Include(c => c.LessonsLearned)
                    .Include(c => c.Videos)
                    .Where(c => c.Id == id)
                    .Select(c => new CourseDetailDto
                    {
                        Id = c.Id,
                        NameArabic = c.NameArabic,
                        NameEnglish = c.NameEnglish,
                        Description = c.Description,
                        Price = c.Price,
                        Discount = c.Discount,
                        FinalPrice = c.Discount.HasValue ?
                            c.Price - (c.Price * c.Discount.Value / 100) : c.Price,
                        LessonsNumber = c.LessonsNumber,
                        HoursNumber = c.HoursNumber,
                        PosterImage = c.PosterImage,
                        Classification = c.Classification,
                        InstructorName = c.InstructorName,
                        InstructorImage = c.InstructorImage,
                        InstructorDescription = c.InstructorDescription,
                        CreatedDate = c.CreatedAt,
                        LessonsLearned = c.LessonsLearned.Select(ll => new LessonDto
                        {
                            Id = ll.Id,
                            PointName = ll.PointName
                        }).ToList(),
                        Videos = c.Videos.Select(v => new VideoDto
                        {
                            Id = v.Id,
                            Title = v.Title,
                            Duration = v.DurationInMinutes,
                            VideoUrl = v.VideoLink
                        }).ToList(),
                        // احصائيات (يمكنك تعديلها حسب جدول StudentCourses)
                        StudentsCount = c.StudentCourses != null ? c.StudentCourses.Count : 0,
                        AverageRating = 4.9 // افتراضي - يجب تعديله حسب جدول التقييمات
                    })
                    .FirstOrDefaultAsync();

                if (course == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = "الدورة غير موجودة"
                    });
                }

                

                var response = new
                {
                    Success = true,
                    Message = "تم جلب الدورة بنجاح",
                    Data = new
                    {
                        Course = course,
                   
                        Pricing = new
                        {
                            OriginalPrice = course.Price.ToString("C"),
                            DiscountPercentage = course.Discount.HasValue ? $"{course.Discount}%" : "0%",
                            FinalPrice = course.FinalPrice.ToString("C"),
                            Savings = course.Discount.HasValue ?
                                (course.Price - course.FinalPrice).ToString("C") : "$0.00"
                        },
                        Stats = new
                        {
                            LessonsCount = course.LessonsNumber,
                            HoursCount = course.HoursNumber ?? 0,
                            StudentsCount = course.StudentsCount,
                            Rating = course.AverageRating,
                            VideosCount = course.Videos.Count
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
                    Message = "حدث خطأ أثناء جلب الدورة",
                    Error = ex.Message
                });
            }
        }
        private async Task<ApplicationUser> GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return null;

            return await _userManager.FindByIdAsync(userId);
        }

        // دالة مساعدة: التحقق من اشتراك الطالب في الكورس
        private async Task<bool> HasCourseAccess(int courseId, string userId)
        {
            if (string.IsNullOrEmpty(userId)) return false;

            // التحقق من جدول StudentCourse
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.ApplicationUserId == userId && !s.IsDeleted);

            if (student == null) return false;

            var studentCourse = await _context.StudentCourses
                .FirstOrDefaultAsync(sc => sc.StudentId == student.Id &&
                                          sc.CourseId == courseId &&
                                          !sc.IsDeleted);

            // التحقق من أن الطالب مشترك والاشتراك مفعل
            return studentCourse != null;
        }

        // دالة مساعدة: الحصول على Student
        private async Task<Student> GetCurrentStudent()
        {
            var user = await GetCurrentUser();
            if (user == null) return null;

            return await _context.Students
                .FirstOrDefaultAsync(s => s.ApplicationUserId == user.Id && !s.IsDeleted);
        }

        // 1. الحصول على جميع فيديوهات الكورس (مع التحقق من الصلاحيات)
        [HttpGet("{courseId}/videos")]
        public async Task<IActionResult> GetCourseVideos(int courseId)
        {
            try
            {
                var user = await GetCurrentUser();
                var hasAccess = user != null && await HasCourseAccess(courseId, user.Id);

                var videos = await _context.Videos
                    .Where(v => v.CourseId == courseId && !v.IsDeleted)
                    .OrderBy(v => v.CreatedAt)
                    .Select(v => new VideoCourseDto
                    {
                        Id = v.Id,
                        Title = v.Title,
                        DurationInMinutes = v.DurationInMinutes,
                        IsPaid = v.IsPaid,
                        Description = v.Description,
                        CanAccess = !v.IsPaid || hasAccess, // الفيديوهات المجانية متاحة للجميع
                        AccessMessage = !v.IsPaid ? "فيديو مجاني" :
                                       hasAccess ? "متاح للعرض" : "يتطلب شراء الكورس",
                        PlayerEmbedUrl = (!v.IsPaid || hasAccess) ? v.PlayerEmbedUrl : null,
                        VimeoId = v.VimeoId,
                        CreatedAt = v.CreatedAt
                    })
                    .ToListAsync();

                // إضافة فيديوهات عينة مجانية إذا لم يكن لدى المستخدم وصول
                if (!hasAccess && videos.Count(v => !v.IsPaid) < 3)
                {
                    return StatusCode(403, new
                    {
                        Success = false,
                        Message = "غير مصرح بالوصول",
                        Error = "يتطلب شراء الكورس لمشاهدة هذا الفيديو"
                    });
                    }
            

                return Ok(new
                {
                    Success = true,
                    Message = "تم جلب الفيديوهات بنجاح",
                    Data = new
                    {
                        Videos = videos,
                        TotalVideos = videos.Count,
                        FreeVideos = videos.Count(v => !v.IsPaid),
                        PaidVideos = videos.Count(v => v.IsPaid),
                        HasCourseAccess = hasAccess,
                        UserHasAccess = hasAccess
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء جلب الفيديوهات",
                    Error = ex.Message
                });
            }
        }

        // 2. الحصول على فيديو محدد (مع التحقق من الصلاحيات)
        [HttpGet("{courseId}/videos/{videoId}")]
        public async Task<IActionResult> GetVideoById(int courseId, int videoId)
        {
            try
            {
                var user = await GetCurrentUser();
                var hasAccess = user != null && await HasCourseAccess(courseId, user.Id);

                var video = await _context.Videos
                    .Include(v => v.Course)
                    .Where(v => v.Id == videoId &&
                                v.CourseId == courseId &&
                                !v.IsDeleted)
                    .Select(v => new
                    {
                        Id = v.Id,
                        Title = v.Title,
                        DurationInMinutes = v.DurationInMinutes,
                        IsPaid = v.IsPaid,
                        Description = v.Description,
                        VideoLink = v.VideoLink,
                        PlayerEmbedUrl = v.PlayerEmbedUrl,
                        VimeoId = v.VimeoId,
                        CourseId = v.CourseId,
                        CourseName = v.Course.NameArabic,
                        CreatedAt = v.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (video == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = "الفيديو غير موجود"
                    });
                }

                // التحقق من الصلاحية
                if (video.IsPaid && !hasAccess)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = "يتطلب شراء الكورس لمشاهدة هذا الفيديو",
                        Data = new
                        {
                            Id = video.Id,
                            Title = video.Title,
                            DurationInMinutes = video.DurationInMinutes,
                            IsPaid = video.IsPaid,
                            Description = video.Description,
                            CanAccess = false,
                            AccessMessage = "يتطلب شراء الكورس",
                            CourseId = video.CourseId,
                            CourseName = video.CourseName,
                            PreviewAvailable = false,
                            UpgradePrompt = "اشترك الآن لمشاهدة هذا الفيديو وجميع محتويات الكورس"
                        }
                    });
                }

               
               

                return Ok(new
                {
                    Success = true,
                    Message = "تم جلب الفيديو بنجاح",
                    Data = new FullVideoDto
                    {
                        Id = video.Id,
                        Title = video.Title,
                        DurationInMinutes = video.DurationInMinutes,
                        IsPaid = video.IsPaid,
                        Description = video.Description,
                        VideoLink = video.VideoLink,
                        PlayerEmbedUrl = video.PlayerEmbedUrl,
                        VimeoId = video.VimeoId,
                        CourseId = video.CourseId,
                        CourseName = video.CourseName,
                        CanAccess = true,
                        AccessMessage = "متاح للعرض"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء جلب الفيديو",
                    Error = ex.Message
                });
            }
        }

        // 3. الحصول على فيديوهات مجانية فقط
        [HttpGet("{courseId}/videos/free")]
        public async Task<IActionResult> GetFreeVideos(int courseId)
        {
            try
            {
                var videos = await _context.Videos
                    .Where(v => v.CourseId == courseId &&
                                !v.IsPaid &&
                                !v.IsDeleted)
                    .OrderBy(v => v.CreatedAt)
                    .Select(v => new FullVideoDto
                    {
                        Id = v.Id,
                        Title = v.Title,
                        DurationInMinutes = v.DurationInMinutes,
                        IsPaid = v.IsPaid,
                        Description = v.Description,
                        VideoLink = v.VideoLink,
                        PlayerEmbedUrl = v.PlayerEmbedUrl,
                        VimeoId = v.VimeoId,
                        CourseId = v.CourseId,
                        CanAccess = true,
                        AccessMessage = "فيديو مجاني"
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Success = true,
                    Message = "تم جلب الفيديوهات المجانية بنجاح",
                    Data = videos,
                    Count = videos.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء جلب الفيديوهات المجانية",
                    Error = ex.Message
                });
            }
        }

        // 4. فحص صلاحية وصول المستخدم لفيديو محدد
        [HttpGet("{courseId}/videos/{videoId}/check-access")]
        [Authorize]
        public async Task<IActionResult> CheckVideoAccess(int courseId, int videoId)
        {
            try
            {
                var user = await GetCurrentUser();
                if (user == null)
                {
                    return Unauthorized(new
                    {
                        Success = false,
                        Message = "غير مصرح بالوصول"
                    });
                }

                var video = await _context.Videos
                    .FirstOrDefaultAsync(v => v.Id == videoId &&
                                              v.CourseId == courseId &&
                                              !v.IsDeleted);

                if (video == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = "الفيديو غير موجود"
                    });
                }

                var hasCourseAccess = await HasCourseAccess(courseId, user.Id);
                var canAccess = !video.IsPaid || hasCourseAccess;

                // الحصول على معلومات الاشتراك
                var student = await GetCurrentStudent();
                var studentCourse = student != null ?
                    await _context.StudentCourses
                        .FirstOrDefaultAsync(sc => sc.StudentId == student.Id &&
                                                  sc.CourseId == courseId &&
                                                  !sc.IsDeleted) : null;

                var accessCheck = new VideoAccessCheckDto
                {
                    HasAccess = canAccess,
                    Message = canAccess ? "يمكنك مشاهدة الفيديو" :
                             video.IsPaid ? "يتطلب شراء الكورس" : "فيديو مجاني",
                    IsCoursePaid = video.IsPaid,
                    IsVideoFree = !video.IsPaid,
                    IsTrialAvailable = !hasCourseAccess && studentCourse == null, // عرض تجريبي
                    PurchaseDate = studentCourse?.CreatedAt,
               
                };

                return Ok(new
                {
                    Success = true,
                    Message = "تم فحص الصلاحية بنجاح",
                    Data = accessCheck
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء فحص الصلاحية",
                    Error = ex.Message
                });
            }
        }


    }
}
