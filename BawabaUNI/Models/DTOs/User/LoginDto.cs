using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Models.DTOs.User
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [StringLength(100, ErrorMessage = "الاسم لا يمكن أن يتجاوز 100 حرف")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string Email { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب أن تكون بين 6 و 100 حرف")]
        //[RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
        //    ErrorMessage = "كلمة المرور يجب أن تحتوي على حرف كبير وحرف صغير ورقم")]
        public string Password { get; set; }

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare("Password", ErrorMessage = "كلمة المرور وتأكيدها غير متطابقتين")]
        public string ConfirmPassword { get; set; }

        [Phone(ErrorMessage = "رقم الهاتف غير صحيح")]
        public string PhoneNumber { get; set; }

        //public string UserType { get; set; } = "Student"; 
    }

    public class LoginDto
    {
        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string Email { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        public string Password { get; set; }
    }

    public class UserResponseDto
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string UserType { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Token { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool HasStudentProfile { get; set; }
        public int? StudentId { get; set; }
    }
    public class PartnerResponseDto
    {
        public int Id { get; set; }
        public string ImagePath { get; set; }
        public string Link { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int DisplayOrder { get; set; } // يمكن إضافته لترتيب العرض
    }
    public class ConsultationRequestDto
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [StringLength(200, ErrorMessage = "الاسم لا يمكن أن يتجاوز 200 حرف")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        [StringLength(100, ErrorMessage = "البريد الإلكتروني لا يمكن أن يتجاوز 100 حرف")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "رقم الهاتف غير صحيح")]
        [StringLength(20, ErrorMessage = "رقم الهاتف لا يمكن أن يتجاوز 20 رقم")]
        public string PhoneNumber { get; set; }

       

        [Required(ErrorMessage = "الرسالة مطلوبة")]
        [StringLength(5000, ErrorMessage = "الرسالة لا يمكن أن تتجاوز 5000 حرف")]
        public string Message { get; set; }

      

        // تم إزالة StudentId من هنا لأنه سيتم الحصول عليه من الـ Token
    }

    // DTO جديد للطلبات من المستخدمين المسجلين
    public class ConsultationRequestFromStudentDto
    {
        
        [Required(ErrorMessage = "الرسالة مطلوبة")]
        [StringLength(5000, ErrorMessage = "الرسالة لا يمكن أن تتجاوز 5000 حرف")]
        public string Message { get; set; }

      

        // لا نحتاج للبيانات الشخصية لأنها موجودة في الـ Profile
    }

    public class ConsultationResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public string RequestType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsPaid { get; set; }
        public string PaymentReference { get; set; }
        public int? StudentId { get; set; }
        public string StudentName { get; set; }
        public string AssignedToName { get; set; }
    }
    public class VideoCourseDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int DurationInMinutes { get; set; }
        public bool IsPaid { get; set; }
        public string Description { get; set; }
        public bool CanAccess { get; set; } 
        public string AccessMessage { get; set; } 
        public string? PlayerEmbedUrl { get; set; } 
        public string? VimeoId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    
    public class FullVideoDto : VideoCourseDto
    {
        public string VideoLink { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; }
    }


    public class VideoAccessCheckDto
    {
        public bool HasAccess { get; set; }
        public string Message { get; set; }
        public bool IsCoursePaid { get; set; }
        public bool IsVideoFree { get; set; }
        public bool IsTrialAvailable { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public DateTime? AccessUntil { get; set; }
    }
}
