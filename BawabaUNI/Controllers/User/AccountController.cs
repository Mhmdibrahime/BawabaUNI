using BawabaUNI.Models.Data;
using BawabaUNI.Models.DTOs.User;
using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BawabaUNI.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(
            AppDbContext context,
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _configuration = configuration;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        // DTOs
       
        // 1. Register API with Student creation
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
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

                // التحقق من وجود المستخدم مسبقاً
                var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
                if (existingUser != null)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "البريد الإلكتروني مستخدم بالفعل"
                    });
                }

                // إنشاء ApplicationUser جديد
                var user = new ApplicationUser
                {
                    UserName = registerDto.Email,
                    Email = registerDto.Email,
                    FullName = registerDto.FullName,
                    PhoneNumber = registerDto.PhoneNumber,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                };

                // إنشاء المستخدم في Identity
                var result = await _userManager.CreateAsync(user, registerDto.Password);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "فشل في إنشاء الحساب",
                        Errors = errors
                    });
                }

                // إضافة المستخدم للـ Role
                var role = "Student";
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }

                await _userManager.AddToRoleAsync(user, role);

               
                if (role == "Student")
                {
                    var student = new Student
                    {
                        Name = registerDto.FullName,
                        ApplicationUserId = user.Id,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _context.Students.AddAsync(student);
                    await _context.SaveChangesAsync();
                }

               

                // إرجاع الاستجابة
                var userResponse = new UserResponseDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,

                    CreatedAt = user.CreatedAt,
                   
                    EmailConfirmed = user.EmailConfirmed,
        
                };

                // إرسال بريد تأكيد (اختياري)
                // var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                // await SendConfirmationEmail(user.Email, confirmationToken);

                return Ok(new
                {
                    Success = true,
                    Message = "تم إنشاء الحساب بنجاح",
                    Data = userResponse
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء إنشاء الحساب",
                    Error = ex.Message
                });
            }
        }

        // 2. Login API
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
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

                // البحث عن المستخدم
                var user = await _userManager.FindByEmailAsync(loginDto.Email);

                if (user == null || !user.IsActive)
                {
                    return Unauthorized(new
                    {
                        Success = false,
                        Message = "البريد الإلكتروني أو كلمة المرور غير صحيحة"
                    });
                }

                // محاولة تسجيل الدخول
                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName,
                    loginDto.Password,
                    isPersistent: false,
                    lockoutOnFailure: false);

                if (!result.Succeeded)
                {
                    return Unauthorized(new
                    {
                        Success = false,
                        Message = "البريد الإلكتروني أو كلمة المرور غير صحيحة"
                    });
                }

                // إنشاء التوكن
                var token = await GenerateJwtToken(user);

                // تحديث آخر تاريخ دخول
                user.LastLogin = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            

                // الحصول على معلومات الـ Student إذا كان طالباً
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.ApplicationUserId == user.Id && !s.IsDeleted);

                // إرجاع الاستجابة
                var userResponse = new UserResponseDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    CreatedAt = user.CreatedAt,
                    Token = token,
                    EmailConfirmed = user.EmailConfirmed,
                    HasStudentProfile = student != null,
                    StudentId = student?.Id
                };

                return Ok(new
                {
                    Success = true,
                    Message = "تم تسجيل الدخول بنجاح",
                    Data = userResponse
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء تسجيل الدخول",
                    Error = ex.Message
                });
            }
        }
        private async Task<string> GenerateJwtToken(ApplicationUser user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);

            // الحصول على أدوار المستخدم
            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
           
            };

            // إضافة الأدوار كـ Claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                // الحصول على معرف المستخدم من التوكن
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new
                    {
                        Success = false,
                        Message = "غير مصرح بالوصول"
                    });
                }

                var user = await _userManager.FindByIdAsync(userId);

                if (user == null || !user.IsActive)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = "المستخدم غير موجود"
                    });
                }

                // الحصول على أدوار المستخدم
                var roles = await _userManager.GetRolesAsync(user);

                // الحصول على معلومات الـ Student إذا كان طالباً
                var student = await _context.Students
                    .Include(s => s.StudentCourses)
                    .ThenInclude(sc => sc.Course)
                    .FirstOrDefaultAsync(s => s.ApplicationUserId == user.Id && !s.IsDeleted);

                var studentInfo = student != null ? new
                {
                    StudentId = student.Id,
                    CoursesCount = student.StudentCourses?.Count ?? 0,
                    RegisteredCourses = student.StudentCourses?.Select(sc => new
                    {
                        CourseId = sc.CourseId,
                        CourseName = sc.Course?.NameArabic,
                        RegistrationDate = sc.CreatedAt
                    }).ToList()
                } : null;

                // إرجاع الاستجابة
                var response = new
                {
                    Success = true,
                    Message = "تم جلب الملف الشخصي بنجاح",
                    Data = new
                    {
                        User = new
                        {
                            user.Id,
                            user.FullName,
                            user.Email,
                            user.PhoneNumber,
                            
                            Roles = roles,
                            user.CreatedAt,
                            user.LastLogin,
                            user.IsActive,
                            user.EmailConfirmed,
                            ProfileComplete = !string.IsNullOrEmpty(user.PhoneNumber)
                        },
                        Student = studentInfo
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "حدث خطأ أثناء جلب الملف الشخصي",
                    Error = ex.Message
                });
            }
        }

    }
}
