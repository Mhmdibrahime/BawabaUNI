using BawabaUNI.Models.Data;
using BawabaUNI.Models.DTOs.Admin.AdvertisementsDTOs;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdvertisementsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AdvertisementsController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: api/Advertisements
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAdvertisements([FromQuery] string search = null)
        {
            var query = _context.Advertisements.Where(a => !a.IsDeleted).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    a.Title.Contains(search) ||
                    a.Description.Contains(search));
            }

            var advertisements = await query
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.ImagePath,
                    a.Link,
                    a.Status,
                    a.StartDate,
                    a.EndDate,
                    a.ClickCount,
                    a.CreatedAt,
                    a.UpdatedAt
                })
                .ToListAsync();

            return Ok(advertisements);
        }

        // GET: api/Advertisements/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetAdvertisement(int id)
        {
            var advertisement = await _context.Advertisements
                .Where(a => a.Id == id && !a.IsDeleted)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.ImagePath,
                    a.Link,
                    a.Status,
                    a.StartDate,
                    a.EndDate,
                    a.ClickCount,
                    a.CreatedAt,
                    a.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (advertisement == null)
            {
                return NotFound();
            }

            return Ok(advertisement);
        }

        // POST: api/Advertisements
        [HttpPost]
        public async Task<ActionResult<object>> CreateAdvertisement([FromForm] AdvertisementCreateRequest request)
        {
            string imagePath = null;

            try
            {
                // Save image file if provided
                if (request.Image != null)
                {
                    imagePath = await SaveImageFile(request.Image);
                }

                var advertisement = new Advertisement
                {
                    Title = request.Title,
                    Description = request.Description,
                    ImagePath = imagePath,
                    Link = request.Link,
                    Status = request.Status ?? "Active",
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    ClickCount = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Advertisements.Add(advertisement);
                await _context.SaveChangesAsync();

                var response = new
                {
                    advertisement.Id,
                    advertisement.Title,
                    advertisement.Description,
                    advertisement.ImagePath,
                    advertisement.Link,
                    advertisement.Status,
                    advertisement.StartDate,
                    advertisement.EndDate,
                    advertisement.ClickCount,
                    advertisement.CreatedAt,
                    advertisement.UpdatedAt
                };

                return CreatedAtAction("GetAdvertisement", new { id = advertisement.Id }, response);
            }
            catch (ArgumentException ex)
            {
                // Clean up file if saved before error
                if (!string.IsNullOrEmpty(imagePath))
                {
                    DeleteImageFile(imagePath);
                }
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                // Clean up file if saved before error
                if (!string.IsNullOrEmpty(imagePath))
                {
                    DeleteImageFile(imagePath);
                }
                return StatusCode(500, new { error = "An error occurred while creating the advertisement." });
            }
        }

        // PUT: api/Advertisements/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAdvertisement(int id, [FromForm] AdvertisementUpdateRequest request)
        {
            var advertisement = await _context.Advertisements
                .Where(a => a.Id == id && !a.IsDeleted)
                .FirstOrDefaultAsync();

            if (advertisement == null)
            {
                return NotFound();
            }

            string oldImagePath = advertisement.ImagePath;
            string newImagePath = null;

            try
            {
                // Handle image update if new image is provided
                if (request.Image != null)
                {
                    newImagePath = await SaveImageFile(request.Image);
                    advertisement.ImagePath = newImagePath;
                }

                // Update other properties
                advertisement.Title = request.Title;
                advertisement.Description = request.Description;
                advertisement.Link = request.Link;
                advertisement.Status = request.Status;
                advertisement.StartDate = request.StartDate;
                advertisement.EndDate = request.EndDate;
                advertisement.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Delete old image file if new one was uploaded successfully
                if (!string.IsNullOrEmpty(newImagePath) && !string.IsNullOrEmpty(oldImagePath))
                {
                    DeleteImageFile(oldImagePath);
                }

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                // Clean up new file if saved before error
                if (!string.IsNullOrEmpty(newImagePath))
                {
                    DeleteImageFile(newImagePath);
                }
                return BadRequest(new { error = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AdvertisementExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                // Clean up new file if saved before error
                if (!string.IsNullOrEmpty(newImagePath))
                {
                    DeleteImageFile(newImagePath);
                }
                return StatusCode(500, new { error = "An error occurred while updating the advertisement." });
            }
        }


        // PATCH: api/Advertisements/5/toggle-status
        [HttpPatch("{id}/toggle-status")]
        public async Task<IActionResult> ToggleAdvertisementStatus(int id)
        {
            var advertisement = await _context.Advertisements
                .Where(a => a.Id == id && !a.IsDeleted)
                .FirstOrDefaultAsync();

            if (advertisement == null)
            {
                return NotFound();
            }

            // Toggle between Active and Inactive
            advertisement.Status = advertisement.Status == "Active" ? "Inactive" : "Active";
            advertisement.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Id = advertisement.Id,
                NewStatus = advertisement.Status
            });
        }

        
        // DELETE: api/Advertisements/5 (Soft Delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAdvertisement(int id)
        {
            var advertisement = await _context.Advertisements
                .Where(a => a.Id == id && !a.IsDeleted)
                .FirstOrDefaultAsync();

            if (advertisement == null)
            {
                return NotFound();
            }

            // Soft delete
            advertisement.IsDeleted = true;
            advertisement.DeletedAt = DateTime.UtcNow;
            advertisement.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Advertisements/stats
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetAdvertisementStats()
        {
            var totalAds = await _context.Advertisements.CountAsync(a => !a.IsDeleted);
            var activeAds = await _context.Advertisements.CountAsync(a => !a.IsDeleted && a.Status == "Active");
            

            return Ok(new
            {
                TotalAdvertisements = totalAds,
                ActiveAdvertisements = activeAds,
                
            });
        }

        // GET: api/Advertisements/active
        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<object>>> GetActiveAdvertisements()
        {
            var currentDate = DateTime.UtcNow;

            var activeAds = await _context.Advertisements
                .Where(a => !a.IsDeleted && a.Status == "Active")
                .Where(a => (!a.StartDate.HasValue || a.StartDate <= currentDate) &&
                           (!a.EndDate.HasValue || a.EndDate >= currentDate))
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.ImagePath,
                    a.Link,
                    a.Status,
                    a.StartDate,
                    a.EndDate,
                    a.ClickCount,
                    a.CreatedAt
                })
                .ToListAsync();

            return Ok(activeAds);
        }



        // Helper method to save image file
        private async Task<string> SaveImageFile(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return null;

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(imageFile.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                throw new ArgumentException("Invalid file type. Only JPG, JPEG, PNG, GIF, and WebP are allowed.");
            }

            // Validate file size (max 5MB)
            if (imageFile.Length > 5 * 1024 * 1024)
            {
                throw new ArgumentException("File size exceeds 5MB limit.");
            }

            // Create unique filename
            var fileName = Guid.NewGuid().ToString() + fileExtension;
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "advertisements");

            // Ensure directory exists
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            // Return relative path for database storage
            return $"/uploads/advertisements/{fileName}";
        }

        // Helper method to delete old image file
        private void DeleteImageFile(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return;

            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch (Exception)
            {
                // Log error if needed, but don't fail the main operation
            }
        }


        private bool AdvertisementExists(int id)
        {
            return _context.Advertisements.Any(e => e.Id == id && !e.IsDeleted);
        }
    }
    
}