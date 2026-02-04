using BawabaUNI.Models.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BawabaUNI.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArticaleController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ArticaleController(AppDbContext context)
        {
            _context = context;
        }

        public class ArticleResponseDto
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string ImagePath { get; set; }
            public string AuthorName { get; set; }
            public string AuthorImage { get; set; }
            public DateTime Date { get; set; }
            public int ReadTime { get; set; }
            public DateTime CreatedDate { get; set; }
            public string ShortDescription { get; set; }
        }

        public class ArticleDetailDto : ArticleResponseDto
        {
            public string Content { get; set; }
        }

        [HttpGet("articles")]
        public async Task<IActionResult> GetAllArticles(
    [FromQuery] string search = null,
    [FromQuery] string author = null,
    [FromQuery] string tag = null,
    [FromQuery] DateTime? fromDate = null,
    [FromQuery] DateTime? toDate = null,
    [FromQuery] string sortBy = "newest",
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.Articles.AsQueryable()
                    .Where(u => !u.IsDeleted);

                // البحث بالنص
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim().ToLower();
                    query = query.Where(a =>
                        a.Title.ToLower().Contains(search) ||
                        a.Description.ToLower().Contains(search) ||
                        a.Content.ToLower().Contains(search));
                }

                // البحث بالكاتب
                if (!string.IsNullOrWhiteSpace(author))
                {
                    author = author.Trim();
                    query = query.Where(a => a.AuthorName.Contains(author));
                }

               

                // فلترة حسب التاريخ من
                if (fromDate.HasValue)
                {
                    query = query.Where(a => a.Date >= fromDate);
                }

                // فلترة حسب التاريخ إلى
                if (toDate.HasValue)
                {
                    query = query.Where(a => a.Date <= toDate);
                }

                // الترتيب حسب الاختيار
                switch (sortBy.ToLower())
                {
                    case "newest":
                        query = query.OrderByDescending(a => a.Date);
                        break;
                    case "oldest":
                        query = query.OrderBy(a => a.Date);
                        break;
                    case "title":
                        query = query.OrderBy(a => a.Title);
                        break;
                    case "readtime":
                        query = query.OrderByDescending(a => a.ReadTime);
                        break;
                    default:
                        query = query.OrderByDescending(a => a.Date);
                        break;
                }

                // حساب العدد الإجمالي
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // التجزئة
                var articles = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new ArticleResponseDto
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Description = a.Description,
                        ImagePath = a.ImagePath,
                        AuthorName = a.AuthorName,
                        AuthorImage = a.AuthorImage,
                        Date = a.Date,
                        ReadTime = a.ReadTime,
                        CreatedDate = a.CreatedAt,
                        // حقل محسوب للعرض المختصر
                        ShortDescription = a.Description.Length > 150 ?
                            a.Description.Substring(0, 150) + "..." : a.Description
                    })
                    .ToListAsync();

                // استجابة مع بيانات التجزئة
                var response = new
                {
                    Success = true,
                    Message = "تم جلب المقالات بنجاح",
                    Data = new
                    {
                        Articles = articles,
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
                            Author = author,
                            Tag = tag,
                            FromDate = fromDate,
                            ToDate = toDate
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
                    Message = "حدث خطأ أثناء جلب المقالات",
                    Error = ex.Message
                });
            }
        }

        // 2. الحصول على مقالة واحدة بالكامل حسب ID
        [HttpGet("articles/{id}")]
        public async Task<IActionResult> GetArticleById(int id)
        {
            try
            {
                var article = await _context.Articles
                    .Where(u => !u.IsDeleted)
                    .Where(a => a.Id == id)
                    .Select(a => new ArticleDetailDto
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Description = a.Description,
                        ImagePath = a.ImagePath,
                        AuthorName = a.AuthorName,
                        AuthorImage = a.AuthorImage,
                        Content = a.Content,
                        Date = a.Date,
                        ReadTime = a.ReadTime,
                        CreatedDate = a.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (article == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = "المقالة غير موجودة"
                    });
                }

                
              

                var response = new
                {
                    Success = true,
                    Message = "تم جلب المقالة بنجاح",
                    Data = new
                    {
                        Article = article,
                        Meta = new
                        {
                            PublishedDate = article.Date.ToString("yyyy-MM-dd"),
                            ReadTimeText = $"{article.ReadTime} دقيقة للقراءة",
                            AuthorInfo = $"بقلم: {article.AuthorName}"
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
                    Message = "حدث خطأ أثناء جلب المقالة",
                    Error = ex.Message
                });
            }
        }
    }
}
