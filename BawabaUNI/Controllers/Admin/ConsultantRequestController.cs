using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BawabaUNI.Models.Entities;
using BawabaUNI.Models.Data;
using Microsoft.AspNetCore.Authorization;

namespace BawabaUNI.Controllers.Admin
{
    [Route("api/Admin/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class ConsultantRequestController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ConsultantRequestController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Admin/ConsultantRequest
        // Get all with search, pagination and filter by status
        [HttpGet]
        public async Task<ActionResult<CPagedResult<ConsultationRequest>>> GetConsultationRequests(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sortBy = "CreatedAt")
        {
            try
            {
                var query = _context.ConsultationRequests
                    .Include(cr => cr.Student)
                    .AsQueryable();

                // Apply status filter
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(cr => cr.Status == status);
                }

                // Apply search filter
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(cr =>
                        cr.FullName.Contains(search) ||
                        cr.Email.Contains(search) ||
                        cr.PhoneNumber.Contains(search) ||
                        cr.Message.Contains(search) ||
                        (cr.Student != null && cr.Student.Name.Contains(search)));
                }

               

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply pagination
                var items = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(cr => new ConsultationRequestDto
                    {
                        Id = cr.Id,
                        FullName = cr.FullName,
                        Email = cr.Email,
                        PhoneNumber = cr.PhoneNumber,
                        Message = cr.Message,
                        Status = cr.Status,
                        StudentId = cr.StudentId,
                        StudentName = cr.Student != null ? cr.Student.Name : null,
                        
                        ConsultationFee = cr.ConsultationFee,
                        IsPaid = cr.IsPaid,
                        PaymentDate = cr.PaymentDate,
                        PaymentReference = cr.PaymentReference,
                        CreatedAt = cr.CreatedAt                    })
                    .ToListAsync();

                var result = new CPagedResult<ConsultationRequestDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/Admin/ConsultantRequest/5
        // Get by ID
        [HttpGet("{id}")]
        public async Task<ActionResult<ConsultationRequest>> GetConsultationRequest(int id)
        {
            try
            {
                var consultationRequest = await _context.ConsultationRequests
                    .Include(cr => cr.Student)
                   
                    .FirstOrDefaultAsync(cr => cr.Id == id);

                if (consultationRequest == null)
                {
                    return NotFound($"Consultation request with ID {id} not found.");
                }

                var dto = new ConsultationRequestDto
                {
                    Id = consultationRequest.Id,
                    FullName = consultationRequest.FullName,
                    Email = consultationRequest.Email,
                    PhoneNumber = consultationRequest.PhoneNumber,
                    Message = consultationRequest.Message,
                    Status = consultationRequest.Status,
                    StudentId = consultationRequest.StudentId,
                    StudentName = consultationRequest.Student != null ? consultationRequest.Student.Name : null,
                  
                    ConsultationFee = consultationRequest.ConsultationFee,
                    IsPaid = consultationRequest.IsPaid,
                    PaymentDate = consultationRequest.PaymentDate,
                    PaymentReference = consultationRequest.PaymentReference,
                    CreatedAt = consultationRequest.CreatedAt
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/Admin/ConsultantRequest/5/status
        // Change status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Status))
                {
                    return BadRequest("Status is required.");
                }

                var consultationRequest = await _context.ConsultationRequests.FindAsync(id);
                if (consultationRequest == null)
                {
                    return NotFound($"Consultation request with ID {id} not found.");
                }

                var oldStatus = consultationRequest.Status;
                consultationRequest.Status = request.Status;
                consultationRequest.UpdatedAt = DateTime.UtcNow;

                

                _context.Entry(consultationRequest).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Message = "Status updated successfully.",
                    OldStatus = oldStatus,
                    NewStatus = consultationRequest.Status,
                    UpdatedAt = consultationRequest.UpdatedAt
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ConsultationRequestExists(id))
                {
                    return NotFound($"Consultation request with ID {id} no longer exists.");
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        //// PUT: api/Admin/ConsultantRequest/5/assign
        //// Assign to user
        //[HttpPut("{id}/assign")]
        //public async Task<IActionResult> AssignToUser(int id, [FromBody] AssignRequest request)
        //{
        //    try
        //    {
        //        if (string.IsNullOrEmpty(request.UserId))
        //        {
        //            return BadRequest("User ID is required.");
        //        }

        //        var consultationRequest = await _context.ConsultationRequests.FindAsync(id);
        //        if (consultationRequest == null)
        //        {
        //            return NotFound($"Consultation request with ID {id} not found.");
        //        }

        //        // Check if user exists
        //        var user = await _context.Users.FindAsync(request.UserId);
        //        if (user == null)
        //        {
        //            return BadRequest($"User with ID {request.UserId} not found.");
        //        }

        //        consultationRequest.AssignedToUserId = request.UserId;
        //        consultationRequest.Status = "InProgress";
        //        consultationRequest.AssignedAt = DateTime.UtcNow;
        //        consultationRequest.UpdatedAt = DateTime.UtcNow;

        //        _context.Entry(consultationRequest).State = EntityState.Modified;
        //        await _context.SaveChangesAsync();

        //        return Ok(new
        //        {
        //            Message = "Consultation request assigned successfully.",
        //            AssignedTo = user.FullName,
        //            AssignedAt = consultationRequest.AssignedAt
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"Internal server error: {ex.Message}");
        //    }
        //}

        // GET: api/Admin/ConsultantRequest/stats
        // Get statistics - dynamically group by existing statuses in database
        [HttpGet("stats")]
        public async Task<ActionResult<ConsultationStats>> GetStats([FromQuery] string? status = null)
        {
            try
            {
                var query = _context.ConsultationRequests.AsQueryable();

                // Apply status filter if provided
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(cr => cr.Status == status);
                }

                var total = await query.CountAsync();

                // Get all distinct statuses from the database and their counts
                var statusGroups = await query
                    .GroupBy(cr => cr.Status)
                    .Select(g => new StatusGroup
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .OrderBy(g => g.Status)
                    .ToListAsync();

                // Get paid vs unpaid stats
                var paidCount = await query.CountAsync(cr => cr.IsPaid);
                var unpaidCount = total - paidCount;

                // Get recent requests (last 7 days)
                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                var recentCount = await query.CountAsync(cr => cr.CreatedAt >= sevenDaysAgo);

                

                var stats = new ConsultationStats
                {
                    Total = total,
                    StatusGroups = statusGroups,
                    StatusSummary = statusGroups.ToDictionary(g => g.Status, g => g.Count),
                    RecentRequests = recentCount,
                    PaidCount = paidCount,
                    UnpaidCount = unpaidCount,
                    GeneratedAt = DateTime.UtcNow
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        public class ConsultationStats
        {
            public int Total { get; set; }
            public List<StatusGroup> StatusGroups { get; set; } = new List<StatusGroup>();
            public Dictionary<string, int> StatusSummary { get; set; } = new Dictionary<string, int>();
            public int RecentRequests { get; set; }
            public int PaidCount { get; set; }
            public int UnpaidCount { get; set; }
            public double AverageCompletionDays { get; set; }
            public DateTime GeneratedAt { get; set; }
        }

        public class StatusGroup
        {
            public string Status { get; set; }
            public int Count { get; set; }
        }

        private bool ConsultationRequestExists(int id)
        {
            return _context.ConsultationRequests.Any(e => e.Id == id);
        }
    }

    // DTO Classes
    public class CPagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class ConsultationRequestDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public int? StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? AssignedToName { get; set; }
        public decimal? ConsultationFee { get; set; }
        public bool IsPaid { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string PaymentReference { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; }
        public string? Notes { get; set; }
    }

    public class AssignRequest
    {
        public string UserId { get; set; }
        public string? Notes { get; set; }
    }

    public class ConsultationStats
    {
        public int Total { get; set; }
        public int Pending { get; set; }
        public int InProgress { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int RecentRequests { get; set; }
        public int PaidCount { get; set; }
        public int UnpaidCount { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}