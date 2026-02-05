using BawabaUNI.Models.Data;
using BawabaUNI.Models.DTOs.User;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BawabaUNI.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
  
        public class ConsultationsController : ControllerBase
        {
            private readonly AppDbContext _context;
            private readonly UserManager<ApplicationUser> _userManager;

            public ConsultationsController(
                AppDbContext context,
                UserManager<ApplicationUser> userManager)
            {
                _context = context;
                _userManager = userManager;
            }

            // دالة مساعدة: الحصول على معلومات الطالب من الـ Token
            private async Task<(ApplicationUser User, Student Student)> GetCurrentUserAndStudent()
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return (null, null);
                }

                var user = await _userManager.FindByIdAsync(userId);

                if (user == null || !user.IsActive )
                {
                    return (null, null);
                }

                // إذا كان المستخدم طالباً، نحصل على سجله في جدول Student
                Student student = null;
               
                
                    student = await _context.Students
                        .FirstOrDefaultAsync(s => s.ApplicationUserId == userId && !s.IsDeleted);
                

                return (user, student);
            }

            // 1. إنشاء طلب استشارة جديد (للطلاب المسجلين - باستخدام Token)
            [HttpPost("request/student")]
            [Authorize(Roles = "Student")]
            public async Task<IActionResult> CreateConsultationRequestFromStudent(
                [FromBody] ConsultationRequestFromStudentDto requestDto)
            {
                try
                {
                    // التحقق من صحة البيانات
                    if (!ModelState.IsValid)
                    {
                        var errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList();

                        return BadRequest(new
                        {
                            Success = false,
                            Message = "بيانات غير صحيحة",
                            Errors = errors
                        });
                    }

                    // الحصول على المستخدم والطالب من الـ Token
                    var (user, student) = await GetCurrentUserAndStudent();

                    if (user == null)
                    {
                        return Unauthorized(new
                        {
                            Success = false,
                            Message = "غير مصرح بالوصول"
                        });
                    }

                    if (student == null)
                    {
                        // إذا لم يكن لديه سجل Student، ننشئه
                        student = new Student
                        {
                            Name = user.FullName,
                            ApplicationUserId = user.Id,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        await _context.Students.AddAsync(student);
                        await _context.SaveChangesAsync();
                    }

                   
                    var consultationRequest = new ConsultationRequest
                    {
                        FullName = user.FullName,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                      
                        Message = requestDto.Message,
               
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow,
                        StudentId = student.Id,
                        IsPaid = false,
                        ConsultationFee = null
                    };

                    // حفظ الطلب
                    await _context.ConsultationRequests.AddAsync(consultationRequest);
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        Success = true,
                        Message = "تم إرسال طلب الاستشارة بنجاح. سيتم التواصل معك قريباً.",
                        Data = new
                        {
                            RequestId = consultationRequest.Id,
                            ReferenceNumber = $"CONS-{consultationRequest.Id:000000}",
                            Status = consultationRequest.Status,
                            CreatedAt = consultationRequest.CreatedAt,
                            StudentName = student.Name,
                            StudentEmail = user.Email,
                            NextSteps = "سيتم مراجعة طلبك وتحديد رسوم الاستشارة. ستتلقى بريداً إلكترونياً بالتفاصيل."
                        }
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        Success = false,
                        Message = "حدث خطأ أثناء إرسال طلب الاستشارة",
                        Error = ex.Message
                    });
                }
            }

            // 2. إنشاء طلب استشارة جديد (للمستخدمين غير المسجلين)
            [HttpPost("request/guest")]
            [AllowAnonymous]
            public async Task<IActionResult> CreateConsultationRequestFromGuest(
                [FromBody] ConsultationRequestDto requestDto)
            {
                try
                {
                    // التحقق من صحة البيانات
                    if (!ModelState.IsValid)
                    {
                        var errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList();

                        return BadRequest(new
                        {
                            Success = false,
                            Message = "بيانات غير صحيحة",
                            Errors = errors
                        });
                    }

                    // إنشاء طلب الاستشارة (بدون StudentId)
                    var consultationRequest = new ConsultationRequest
                    {
                        FullName = requestDto.FullName,
                        Email = requestDto.Email,
                        PhoneNumber = requestDto.PhoneNumber,
                     
                        Message = requestDto.Message,
                
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow,
                   
                        StudentId = null, // غير مسجل
                        IsPaid = false,
                        ConsultationFee = null
                    };

                    // حفظ الطلب
                    await _context.ConsultationRequests.AddAsync(consultationRequest);
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        Success = true,
                        Message = "تم إرسال طلب الاستشارة بنجاح. سيتم التواصل معك قريباً.",
                        Data = new
                        {
                            RequestId = consultationRequest.Id,
                            ReferenceNumber = $"CONS-{consultationRequest.Id:000000}",
                            Status = consultationRequest.Status,
                            CreatedAt = consultationRequest.CreatedAt,
                            NextSteps = "سيتم مراجعة طلبك وتحديد رسوم الاستشارة. ستتلقى بريداً إلكترونياً بالتفاصيل."
                        }
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        Success = false,
                        Message = "حدث خطأ أثناء إرسال طلب الاستشارة",
                        Error = ex.Message
                    });
                }
            }

            // 3. الحصول على طلبات الاستشارة الخاصة بالطالب الحالي (من الـ Token)
            [HttpGet("student/my-requests")]
            [Authorize(Roles = "Student")]
            public async Task<IActionResult> GetMyConsultations()
            {
                try
                {
                    // الحصول على الطالب الحالي من الـ Token
                    var (user, student) = await GetCurrentUserAndStudent();

                    if (user == null)
                    {
                        return Unauthorized(new
                        {
                            Success = false,
                            Message = "غير مصرح بالوصول"
                        });
                    }

                    if (student == null)
                    {
                        // إذا لم يكن لديه طلبات بعد
                        return Ok(new
                        {
                            Success = true,
                            Message = "ليس لديك أي طلبات استشارة",
                            Data = new
                            {
                                StudentId = 0,
                                StudentName = user.FullName,
                                Consultations = new List<ConsultationResponseDto>(),
                                TotalCount = 0
                            }
                        });
                    }

                    var consultations = await _context.ConsultationRequests
                        .Where(cr => cr.StudentId == student.Id && !cr.IsDeleted)
                        .OrderByDescending(cr => cr.CreatedAt)
                        .Select(cr => new ConsultationResponseDto
                        {
                            Id = cr.Id,
                            FullName = cr.FullName,
                            Email = cr.Email,
                            PhoneNumber = cr.PhoneNumber,
                            
                            Status = cr.Status,
                           
                            CreatedAt = cr.CreatedAt,
                            AssignedAt = cr.AssignedAt,
                            CompletedAt = cr.CompletedAt,
                            IsPaid = cr.IsPaid,
                            PaymentReference = cr.PaymentReference,

                        })
                        .ToListAsync();

                    return Ok(new
                    {
                        Success = true,
                        Message = "تم جلب طلبات الاستشارة بنجاح",
                        Data = new
                        {
                            StudentId = student.Id,
                            StudentName = student.Name,
                            Consultations = consultations,
                            TotalCount = consultations.Count,
                            PendingCount = consultations.Count(c => c.Status == "Pending"),
                            InProgressCount = consultations.Count(c => c.Status == "InProgress"),
                            CompletedCount = consultations.Count(c => c.Status == "Completed"),
                            CancelledCount = consultations.Count(c => c.Status == "Cancelled")
                        }
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        Success = false,
                        Message = "حدث خطأ أثناء جلب طلبات الاستشارة",
                        Error = ex.Message
                    });
                }
            }

            // 4. الحصول على طلب استشارة محدد للطالب الحالي
            [HttpGet("student/my-requests/{id}")]
            [Authorize(Roles = "Student")]
            public async Task<IActionResult> GetMyConsultationById(int id)
            {
                try
                {
                    // الحصول على الطالب الحالي من الـ Token
                    var (user, student) = await GetCurrentUserAndStudent();

                    if (user == null || student == null)
                    {
                        return Unauthorized(new
                        {
                            Success = false,
                            Message = "غير مصرح بالوصول"
                        });
                    }

                    var consultation = await _context.ConsultationRequests
                        .Where(cr => cr.Id == id &&
                                     cr.StudentId == student.Id &&
                                     !cr.IsDeleted)
                        .Select(cr => new ConsultationResponseDto
                        {
                            Id = cr.Id,
                            FullName = cr.FullName,
                            Email = cr.Email,
                            PhoneNumber = cr.PhoneNumber,
                           
                            Message = cr.Message,
                            Status = cr.Status,
                         
                            CreatedAt = cr.CreatedAt,
                            AssignedAt = cr.AssignedAt,
                            CompletedAt = cr.CompletedAt,
                            IsPaid = cr.IsPaid,
                            PaymentReference = cr.PaymentReference,

                        })
                        .FirstOrDefaultAsync();

                    if (consultation == null)
                    {
                        return NotFound(new
                        {
                            Success = false,
                            Message = "طلب الاستشارة غير موجود أو لا يخصك"
                        });
                    }

                    return Ok(new
                    {
                        Success = true,
                        Message = "تم جلب طلب الاستشارة بنجاح",
                        Data = consultation
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        Success = false,
                        Message = "حدث خطأ أثناء جلب طلب الاستشارة",
                        Error = ex.Message
                    });
                }
            }

      
    }

}
