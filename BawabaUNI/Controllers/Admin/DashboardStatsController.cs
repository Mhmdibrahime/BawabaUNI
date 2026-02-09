using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BawabaUNI.Models.Data;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/Admin/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class DashboardStatsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardStatsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Admin/DashboardStats
        // Get all dashboard statistics
        [HttpGet]
        public async Task<ActionResult<DashboardStats>> GetDashboardStats()
        {
            try
            {
                // Get current date for calculations
                var today = DateTime.UtcNow.Date;
                var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
                var firstDayOfPreviousMonth = firstDayOfMonth.AddMonths(-1);
                var lastDayOfPreviousMonth = firstDayOfMonth.AddDays(-1);

                // 1. Total Universities
                var totalUniversities = await _context.Universities.CountAsync();
              
                // 2. Total Faculties
                var totalFaculties = await _context.Faculties.CountAsync();
               
                // 3. Total Courses
                var totalCourses = await _context.Courses.CountAsync();
                var previousMonthCourses = await _context.Courses
                    .Where(c => c.CreatedAt >= firstDayOfPreviousMonth && c.CreatedAt < firstDayOfMonth)
                    .CountAsync();
                var coursesGrowth = CalculateGrowthPercentage(previousMonthCourses, totalCourses);

                // 4. Total Advertisements
                var totalAdvertisements = await _context.Advertisements.CountAsync();
                var previousMonthAdvertisements = await _context.Advertisements
                    .Where(a => a.CreatedAt >= firstDayOfPreviousMonth && a.CreatedAt < firstDayOfMonth)
                    .CountAsync();
                var advertisementsGrowth = CalculateGrowthPercentage(previousMonthAdvertisements, totalAdvertisements);


              

                var stats = new DashboardStats
                {
                    // Main Statistics with Growth
                    TotalUniversities = new StatWithGrowth
                    {
                        Value = totalUniversities,
                       
                    },
                    TotalFaculties = new StatWithGrowth
                    {
                        Value = totalFaculties,
                      
                    },
                    TotalCourses = new StatWithGrowth
                    {
                        Value = totalCourses,
                        GrowthPercentage = coursesGrowth,
                        IsPositive = coursesGrowth > 0
                    },
                    TotalAdvertisements = new StatWithGrowth
                    {
                        Value = totalAdvertisements,
                        GrowthPercentage = advertisementsGrowth,
                        IsPositive = advertisementsGrowth > 0
                    },


                    // Generated Info
                    GeneratedAt = DateTime.UtcNow,
                    Period = "current-month"
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        private double CalculateGrowthPercentage(double previousValue, double currentValue)
        {
            if (previousValue == 0)
                return currentValue > 0 ? 100 : 0;

            return Math.Round(((currentValue - previousValue) / previousValue) * 100, 1);
        }
    }

    // DTO Classes
    public class DashboardStats
    {
        public StatWithGrowth TotalUniversities { get; set; } = new StatWithGrowth();
        public StatWithGrowth TotalFaculties { get; set; } = new StatWithGrowth();
        public StatWithGrowth TotalCourses { get; set; } = new StatWithGrowth();
        public StatWithGrowth TotalAdvertisements { get; set; } = new StatWithGrowth();
        public StatWithGrowth PaidCourseRevenue { get; set; } = new StatWithGrowth();
        public StatWithGrowth RecentContentCount { get; set; } = new StatWithGrowth();

        public int ActiveAdvertisements { get; set; }
        public int TrendingUniversities { get; set; }
        public int TotalConsultationRequests { get; set; }
        public int PendingConsultations { get; set; }
        public int TotalAdvertisementClicks { get; set; }
        public double AverageCoursePrice { get; set; }
        public int DiscountedCoursesCount { get; set; }

        public DateTime GeneratedAt { get; set; }
        public string Period { get; set; } = "current-month";
    }

    public class StatWithGrowth
    {
        public double Value { get; set; }
        public double GrowthPercentage { get; set; }
        public bool IsPositive { get; set; }
        public string FormattedValue => Value.ToString("N0");
    }
}