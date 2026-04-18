using BawabaUNI.Models.Data;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/Admin/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class BooksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public BooksController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // 1- GET ALL with search and pagination
        [HttpGet]
        public async Task<ActionResult<PaginatedResponseDto<BookResponseDto>>> GetAll([FromQuery] BookFilterDto filter)
        {
            var query = _context.Books.Where(b => !b.IsDeleted).AsQueryable();

            // Apply search
            if (!string.IsNullOrEmpty(filter.Search))
            {
                query = query.Where(b =>
                    b.Title.Contains(filter.Search) ||
                    b.Description.Contains(filter.Search) ||
                    b.FacultyName.Contains(filter.Search) ||
                    b.Subject.Contains(filter.Search));
            }

            // Apply faculty filter
            if (!string.IsNullOrEmpty(filter.FacultyName))
            {
                query = query.Where(b => b.FacultyName == filter.FacultyName);
            }

            // Apply subject filter
            if (!string.IsNullOrEmpty(filter.Subject))
            {
                query = query.Where(b => b.Subject == filter.Subject);
            }

            // Apply sorting
            query = filter.SortDescending
                ? query.OrderByDescending(b => EF.Property<object>(b, filter.SortBy))
                : query.OrderBy(b => EF.Property<object>(b, filter.SortBy));

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination
            var books = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(b => new BookResponseDto
                {
                    Id = b.Id,
                    Title = b.Title,
                    Description = b.Description,
                    FacultyName = b.FacultyName,
                    Subject = b.Subject,
                    CoverImageUrl = b.CoverImageUrl,
                    BookLink = b.BookLink,
                    UploadsNum = b.UploadsNum,
                    ReadingNum = b.ReadingNum,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt,
                    UploadedAt = b.UploadedAt
                })
                .ToListAsync();

            var response = new PaginatedResponseDto<BookResponseDto>
            {
                Data = books,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize)
            };

            return Ok(response);
        }

        // 2- GET faculties and subjects distinct
        [HttpGet("filter-terms")]
        public async Task<ActionResult<FilterTermsResponseDto>> GetFilterTerms()
        {
            var faculties = await _context.Books
                .Where(b => !b.IsDeleted)
                .Select(b => b.FacultyName)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync();

            var subjects = await _context.Books
                .Where(b => !b.IsDeleted)
                .Select(b => b.Subject)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            return Ok(new FilterTermsResponseDto
            {
                Faculties = faculties,
                Subjects = subjects
            });
        }

        // 3- GET by id
        [HttpGet("{id}")]
        public async Task<ActionResult<BookResponseDto>> GetById(int id)
        {
            var book = await _context.Books
                .Where(b => b.Id == id && !b.IsDeleted)
                .Select(b => new BookResponseDto
                {
                    Id = b.Id,
                    Title = b.Title,
                    Description = b.Description,
                    FacultyName = b.FacultyName,
                    Subject = b.Subject,
                    CoverImageUrl = b.CoverImageUrl,
                    BookLink = b.BookLink,
                    UploadsNum = b.UploadsNum,
                    ReadingNum = b.ReadingNum,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt,
                    UploadedAt = b.UploadedAt
                })
                .FirstOrDefaultAsync();

            if (book == null)
                return NotFound(new { message = "Book not found" });

            return Ok(book);
        }

        // 4- ADD new book with cover image upload to wwwroot
        [HttpPost]
        public async Task<ActionResult<BookResponseDto>> Create([FromForm] BookCreateDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            string coverImagePath = null;

            try
            {
                // Validate book link if provided
                if (!string.IsNullOrEmpty(request.BookLink) && !IsValidUrl(request.BookLink))
                {
                    return BadRequest(new { error = "Invalid book link URL format" });
                }

                // Upload cover image to wwwroot if provided
                if (request.CoverImage != null && request.CoverImage.Length > 0)
                {
                    coverImagePath = await SaveBookCoverImage(request.CoverImage);
                }

                var book = new Book
                {
                    Title = request.Title,
                    Description = request.Description,
                    FacultyName = request.FacultyName,
                    Subject = request.Subject,
                    CoverImageUrl = coverImagePath, // Store local path
                    BookLink = request.BookLink, // Direct URL from admin
                    UploadsNum = 0,
                    ReadingNum = 0,
                    UploadedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Books.Add(book);
                await _context.SaveChangesAsync();

                var response = new BookResponseDto
                {
                    Id = book.Id,
                    Title = book.Title,
                    Description = book.Description,
                    FacultyName = book.FacultyName,
                    Subject = book.Subject,
                    CoverImageUrl = GetFullUrl(coverImagePath), // Return full URL
                    BookLink = book.BookLink,
                    UploadsNum = book.UploadsNum,
                    ReadingNum = book.ReadingNum,
                    CreatedAt = book.CreatedAt,
                    UpdatedAt = book.UpdatedAt,
                    UploadedAt = book.UploadedAt
                };

                return CreatedAtAction(nameof(GetById), new { id = book.Id }, response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to create book: {ex.Message}" });
            }
        }

        // 5- UPDATE book with cover image upload to wwwroot
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromForm] BookUpdateDto request)
        {
            var book = await _context.Books
                .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

            if (book == null)
                return NotFound(new { message = "Book not found" });

            try
            {
                // Validate book link if provided
                if (!string.IsNullOrEmpty(request.BookLink) && !IsValidUrl(request.BookLink))
                {
                    return BadRequest(new { error = "Invalid book link URL format" });
                }

                // Update cover image if provided
                if (request.CoverImage != null && request.CoverImage.Length > 0)
                {
                    // Delete old cover image if exists
                    if (!string.IsNullOrEmpty(book.CoverImageUrl))
                    {
                        DeleteOldCoverImage(book.CoverImageUrl);
                    }

                    // Upload new cover image
                    var newCoverPath = await SaveBookCoverImage(request.CoverImage);
                    book.CoverImageUrl = newCoverPath;
                }

                // Update other properties
                book.Title = request.Title;
                book.Description = request.Description;
                book.FacultyName = request.FacultyName;
                book.Subject = request.Subject;
                book.BookLink = request.BookLink ?? book.BookLink;
                book.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Book updated successfully", id = book.Id });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to update book: {ex.Message}" });
            }
        }

        // Helper method to save book cover image
        private async Task<string> SaveBookCoverImage(IFormFile imageFile)
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
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "books", "covers");

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
            return $"/uploads/books/covers/{fileName}";
        }

        // Helper method to delete old cover image
        private void DeleteOldCoverImage(string imagePath)
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

        // Helper method to get full URL from relative path
        private string GetFullUrl(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            var request = HttpContext.Request;
            return $"{request.Scheme}://{request.Host}{relativePath}";
        }

        // 6- DELETE (Soft delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var book = await _context.Books
                .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

            if (book == null)
                return NotFound(new { message = "Book not found" });

            // Soft delete
            book.IsDeleted = true;
            book.DeletedAt = DateTime.UtcNow;
            book.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Book deleted successfully", id = book.Id });
        }

        // Helper method to validate URLs
        private bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
    }

    // Response DTO for Get All and Get By Id
    public class BookResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string FacultyName { get; set; }
        public string Subject { get; set; }
        public string CoverImageUrl { get; set; }
        public string BookLink { get; set; }
        public int UploadsNum { get; set; }
        public int ReadingNum { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? UploadedAt { get; set; }
    }

    // Create Request DTO - Use IFormFile for cover image
    public class BookCreateDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string FacultyName { get; set; }
        public string Subject { get; set; }
        public IFormFile CoverImage { get; set; } // Changed from string to IFormFile
        public string BookLink { get; set; } // Still text link
    }

    // Update Request DTO - Use IFormFile for cover image
    public class BookUpdateDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string FacultyName { get; set; }
        public string Subject { get; set; }
        public IFormFile CoverImage { get; set; } // Changed from string to IFormFile (optional)
        public string BookLink { get; set; } // Still text link
    }

    // Filter DTO for Get All with pagination
    public class BookFilterDto
    {
        public string Search { get; set; }
        public string FacultyName { get; set; }
        public string Subject { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortBy { get; set; } = "CreatedAt";
        public bool SortDescending { get; set; } = true;
    }

    // Filter Terms Response DTO
    public class FilterTermsResponseDto
    {
        public List<string> Faculties { get; set; }
        public List<string> Subjects { get; set; }
    }

    // Paginated Response DTO
    public class PaginatedResponseDto<T>
    {
        public List<T> Data { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;
    }
}