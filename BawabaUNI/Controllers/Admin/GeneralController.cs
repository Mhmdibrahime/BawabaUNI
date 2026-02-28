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
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/Admin/admin/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]

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
        #region Footer Advertisements Endpoints

        // GET: api/admin/general/footer-advertisements
        [HttpGet("footer-advertisements")]
        public async Task<ActionResult<IEnumerable<FooterAdvertisementResponseDto>>> GetFooterAdvertisements()
        {
            var footerAds = await _context.FooterAdvertisements
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var result = footerAds.Select(ad => new FooterAdvertisementResponseDto
            {
                Id = ad.Id,
                MobileImagePath = ad.MobileImagePath,
                MobileImageUrl = $"{Request.Scheme}://{Request.Host}{ad.MobileImagePath}",
                DesktopImagePath = ad.DesktobImagePath,
                DesktopImageUrl = $"{Request.Scheme}://{Request.Host}{ad.DesktobImagePath}",
                TabletImagePath = ad.TabletImagePath,
                TabletImageUrl = $"{Request.Scheme}://{Request.Host}{ad.TabletImagePath}",
                Link = ad.Link,
                Status = ad.Status,
                StartDate = ad.StartDate,
                EndDate = ad.EndDate,
                CreatedAt = ad.CreatedAt,
                UpdatedAt = ad.UpdatedAt
            }).ToList();

            return Ok(result);
        }

        

        // GET: api/admin/general/footer-advertisements/{id}
        [HttpGet("footer-advertisements/{id}")]
        public async Task<ActionResult<FooterAdvertisementResponseDto>> GetFooterAdvertisement(int id)
        {
            var footerAd = await _context.FooterAdvertisements
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (footerAd == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الإعلان" });
            }

            var result = new FooterAdvertisementResponseDto
            {
                Id = footerAd.Id,
                MobileImagePath = footerAd.MobileImagePath,
                MobileImageUrl = $"{Request.Scheme}://{Request.Host}{footerAd.MobileImagePath}",
                DesktopImagePath = footerAd.DesktobImagePath,
                DesktopImageUrl = $"{Request.Scheme}://{Request.Host}{footerAd.DesktobImagePath}",
                TabletImagePath = footerAd.TabletImagePath,
                TabletImageUrl = $"{Request.Scheme}://{Request.Host}{footerAd.TabletImagePath}",
                Link = footerAd.Link,
                Status = footerAd.Status,
                StartDate = footerAd.StartDate,
                EndDate = footerAd.EndDate,
                CreatedAt = footerAd.CreatedAt,
                UpdatedAt = footerAd.UpdatedAt
            };

            return Ok(result);
        }

        // POST: api/admin/general/footer-advertisements
        [HttpPost("footer-advertisements")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB total limit for all images
        public async Task<ActionResult<FooterAdvertisementResponseDto>> CreateFooterAdvertisement([FromForm] CreateFooterAdvertisementDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate all images
            var mobileValidation = ValidateFooterAdImageFile(dto.MobileImageFile, "mobile");
            if (!mobileValidation.IsValid)
                return BadRequest(mobileValidation.ErrorMessage);

            var desktopValidation = ValidateFooterAdImageFile(dto.DesktopImageFile, "desktop");
            if (!desktopValidation.IsValid)
                return BadRequest(desktopValidation.ErrorMessage);

            var tabletValidation = ValidateFooterAdImageFile(dto.TabletImageFile, "tablet");
            if (!tabletValidation.IsValid)
                return BadRequest(tabletValidation.ErrorMessage);

            // Create uploads folder if it doesn't exist
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "footer-ads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Save mobile image
            var mobileFileName = $"mobile_{Guid.NewGuid()}{Path.GetExtension(dto.MobileImageFile.FileName)}";
            var mobileFilePath = Path.Combine(uploadsFolder, mobileFileName);
            using (var stream = new FileStream(mobileFilePath, FileMode.Create))
            {
                await dto.MobileImageFile.CopyToAsync(stream);
            }

            // Save desktop image
            var desktopFileName = $"desktop_{Guid.NewGuid()}{Path.GetExtension(dto.DesktopImageFile.FileName)}";
            var desktopFilePath = Path.Combine(uploadsFolder, desktopFileName);
            using (var stream = new FileStream(desktopFilePath, FileMode.Create))
            {
                await dto.DesktopImageFile.CopyToAsync(stream);
            }

            // Save tablet image
            var tabletFileName = $"tablet_{Guid.NewGuid()}{Path.GetExtension(dto.TabletImageFile.FileName)}";
            var tabletFilePath = Path.Combine(uploadsFolder, tabletFileName);
            using (var stream = new FileStream(tabletFilePath, FileMode.Create))
            {
                await dto.TabletImageFile.CopyToAsync(stream);
            }

            var footerAd = new FooterAdvertisement
            {
                MobileImagePath = $"/uploads/footer-ads/{mobileFileName}",
                DesktobImagePath = $"/uploads/footer-ads/{desktopFileName}",
                TabletImagePath = $"/uploads/footer-ads/{tabletFileName}",
                Link = dto.Link,
                Status = dto.Status ?? "Active",
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                CreatedAt = DateTime.UtcNow
            };

            _context.FooterAdvertisements.Add(footerAd);
            await _context.SaveChangesAsync();

            var result = new FooterAdvertisementResponseDto
            {
                Id = footerAd.Id,
                MobileImagePath = footerAd.MobileImagePath,
                MobileImageUrl = $"{Request.Scheme}://{Request.Host}{footerAd.MobileImagePath}",
                DesktopImagePath = footerAd.DesktobImagePath,
                DesktopImageUrl = $"{Request.Scheme}://{Request.Host}{footerAd.DesktobImagePath}",
                TabletImagePath = footerAd.TabletImagePath,
                TabletImageUrl = $"{Request.Scheme}://{Request.Host}{footerAd.TabletImagePath}",
                Link = footerAd.Link,
                Status = footerAd.Status,
                StartDate = footerAd.StartDate,
                EndDate = footerAd.EndDate,
                CreatedAt = footerAd.CreatedAt,
                UpdatedAt = footerAd.UpdatedAt
            };

            return CreatedAtAction(nameof(GetFooterAdvertisement), new { id = footerAd.Id }, result);
        }

        

        // DELETE: api/admin/general/footer-advertisements/{id}
        [HttpDelete("footer-advertisements/{id}")]
        public async Task<IActionResult> DeleteFooterAdvertisement(int id)
        {
            var footerAd = await _context.FooterAdvertisements
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (footerAd == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الإعلان" });
            }

            // Delete all three images
            var mobilePath = Path.Combine(_environment.WebRootPath, footerAd.MobileImagePath.TrimStart('/'));
            if (System.IO.File.Exists(mobilePath))
                System.IO.File.Delete(mobilePath);

            var desktopPath = Path.Combine(_environment.WebRootPath, footerAd.DesktobImagePath.TrimStart('/'));
            if (System.IO.File.Exists(desktopPath))
                System.IO.File.Delete(desktopPath);

            var tabletPath = Path.Combine(_environment.WebRootPath, footerAd.TabletImagePath.TrimStart('/'));
            if (System.IO.File.Exists(tabletPath))
                System.IO.File.Delete(tabletPath);

            footerAd.IsDeleted = true;
            footerAd.DeletedAt = DateTime.UtcNow;

            _context.FooterAdvertisements.Update(footerAd);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PATCH: api/admin/general/footer-advertisements/{id}/toggle-status
        [HttpPatch("footer-advertisements/{id}/toggle-status")]
        public async Task<IActionResult> ToggleFooterAdvertisementStatus(int id)
        {
            var footerAd = await _context.FooterAdvertisements
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (footerAd == null)
            {
                return NotFound(new { Message = "لم يتم العثور على الإعلان" });
            }

            footerAd.Status = footerAd.Status == "Active" ? "Inactive" : "Active";
            footerAd.UpdatedAt = DateTime.UtcNow;

            _context.FooterAdvertisements.Update(footerAd);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Id = footerAd.Id,
                Status = footerAd.Status,
                Message = $"تم تغيير حالة الإعلان إلى {(footerAd.Status == "Active" ? "نشط" : "غير نشط")}"
            });
        }

        // GET: api/admin/general/footer-advertisements/stats
        [HttpGet("footer-advertisements/stats")]
        public async Task<IActionResult> GetFooterAdvertisementStats()
        {
            var now = DateTime.UtcNow;
            var totalAds = await _context.FooterAdvertisements.CountAsync(x => !x.IsDeleted);
            var activeAds = await _context.FooterAdvertisements
                .CountAsync(x => !x.IsDeleted &&
                                x.Status == "Active" &&
                                (!x.StartDate.HasValue || x.StartDate.Value <= now) &&
                                (!x.EndDate.HasValue || x.EndDate.Value >= now));
            var inactiveAds = totalAds - activeAds;

            var stats = new
            {
                TotalAdvertisements = totalAds,
                ActiveAdvertisements = activeAds,
                InactiveAdvertisements = inactiveAds,
                Message = $"{activeAds} إعلان نشط من أصل {totalAds}"
            };

            return Ok(stats);
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
        private (bool IsValid, string ErrorMessage) ValidateFooterAdImageFile(IFormFile file, string imageType)
        {
            if (file == null)
            {
                return (false, $"صورة {imageType} مطلوبة");
            }

            if (file.Length > 3 * 1024 * 1024) // 3MB per image
            {
                return (false, $"حجم صورة {imageType} يتجاوز 3 ميجابايت");
            }

            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return (false, $"صورة {imageType} يجب أن تكون من نوع PNG، JPG، GIF، أو WEBP");
            }

            return (true, null);
        }

        #endregion


    }
    public class FooterAdvertisementResponseDto
    {
        public int Id { get; set; }
        public string MobileImagePath { get; set; }
        public string MobileImageUrl { get; set; }
        public string DesktopImagePath { get; set; }
        public string DesktopImageUrl { get; set; }
        public string TabletImagePath { get; set; }
        public string TabletImageUrl { get; set; }
        public string Link { get; set; }
        public string Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
    public class UpdateFooterAdvertisementDto
    {
        public IFormFile? MobileImageFile { get; set; }
        public IFormFile? DesktopImageFile { get; set; }
        public IFormFile? TabletImageFile { get; set; }

        [MaxLength(500)]
        [Url]
        public string? Link { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
    public class CreateFooterAdvertisementDto
    {
        [Required]
        public IFormFile MobileImageFile { get; set; }

        [Required]
        public IFormFile DesktopImageFile { get; set; }

        [Required]
        public IFormFile TabletImageFile { get; set; }

        [MaxLength(500)]
        [Url]
        public string Link { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Active";

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}