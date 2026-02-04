using BawabaUNI.Models.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BawabaUNI.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context )
        {
            _context = context;
        }

        public class ArticleImageDto
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string ImagePath { get; set; }
            public string AuthorName { get; set; }
            public string AuthorImage { get; set; }
            public DateTime Date { get; set; }
            public int ReadTime { get; set; }
        }

        public class CourseImageDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public decimal? Discount { get; set; }
            public string PosterImage { get; set; }
            public string Classification { get; set; }
            public string InstructorName { get; set; }
            public string InstructorImage { get; set; }
        }

        public class UniversityImageDto
        {
            public int Id { get; set; }
            public string NameArabic { get; set; }
            public string NameEnglish { get; set; }
            public string Type { get; set; }
            public string Description { get; set; }
            public string UniversityImage { get; set; }
            public int? GlobalRanking { get; set; }
            public string Location { get; set; }
        }

        // 1. الحصول على الصور الرئيسية النشطة (Hero Images)
        [HttpGet("hero")]
        public async Task<IActionResult> GetHeroImages()
        {
            var heroImages = await _context.HeroImages
                .Where(u => !u.IsDeleted)
                .Where(hi => hi.IsActive)
                .OrderBy(hi => hi.Id)
                .Select(hi => new
                {
                    hi.Id,
                    hi.ImagePath,
                    hi.IsActive,
                    hi.CreatedAt
                })
                .ToListAsync();

            return Ok(heroImages);
        }

        // 2. الحصول على صور المقالات الأخيرة
        [HttpGet("articles/latest")]
        public async Task<IActionResult> GetLatestArticleImages([FromQuery] int count = 3)
        {
            var articles = await _context.Articles
                .Where(u => !u.IsDeleted)
                .Where(a => a.Date <= DateTime.Now)
                .OrderByDescending(a => a.Date)
                .Take(count)
                .Select(a => new ArticleImageDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    ImagePath = a.ImagePath,
                    AuthorName = a.AuthorName,
                    AuthorImage = a.AuthorImage,
                    Date = a.Date,
                    ReadTime = a.ReadTime
                })
                .ToListAsync();

            return Ok(articles);
        }

        // 3. الحصول على صور المقالات حسب الكاتب
        [HttpGet("articles/by-author/{authorName}")]
        public async Task<IActionResult> GetArticlesByAuthor(string authorName)
        {
            var articles = await _context.Articles
                .Where(u => !u.IsDeleted)
                .Where(a => a.AuthorName.Contains(authorName))
                .OrderByDescending(a => a.Date)
                .Select(a => new ArticleImageDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    ImagePath = a.ImagePath,
                    AuthorName = a.AuthorName,
                    AuthorImage = a.AuthorImage,
                    Date = a.Date,
                    ReadTime = a.ReadTime
                })
                .ToListAsync();

            return Ok(articles);
        }

        // 4. الحصول على صور الدورات المميزة
        [HttpGet("courses/featured")]
        public async Task<IActionResult> GetFeaturedCourseImages([FromQuery] int count = 6)
        {
            var courses = await _context.Courses
                .OrderByDescending(c => c.Discount.HasValue ? c.Discount.Value : 0)
                .ThenByDescending(c => c.Id)
                .Take(count)
                .Select(c => new CourseImageDto
                {
                    Id = c.Id,
                    NameArabic = c.NameArabic,
                    NameEnglish = c.NameEnglish,
                    Description = c.Description,
                    Price = c.Price,
                    Discount = c.Discount,
                    PosterImage = c.PosterImage,
                    Classification = c.Classification,
                    InstructorName = c.InstructorName,
                    InstructorImage = c.InstructorImage
                })
                .ToListAsync();

            return Ok(courses);
        }

        // 5. الحصول على صور الدورات حسب التصنيف
        [HttpGet("courses/by-classification/{classification}")]
        public async Task<IActionResult> GetCoursesByClassification(string classification)
        {
            var courses = await _context.Courses
                .Where(u => !u.IsDeleted)
                .Where(c => c.Classification.Contains(classification))
                .OrderByDescending(c => c.Discount.HasValue ? c.Discount.Value : 0)
                .Select(c => new CourseImageDto
                {
                    Id = c.Id,
                    NameArabic = c.NameArabic,
                    NameEnglish = c.NameEnglish,
                    Description = c.Description,
                    Price = c.Price,
                    Discount = c.Discount,
                    PosterImage = c.PosterImage,
                    Classification = c.Classification,
                    InstructorName = c.InstructorName,
                    InstructorImage = c.InstructorImage
                })
                .ToListAsync();

            return Ok(courses);
        }

       
        [HttpGet("universities/trending")]
        public async Task<IActionResult> GetTrendingUniversityImages([FromQuery] int count = 10)
        {
            var universities = await _context.Universities
                .Where(u => !u.IsDeleted)
                .Where(u => u.IsTrending)
                .OrderByDescending(u => u.GlobalRanking.HasValue ? u.GlobalRanking.Value : int.MaxValue)
                .Take(count)
                .Select(u => new UniversityImageDto
                {
                    Id = u.Id,
                    NameArabic = u.NameArabic,
                    NameEnglish = u.NameEnglish,
                    Type = u.Type,
                    Description = u.Description,
                    UniversityImage = u.UniversityImage,
                    GlobalRanking = u.GlobalRanking,
                    Location = u.Location
                })
                .ToListAsync();

            return Ok(universities);
        }

        [HttpGet("universities/by-type/{type}")]
        public async Task<IActionResult> GetUniversitiesByType(string type)
        {
            var universities = await _context.Universities
                .Where(u => !u.IsDeleted)
                .Where(u => u.Type.ToLower() == type.ToLower())
                .OrderBy(u => u.NameArabic)
                .Select(u => new UniversityImageDto
                {
                    Id = u.Id,
                    NameArabic = u.NameArabic,
                    NameEnglish = u.NameEnglish,
                    Type = u.Type,
                    Description = u.Description,
                    UniversityImage = u.UniversityImage,
                    GlobalRanking = u.GlobalRanking,
                    Location = u.Location
                })
                .ToListAsync();

            return Ok(universities);
        }

 
      

        // 9. الحصول على صور جميع الأنواع مع إحصائيات
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardImages()
        {
            var heroImages = await _context.HeroImages
                .Where(u => !u.IsDeleted)
                .Where(hi => hi.IsActive)
                .OrderBy(hi => hi.Id)
                .Select(hi => new { hi.Id, hi.ImagePath })
                .Take(3)
                .ToListAsync();

            var latestArticles = await _context.Articles
                .Where(a => a.Date <= DateTime.Now)
                .OrderByDescending(a => a.Date)
                .Take(3)
                .Select(a => new ArticleImageDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    ImagePath = a.ImagePath,
                    AuthorName = a.AuthorName,
                    Date = a.Date
                })
                .ToListAsync();

            var featuredCourses = await _context.Courses
                .Where(c => c.Discount.HasValue && c.Discount > 0)
                .OrderByDescending(c => c.Discount)
                .Take(4)
                .Select(c => new CourseImageDto
                {
                    Id = c.Id,
                    NameArabic = c.NameArabic,
                    PosterImage = c.PosterImage,
                    Price = c.Price,
                    Discount = c.Discount
                })
                .ToListAsync();

            var trendingUniversities = await _context.Universities
                .Where(u => u.IsTrending)
                .OrderBy(u => u.GlobalRanking)
                .Take(6)
                .Select(u => new UniversityImageDto
                {
                    Id = u.Id,
                    NameArabic = u.NameArabic,
                    UniversityImage = u.UniversityImage,
                    Type = u.Type
                })
                .ToListAsync();

            var result = new
            {
                HeroImages = heroImages,
                LatestArticles = latestArticles,
                FeaturedCourses = featuredCourses,
                TrendingUniversities = trendingUniversities,
                Statistics = new
                {
                    TotalArticles = await _context.Articles.CountAsync(),
                    TotalCourses = await _context.Courses.CountAsync(),
                    TotalUniversities = await _context.Universities.CountAsync(),
                    ActiveHeroImages = await _context.HeroImages.CountAsync(hi => hi.IsActive)
                }
            };

            return Ok(result);
        }

        // 10. البحث عن الصور في كل الأنواع
        [HttpGet("search")]
        public async Task<IActionResult> SearchImages([FromQuery] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest("Keyword is required");

            var articles = await _context.Articles
                .Where(u => !u.IsDeleted)
                .Where(a => a.Title.Contains(keyword) || a.Description.Contains(keyword) || a.AuthorName.Contains(keyword))
                .Select(a => new
                {
                    Type = "Article",
                    Id = a.Id,
                    Title = a.Title,
                    ImagePath = a.ImagePath,
                    AuthorImage = a.AuthorImage,
                    CreatedDate = a.CreatedAt
                })
                .Take(10)
                .ToListAsync();

            var courses = await _context.Courses
                .Where(c => c.NameArabic.Contains(keyword) || c.NameEnglish.Contains(keyword) ||
                           c.Classification.Contains(keyword) || c.InstructorName.Contains(keyword))
                .Select(c => new
                {
                    Type = "Course",
                    Id = c.Id,
                    Title = c.NameArabic,
                    ImagePath = c.PosterImage,
                    AuthorImage = c.InstructorImage,
                    CreatedDate = c.CreatedAt
                })
                .Take(10)
                .ToListAsync();

            var universities = await _context.Universities
                .Where(u => u.NameArabic.Contains(keyword) || u.NameEnglish.Contains(keyword) ||
                           u.Type.Contains(keyword) || u.Location.Contains(keyword))
                .Select(u => new
                {
                    Type = "University",
                    Id = u.Id,
                    Title = u.NameArabic,
                    ImagePath = u.UniversityImage,
                    AuthorImage = null as string,
                    CreatedDate = u.CreatedAt
                })
                .Take(10)
                .ToListAsync();

            var result = new
            {
                Articles = articles,
                Courses = courses,
                Universities = universities,
                TotalResults = articles.Count + courses.Count + universities.Count
            };

            return Ok(result);
        }

        public class AdvertisementImageDto
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string ImagePath { get; set; }
            public string Link { get; set; }
            public string Status { get; set; }
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public int? ClickCount { get; set; }
            public DateTime CreatedDate { get; set; }
            public bool? IsNew { get; set; }
        }


        [HttpGet("advertisements")]
        public async Task<IActionResult> GetAllActiveAdvertisements(
     [FromQuery] string search = null,
     [FromQuery] DateTime? fromDate = null,
     [FromQuery] DateTime? toDate = null,
     [FromQuery] string sortBy = "newest",
     [FromQuery] int page = 1,
     [FromQuery] int pageSize = 20)
        {
            try
            {
                // فلترة الأساسية: الإعلانات النشطة فقط
                var query = _context.Advertisements
                    .Where(a => a.Status == "Active" && a.Status == "نشط");

                // تطبيق السيرش إذا موجود
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();
                    query = query.Where(a =>
                        a.Title.ToLower().Contains(search) ||
                        a.Description.ToLower().Contains(search));
                }

                // فلترة حسب التاريخ من
                if (fromDate.HasValue)
                {
                    query = query.Where(a => a.StartDate >= fromDate || !a.StartDate.HasValue);
                }

                // فلترة حسب التاريخ إلى
                if (toDate.HasValue)
                {
                    query = query.Where(a => a.EndDate <= toDate || !a.EndDate.HasValue);
                }

                // التحقق من الصلاحية الزمنية للإعلانات
                var now = DateTime.Now;
                query = query.Where(a =>
                    (!a.StartDate.HasValue || a.StartDate <= now) &&
                    (!a.EndDate.HasValue || a.EndDate >= now));

                // الترتيب حسب الاختيار
                switch (sortBy.ToLower())
                {
                    case "newest":
                        query = query.OrderByDescending(a => a.CreatedAt);
                        break;
                    case "oldest":
                        query = query.OrderBy(a => a.CreatedAt);
                        break;
                    case "mostviewed":
                        query = query.OrderByDescending(a => a.ClickCount ?? 0);
                        break;
                    case "title":
                        query = query.OrderBy(a => a.Title);
                        break;
                    default:
                        query = query.OrderByDescending(a => a.CreatedAt);
                        break;
                }

                // حساب العدد الإجمالي
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // التجزئة
                var advertisements = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new AdvertisementImageDto
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Description = a.Description,
                        ImagePath = a.ImagePath,
                        Link = a.Link,
                        Status = a.Status,
                        StartDate = a.StartDate,
                        EndDate = a.EndDate,
                        ClickCount = a.ClickCount,
                        CreatedDate = a.CreatedAt,
                        // حقل محسوب لمعرفة إذا الإعلان جديد (في آخر 7 أيام)
                        IsNew = a.CreatedAt >= DateTime.Now.AddDays(-7)
                    })
                    .ToListAsync();

                // استجابة مع بيانات التجزئة
                var response = new
                {
                    Success = true,
                    Message = "تم جلب الإعلانات بنجاح",
                    Data = new
                    {
                        Advertisements = advertisements,
                        TotalCount = totalCount,
                        TotalPages = totalPages,
                        CurrentPage = page,
                        PageSize = pageSize,
                        HasPreviousPage = page > 1,
                        HasNextPage = page < totalPages,
                        SearchQuery = search,
                        SortBy = sortBy
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                // في حالة حدوث خطأ
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء جلب الإعلانات",
                    Error = ex.Message
                });
            }
        }


        [HttpPost("advertisements/{id}/click")]
        public async Task<IActionResult> IncrementAdvertisementClick(int id)
        {
            var advertisement = await _context.Advertisements.FindAsync(id);

            if (advertisement == null)
                return NotFound(new { Message = "Advertisement not found" });

            advertisement.ClickCount = (advertisement.ClickCount ?? 0) + 1;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Click count updated successfully",
                NewClickCount = advertisement.ClickCount
            });
        }

    }
}
