using BawabaUNI.Models.Data;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/Admin/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class JobAdvertisementsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public JobAdvertisementsController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // 1- GET ALL
        [HttpGet]
        public async Task<ActionResult<IEnumerable<JobAdvertisementResponseDto>>> GetAll()
        {
            var jobAdvertisements = await _context.JobAdvertisements
                .Where(j => !j.IsDeleted)
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new JobAdvertisementResponseDto
                {
                    Id = j.Id,
                    Description = j.Description,
                    ImagePath = j.ImagePath,
                    Link = j.Link,
                    Status = j.Status,
                    StartDate = j.StartDate,
                    EndDate = j.EndDate,
                    CreatedAt = j.CreatedAt,
                    UpdatedAt = j.UpdatedAt
                })
                .ToListAsync();

            return Ok(jobAdvertisements);
        }

        // 2- GET BY ID
        [HttpGet("{id}")]
        public async Task<ActionResult<JobAdvertisementResponseDto>> GetById(int id)
        {
            var jobAdvertisement = await _context.JobAdvertisements
                .Where(j => j.Id == id && !j.IsDeleted)
                .Select(j => new JobAdvertisementResponseDto
                {
                    Id = j.Id,
                    Description = j.Description,
                    ImagePath = j.ImagePath,
                    Link = j.Link,
                    Status = j.Status,
                    StartDate = j.StartDate,
                    EndDate = j.EndDate,
                    CreatedAt = j.CreatedAt,
                    UpdatedAt = j.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (jobAdvertisement == null)
                return NotFound(new { message = "Job advertisement not found" });

            return Ok(jobAdvertisement);
        }

        // 3- CREATE
        [HttpPost]
        public async Task<ActionResult<JobAdvertisementResponseDto>> Create([FromBody] JobAdvertisementCreateDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var jobAdvertisement = new JobAdvertisement
            {
                Description = request.Description,
                ImagePath = request.ImagePath,
                Link = request.Link,
                Status = request.Status ?? "Active",
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                CreatedAt = DateTime.UtcNow
            };

            _context.JobAdvertisements.Add(jobAdvertisement);
            await _context.SaveChangesAsync();

            var response = new JobAdvertisementResponseDto
            {
                Id = jobAdvertisement.Id,
                Description = jobAdvertisement.Description,
                ImagePath = jobAdvertisement.ImagePath,
                Link = jobAdvertisement.Link,
                Status = jobAdvertisement.Status,
                StartDate = jobAdvertisement.StartDate,
                EndDate = jobAdvertisement.EndDate,
                CreatedAt = jobAdvertisement.CreatedAt,
                UpdatedAt = jobAdvertisement.UpdatedAt
            };

            return CreatedAtAction(nameof(GetById), new { id = jobAdvertisement.Id }, response);
        }

        // 4- SOFT DELETE
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var jobAdvertisement = await _context.JobAdvertisements
                .FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);

            if (jobAdvertisement == null)
                return NotFound(new { message = "Job advertisement not found" });

            jobAdvertisement.IsDeleted = true;
            jobAdvertisement.DeletedAt = DateTime.UtcNow;
            jobAdvertisement.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Job advertisement soft deleted successfully", id = jobAdvertisement.Id });
        }

        // 5- TOGGLE STATUS
        [HttpPatch("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var jobAdvertisement = await _context.JobAdvertisements
                .FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);

            if (jobAdvertisement == null)
                return NotFound(new { message = "Job advertisement not found" });

            // Toggle between Active and Inactive
            jobAdvertisement.Status = jobAdvertisement.Status == "Active" ? "Inactive" : "Active";
            jobAdvertisement.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new ToggleStatusResponseDto
            {
                Id = jobAdvertisement.Id,
                OldStatus = jobAdvertisement.Status == "Active" ? "Inactive" : "Active",
                NewStatus = jobAdvertisement.Status,
                Message = $"Job advertisement status changed to {jobAdvertisement.Status}"
            });
        }

        // 6- STATS
        [HttpGet("stats")]
        public async Task<ActionResult<JobAdvertisementStatsDto>> GetStats()
        {
            var total = await _context.JobAdvertisements.CountAsync(j => !j.IsDeleted);
            var active = await _context.JobAdvertisements.CountAsync(j => !j.IsDeleted && j.Status == "Active");
            var inactive = await _context.JobAdvertisements.CountAsync(j => !j.IsDeleted && j.Status == "Inactive");

            var currentDate = DateTime.UtcNow;
            var activeByDate = await _context.JobAdvertisements
                .CountAsync(j => !j.IsDeleted && j.Status == "Active" &&
                    (!j.StartDate.HasValue || j.StartDate <= currentDate) &&
                    (!j.EndDate.HasValue || j.EndDate >= currentDate));

            var expired = await _context.JobAdvertisements
                .CountAsync(j => !j.IsDeleted && j.EndDate.HasValue && j.EndDate < currentDate);

            var upcoming = await _context.JobAdvertisements
                .CountAsync(j => !j.IsDeleted && j.StartDate.HasValue && j.StartDate > currentDate);

            var stats = new JobAdvertisementStatsDto
            {
                Total = total,
                Active = active,
                Inactive = inactive,
                ActiveByDateRange = activeByDate,
                Expired = expired,
                Upcoming = upcoming
            };

            return Ok(stats);
        }
    }
    #region DTOs
    // 1 & 2 - Response DTO
    public class JobAdvertisementResponseDto
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public string ImagePath { get; set; }
        public string Link { get; set; }
        public string Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // 3 - Create DTO
    public class JobAdvertisementCreateDto
    {
        public string Description { get; set; }
        public string ImagePath { get; set; }
        public string Link { get; set; }
        public string Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    // 5 - Toggle Status Response DTO
    public class ToggleStatusResponseDto
    {
        public int Id { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public string Message { get; set; }
    }

    // 6 - Stats DTO
    public class JobAdvertisementStatsDto
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public int ActiveByDateRange { get; set; }
        public int Expired { get; set; }
        public int Upcoming { get; set; }
    } 
    #endregion
}