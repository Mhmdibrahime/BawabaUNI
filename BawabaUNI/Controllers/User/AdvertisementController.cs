using BawabaUNI.Models.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BawabaUNI.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdvertisementController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdvertisementController(AppDbContext context)
        {
            _context = context;
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
                    .Where(u => !u.IsDeleted)
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
