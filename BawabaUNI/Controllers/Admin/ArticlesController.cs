using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BawabaUNI.Models.Data;
using BawabaUNI.Models.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using BawabaUNI.Models.DTOs.Admin.ArticlesDTOs;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/Admin/[controller]")]
    [ApiController]
    public class ArticlesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ArticlesController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

       

        // Helper methods for file handling
        private async Task<string> SaveFile(IFormFile file, string subFolder)
        {
            if (file == null || file.Length == 0)
                return null;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
                throw new ArgumentException($"Invalid file type for {subFolder}. Only JPG, JPEG, PNG, GIF, and WebP are allowed.");

            if (file.Length > 5 * 1024 * 1024)
                throw new ArgumentException($"File size exceeds 5MB limit for {subFolder}.");

            var fileName = Guid.NewGuid().ToString() + fileExtension;
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", subFolder);

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{subFolder}/{fileName}";
        }

        private void DeleteFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch (Exception)
            {
                // Log error if needed
            }
        }

        // GET: api/Articles
        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<object>>> GetArticles(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string search = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var query = _context.Articles.Where(a => !a.IsDeleted).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    a.Title.Contains(search) ||
                    a.Description.Contains(search) ||
                    a.AuthorName.Contains(search) ||
                    a.Content.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var articles = await query
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.ImagePath,
                    a.AuthorName,
                    a.AuthorImage,
                    a.Content,
                    a.Date,
                    a.ReadTime,
                    a.CreatedAt,
                    a.UpdatedAt
                })
                .ToListAsync();

            var response = new PaginatedResponse<object>
            {
                Data = articles.Cast<object>().ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            return Ok(response);
        }

        // GET: api/Articles/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetArticle(int id)
        {
            var article = await _context.Articles
                .Where(a => a.Id == id && !a.IsDeleted)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.ImagePath,
                    a.AuthorName,
                    a.AuthorImage,
                    a.Content,
                    a.Date,
                    a.ReadTime,
                    a.CreatedAt,
                    a.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (article == null)
            {
                return NotFound();
            }

            return Ok(article);
        }

        // POST: api/Articles
        [HttpPost]
        public async Task<ActionResult<object>> CreateArticle([FromForm] ArticleCreateRequest request)
        {
            string imagePath = null;
            string authorImagePath = null;

            try
            {
                // Save main image
                if (request.Image != null)
                {
                    imagePath = await SaveFile(request.Image, "articles");
                }
                else
                {
                    return BadRequest(new { error = "Article image is required." });
                }

                // Save author image if provided
                if (request.AuthorImage != null)
                {
                    authorImagePath = await SaveFile(request.AuthorImage, "authors");
                }

                var article = new Article
                {
                    Title = request.Title,
                    Description = request.Description,
                    ImagePath = imagePath,
                    AuthorName = request.AuthorName,
                    AuthorImage = authorImagePath,
                    Content = request.Content,
                    Date = request.Date,
                    ReadTime = request.ReadTime,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Articles.Add(article);
                await _context.SaveChangesAsync();

                var response = new
                {
                    article.Id,
                    article.Title,
                    article.Description,
                    article.ImagePath,
                    article.AuthorName,
                    article.AuthorImage,
                    article.Content,
                    article.Date,
                    article.ReadTime,
                    article.CreatedAt,
                    article.UpdatedAt
                };

                return CreatedAtAction("GetArticle", new { id = article.Id }, response);
            }
            catch (ArgumentException ex)
            {
                // Clean up files if saved before error
                if (!string.IsNullOrEmpty(imagePath)) DeleteFile(imagePath);
                if (!string.IsNullOrEmpty(authorImagePath)) DeleteFile(authorImagePath);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(imagePath)) DeleteFile(imagePath);
                if (!string.IsNullOrEmpty(authorImagePath)) DeleteFile(authorImagePath);
                return StatusCode(500, new { error = "An error occurred while creating the article." });
            }
        }

        // PUT: api/Articles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateArticle(int id, [FromForm] ArticleUpdateRequest request)
        {
            var article = await _context.Articles
                .Where(a => a.Id == id && !a.IsDeleted)
                .FirstOrDefaultAsync();

            if (article == null)
            {
                return NotFound();
            }

            string oldImagePath = article.ImagePath;
            string oldAuthorImagePath = article.AuthorImage;
            string newImagePath = null;
            string newAuthorImagePath = null;

            try
            {
                // Handle main image update
                if (request.Image != null)
                {
                    newImagePath = await SaveFile(request.Image, "articles");
                    article.ImagePath = newImagePath;
                }

                // Handle author image update
                if (request.AuthorImage != null)
                {
                    newAuthorImagePath = await SaveFile(request.AuthorImage, "authors");
                    article.AuthorImage = newAuthorImagePath;
                }

                // Update other properties
                article.Title = request.Title;
                article.Description = request.Description;
                article.AuthorName = request.AuthorName;
                article.Content = request.Content;
                article.Date = request.Date;
                article.ReadTime = request.ReadTime;
                article.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Delete old files if new ones were uploaded successfully
                if (!string.IsNullOrEmpty(newImagePath) && !string.IsNullOrEmpty(oldImagePath))
                {
                    DeleteFile(oldImagePath);
                }
                if (!string.IsNullOrEmpty(newAuthorImagePath) && !string.IsNullOrEmpty(oldAuthorImagePath))
                {
                    DeleteFile(oldAuthorImagePath);
                }

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                // Clean up new files if saved before error
                if (!string.IsNullOrEmpty(newImagePath)) DeleteFile(newImagePath);
                if (!string.IsNullOrEmpty(newAuthorImagePath)) DeleteFile(newAuthorImagePath);
                return BadRequest(new { error = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ArticleExists(id))
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
                if (!string.IsNullOrEmpty(newImagePath)) DeleteFile(newImagePath);
                if (!string.IsNullOrEmpty(newAuthorImagePath)) DeleteFile(newAuthorImagePath);
                return StatusCode(500, new { error = "An error occurred while updating the article." });
            }
        }

        // DELETE: api/Articles/5 (Soft Delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            var article = await _context.Articles
                .Where(a => a.Id == id && !a.IsDeleted)
                .FirstOrDefaultAsync();

            if (article == null)
            {
                return NotFound();
            }

            // Soft delete
            article.IsDeleted = true;
            article.DeletedAt = DateTime.UtcNow;
            article.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Articles/stats
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetArticleStats()
        {
            var totalArticles = await _context.Articles.CountAsync(a => !a.IsDeleted);

            // Articles by month (last 6 months)
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var articlesByMonth = await _context.Articles
                .Where(a => !a.IsDeleted && a.CreatedAt >= sixMonthsAgo)
                .GroupBy(a => new { Year = a.CreatedAt.Year, Month = a.CreatedAt.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count(),
                    MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM")
                })
                .OrderBy(g => g.Year)
                .ThenBy(g => g.Month)
                .ToListAsync();

           

            // Articles by author (top 5)
            var topAuthors = await _context.Articles
                .Where(a => !a.IsDeleted)
                .GroupBy(a => a.AuthorName)
                .Select(g => new
                {
                    Author = g.Key,
                    ArticleCount = g.Count(),
                    LastArticleDate = g.Max(a => a.Date)
                })
                .OrderByDescending(g => g.ArticleCount)
                .Take(5)
                .ToListAsync();

            return Ok(new
            {
                TotalArticles = totalArticles,
                ArticlesByMonth = articlesByMonth,
                TopAuthors = topAuthors
            });
        }

       
        private bool ArticleExists(int id)
        {
            return _context.Articles.Any(e => e.Id == id && !e.IsDeleted);
        }
    }
}