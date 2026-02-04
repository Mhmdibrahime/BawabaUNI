using BawabaUNI.Models.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BawabaUNI.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourseController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CourseController(AppDbContext context)
        {
            _context = context;
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
    }
}
