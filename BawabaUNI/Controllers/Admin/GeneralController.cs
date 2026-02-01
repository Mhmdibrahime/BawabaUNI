using BawabaUNI.Models.Data;
using BawabaUNI.Models.DTOs;
using BawabaUNI.Models.DTOs.Admin.GenralDTOS;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class GeneralController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public GeneralController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        #region Hero Images Endpoints

        // GET: api/admin/general/hero-images
        [HttpGet("hero-images")]
        public async Task<ActionResult<IEnumerable<HeroImageResponseDto>>> GetHeroImages()
        {
            var heroImages = await _context.HeroImages
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            var result = heroImages.Select(heroImage => new HeroImageResponseDto
            {
                Id = heroImage.Id,
                ImagePath = heroImage.ImagePath,
                ImageUrl = $"{Request.Scheme}://{Request.Host}{heroImage.ImagePath}",
                IsActive = heroImage.IsActive,
                CreatedAt = heroImage.CreatedAt,
                UpdatedAt = heroImage.UpdatedAt
            }).ToList();

            return Ok(result);
        }

        // GET: api/admin/general/hero-images/active
        [HttpGet("hero-images/active")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<HeroImageResponseDto>>> GetActiveHeroImages()
        {
            var activeImages = await _context.HeroImages
                .Where(x => x.IsActive && !x.IsDeleted)
                .ToListAsync();

            var result = activeImages.Select(heroImage => new HeroImageResponseDto
            {
                Id = heroImage.Id,
                ImagePath = heroImage.ImagePath,
                ImageUrl = $"{Request.Scheme}://{Request.Host}{heroImage.ImagePath}",
                IsActive = heroImage.IsActive,
                CreatedAt = heroImage.CreatedAt,
                UpdatedAt = heroImage.UpdatedAt
            }).ToList();

            return Ok(result);
        }

        // GET: api/admin/general/hero-images/{id}
        [HttpGet("hero-images/{id}")]
        public async Task<ActionResult<HeroImageResponseDto>> GetHeroImage(int id)
        {
            var heroImage = await _context.HeroImages
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (heroImage == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الصورة" });
            }

            var result = new HeroImageResponseDto
            {
                Id = heroImage.Id,
                ImagePath = heroImage.ImagePath,
                ImageUrl = $"{Request.Scheme}://{Request.Host}{heroImage.ImagePath}",
                IsActive = heroImage.IsActive,
                CreatedAt = heroImage.CreatedAt,
                UpdatedAt = heroImage.UpdatedAt
            };

            return Ok(result);
        }

        // POST: api/admin/general/hero-images
        [HttpPost("hero-images")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<ActionResult<HeroImageResponseDto>> CreateHeroImage([FromForm] CreateHeroImageDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var validationResult = ValidateHeroImageFile(dto.ImageFile);
            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult.ErrorMessage);
            }

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.ImageFile.FileName);
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "hero-images");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.ImageFile.CopyToAsync(stream);
            }

            var heroImage = new HeroImage
            {
                ImagePath = $"/uploads/hero-images/{fileName}",
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.HeroImages.Add(heroImage);
            await _context.SaveChangesAsync();

            var result = new HeroImageResponseDto
            {
                Id = heroImage.Id,
                ImagePath = heroImage.ImagePath,
                ImageUrl = $"{Request.Scheme}://{Request.Host}{heroImage.ImagePath}",
                IsActive = heroImage.IsActive,
                CreatedAt = heroImage.CreatedAt,
                UpdatedAt = heroImage.UpdatedAt
            };

            return CreatedAtAction(nameof(GetHeroImage), new { id = heroImage.Id }, result);
        }

        // PUT: api/admin/general/hero-images/{id}
        [HttpPut("hero-images/{id}")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> UpdateHeroImage(int id, [FromForm] UpdateHeroImageDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var heroImage = await _context.HeroImages
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (heroImage == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الصورة" });
            }

            if (dto.ImageFile != null)
            {
                var validationResult = ValidateHeroImageFile(dto.ImageFile);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.ErrorMessage);
                }

                var oldImagePath = Path.Combine(_environment.WebRootPath, heroImage.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.ImageFile.FileName);
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "hero-images");
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.ImageFile.CopyToAsync(stream);
                }

                heroImage.ImagePath = $"/uploads/hero-images/{fileName}";
            }

            heroImage.IsActive = dto.IsActive;
            heroImage.UpdatedAt = DateTime.UtcNow;

            _context.HeroImages.Update(heroImage);
            await _context.SaveChangesAsync();

            var result = new HeroImageResponseDto
            {
                Id = heroImage.Id,
                ImagePath = heroImage.ImagePath,
                ImageUrl = $"{Request.Scheme}://{Request.Host}{heroImage.ImagePath}",
                IsActive = heroImage.IsActive,
                CreatedAt = heroImage.CreatedAt,
                UpdatedAt = heroImage.UpdatedAt
            };

            return Ok(result);
        }

        // DELETE: api/admin/general/hero-images/{id}
        [HttpDelete("hero-images/{id}")]
        public async Task<IActionResult> DeleteHeroImage(int id)
        {
            var heroImage = await _context.HeroImages
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (heroImage == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الصورة" });
            }

            var imagePath = Path.Combine(_environment.WebRootPath, heroImage.ImagePath.TrimStart('/'));
            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }

            heroImage.IsDeleted = true;
            heroImage.DeletedAt = DateTime.UtcNow;

            _context.HeroImages.Update(heroImage);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PATCH: api/admin/general/hero-images/{id}/toggle-status
        [HttpPatch("hero-images/{id}/toggle-status")]
        public async Task<IActionResult> ToggleHeroImageStatus(int id)
        {
            var heroImage = await _context.HeroImages
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (heroImage == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الصورة" });
            }

            heroImage.IsActive = !heroImage.IsActive;
            heroImage.UpdatedAt = DateTime.UtcNow;

            _context.HeroImages.Update(heroImage);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Id = heroImage.Id,
                IsActive = heroImage.IsActive,
                Message = $"تم تغيير حالة الصورة إلى {(heroImage.IsActive ? "نشطة" : "غير نشطة")}"
            });
        }

        // GET: api/admin/general/hero-images/stats
        [HttpGet("hero-images/stats")]
        public async Task<IActionResult> GetHeroImageStats()
        {
            var totalImages = await _context.HeroImages.CountAsync(x => !x.IsDeleted);
            var activeImages = await _context.HeroImages.CountAsync(x => x.IsActive && !x.IsDeleted);
            var inactiveImages = totalImages - activeImages;

            var stats = new
            {
                TotalImages = totalImages,
                ActiveImages = activeImages,
                InactiveImages = inactiveImages,
                Message = $"{activeImages} صورة نشطة من أصل {totalImages}"
            };

            return Ok(stats);
        }

        #endregion

        #region Partners Endpoints

        // GET: api/admin/general/partners
        [HttpGet("partners")]
        public async Task<ActionResult<IEnumerable<PartnerResponseDto>>> GetPartners()
        {
            var partners = await _context.Partners
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var result = partners.Select(partner => new PartnerResponseDto
            {
                Id = partner.Id,
                ImagePath = partner.ImagePath,
                ImageUrl = $"{Request.Scheme}://{Request.Host}{partner.ImagePath}",
                Link = partner.Link,
                CreatedAt = partner.CreatedAt,
                UpdatedAt = partner.UpdatedAt
            }).ToList();

            return Ok(result);
        }

        // GET: api/admin/general/partners/{id}
        [HttpGet("partners/{id}")]
        public async Task<ActionResult<PartnerResponseDto>> GetPartner(int id)
        {
            var partner = await _context.Partners
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (partner == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الشريك" });
            }

            var result = new PartnerResponseDto
            {
                Id = partner.Id,
                ImagePath = partner.ImagePath,
                ImageUrl = $"{Request.Scheme}://{Request.Host}{partner.ImagePath}",
                Link = partner.Link,
                CreatedAt = partner.CreatedAt,
                UpdatedAt = partner.UpdatedAt
            };

            return Ok(result);
        }

        // POST: api/admin/general/partners
        [HttpPost("partners")]
        [RequestSizeLimit(2 * 1024 * 1024)] // 2MB limit for partners
        public async Task<ActionResult<PartnerResponseDto>> CreatePartner([FromForm] CreatePartnerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var validationResult = ValidatePartnerImageFile(dto.ImageFile);
            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult.ErrorMessage);
            }

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.ImageFile.FileName);
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "partners");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.ImageFile.CopyToAsync(stream);
            }

            var partner = new Partner
            {
                ImagePath = $"/uploads/partners/{fileName}",
                Link = dto.Link,
                CreatedAt = DateTime.UtcNow
            };

            _context.Partners.Add(partner);
            await _context.SaveChangesAsync();

            var result = new PartnerResponseDto
            {
                Id = partner.Id,
                ImagePath = partner.ImagePath,
                ImageUrl = $"{Request.Scheme}://{Request.Host}{partner.ImagePath}",
                Link = partner.Link,
                CreatedAt = partner.CreatedAt,
                UpdatedAt = partner.UpdatedAt
            };

            return CreatedAtAction(nameof(GetPartner), new { id = partner.Id }, result);
        }

        // PUT: api/admin/general/partners/{id}
        [HttpPut("partners/{id}")]
        [RequestSizeLimit(2 * 1024 * 1024)]
        public async Task<IActionResult> UpdatePartner(int id, [FromForm] UpdatePartnerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var partner = await _context.Partners
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (partner == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الشريك" });
            }

            if (dto.ImageFile != null)
            {
                var validationResult = ValidatePartnerImageFile(dto.ImageFile);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.ErrorMessage);
                }

                var oldImagePath = Path.Combine(_environment.WebRootPath, partner.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.ImageFile.FileName);
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "partners");
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.ImageFile.CopyToAsync(stream);
                }

                partner.ImagePath = $"/uploads/partners/{fileName}";
            }

            partner.Link = dto.Link ?? partner.Link;
            partner.UpdatedAt = DateTime.UtcNow;

            _context.Partners.Update(partner);
            await _context.SaveChangesAsync();

            var result = new PartnerResponseDto
            {
                Id = partner.Id,
                ImagePath = partner.ImagePath,
                ImageUrl = $"{Request.Scheme}://{Request.Host}{partner.ImagePath}",
                Link = partner.Link,
                CreatedAt = partner.CreatedAt,
                UpdatedAt = partner.UpdatedAt
            };

            return Ok(result);
        }

        // DELETE: api/admin/general/partners/{id}
        [HttpDelete("partners/{id}")]
        public async Task<IActionResult> DeletePartner(int id)
        {
            var partner = await _context.Partners
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (partner == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الشريك" });
            }

            var imagePath = Path.Combine(_environment.WebRootPath, partner.ImagePath.TrimStart('/'));
            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }

            partner.IsDeleted = true;
            partner.DeletedAt = DateTime.UtcNow;

            _context.Partners.Update(partner);
            await _context.SaveChangesAsync();

            return NoContent();
        }

       

        

        #endregion

        #region Helper Methods

        private (bool IsValid, string ErrorMessage) ValidateHeroImageFile(IFormFile file)
        {
            if (file == null)
            {
                return (false, "الملف مطلوب");
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                return (false, "حجم الملف يتجاوز 5 ميجابايت");
            }

            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return (false, "يجب أن يكون الملف من نوع PNG، JPG، أو GIF");
            }

            return (true, null);
        }

        private (bool IsValid, string ErrorMessage) ValidatePartnerImageFile(IFormFile file)
        {
            if (file == null)
            {
                return (false, "الملف مطلوب");
            }

            if (file.Length > 2 * 1024 * 1024)
            {
                return (false, "حجم الملف يتجاوز 2 ميجابايت");
            }

            var allowedExtensions = new[] { ".png" }; // Only PNG for partners
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return (false, "يجب أن يكون الملف من نوع PNG فقط");
            }

            return (true, null);
        }

        #endregion
    }
}