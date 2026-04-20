using BawabaUNI.Controllers.Admin;
using BawabaUNI.Models.Data;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BawabaUNI.Controllers.User
{
    [Route("api/User/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class BooksController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BooksController(AppDbContext context)
        {
            _context = context;
        }

        // 1- GET ALL with search, pagination, and filter
        [HttpGet("GetAll")]
        public async Task<ActionResult<PaginatedResponseDto<UserBookResponseDto>>> GetAll([FromQuery] UserBookFilterDto filter)
        {
            var query = _context.Books
                .Where(b => !b.IsDeleted)
                .AsQueryable();

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
                .Select(b => new UserBookResponseDto
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
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();

            var response = new PaginatedResponseDto<UserBookResponseDto>
            {
                Data = books,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize)
            };

            return Ok(response);
        }

        // 2- GET filter terms (faculties and subjects distinct)
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
        [HttpGet("{id}/GetById")]
        public async Task<ActionResult<UserBookResponseDto>> GetById(int id)
        {
            var book = await _context.Books
                .Where(b => b.Id == id && !b.IsDeleted)
                .Select(b => new UserBookResponseDto
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
                    CreatedAt = b.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (book == null)
                return NotFound(new { message = "Book not found" });

            return Ok(book);
        }

        // 4- INCREMENT upload number
        [HttpPatch("{id}/increment-upload")]
        public async Task<IActionResult> IncrementUploadNum(int id)
        {
            var book = await _context.Books
                .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

            if (book == null)
                return NotFound(new { message = "Book not found" });

            book.UploadsNum++;
            book.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Upload count incremented successfully",
                id = book.Id,
                uploadsNum = book.UploadsNum
            });
        }

        // 5- INCREMENT reading number
        [HttpPatch("{id}/increment-reading")]
        public async Task<IActionResult> IncrementReadingNum(int id)
        {
            var book = await _context.Books
                .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

            if (book == null)
                return NotFound(new { message = "Book not found" });

            book.ReadingNum++;
            book.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Reading count incremented successfully",
                id = book.Id,
                readingNum = book.ReadingNum
            });
        }
    }

    public class UserBookResponseDto
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
    }

    public class UserBookFilterDto
    {
        public string? Search { get; set; }
        public string? FacultyName { get; set; }
        public string? Subject { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortBy { get; set; } = "CreatedAt";
        public bool SortDescending { get; set; } = true;
    }
}