using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Models.DTOs.Admin.University
{
    //public class DocumentRequiredDto
    //{
    //    [Required]
    //    public string DocumentName { get; set; }

    //    public string Description { get; set; }
    //}

    //public class HousingOptionDto
    //{
    //    [Required]
    //    public string Name { get; set; }

    //    public string PhoneNumber { get; set; }

    //    public string Description { get; set; }

    //    public IFormFile Image { get; set; }
    //}

    //public class CreateUniversityFullDto
    //{
    //    // Step 1: معلومات الجامعة الأساسية
    //    [Required] public string Type { get; set; }
    //    [Required] public string NameArabic { get; set; }
    //    [Required] public string NameEnglish { get; set; }
    //    public bool IsTrending { get; set; }
    //    [Required] public string Description { get; set; }
    //    [Required] public int FoundingYear { get; set; }
    //    public int? StudentsNumber { get; set; }
    //    public int? GlobalRanking { get; set; }
    //    public string Location { get; set; }

    //    // Step 1: صورة الجامعة
    //    public IFormFile UniversityImage { get; set; }

    //    // Step 2: HousingOptions بدون الصور كـ JSON
    //    public string HousingOptionsJson { get; set; } // [{"Name","PhoneNumber","Description"}]
    //    public List<IFormFile> HousingImages { get; set; } // الصور بالترتيب

    //    // Step 2: Documents
    //    public string DocumentsJson { get; set; } // [{"DocumentName","Description"}]

    //    // Step 3: معلومات التواصل
    //    [Required, EmailAddress] public string Email { get; set; }
    //    [Required, Url] public string Website { get; set; }
    //    [Required, Phone] public string PhoneNumber { get; set; }
    //    [Url] public string FacebookPage { get; set; }
    //    [Required] public string Address { get; set; }
    //    public string City { get; set; }
    //    public string Governate { get; set; }
    //    public string PostalCode { get; set; }
    //}

    //public class DocumentRequiredDto
    //{
    //    [Required] public string DocumentName { get; set; }
    //    public string Description { get; set; }
    //}

    //public class HousingOptionDto
    //{
    //    [Required] public string Name { get; set; }
    //    public string PhoneNumber { get; set; }
    //    public string Description { get; set; }
    //}


    public class UniversityFormModel
    {
        // بيانات الجامعة الأساسية
        [Required(ErrorMessage = "نوع الجامعة مطلوب")]
        public string Type { get; set; }

        [Required(ErrorMessage = "اسم الجامعة بالعربية مطلوب")]
        public string NameArabic { get; set; }

        [Required(ErrorMessage = "اسم الجامعة بالإنجليزية مطلوب")]
        public string NameEnglish { get; set; }

        public bool IsTrending { get; set; }

        [Required(ErrorMessage = "وصف الجامعة مطلوب")]
        public string Description { get; set; }

        [Range(1000, 2100, ErrorMessage = "سنة التأسيس غير صالحة")]
        public int FoundingYear { get; set; }

        public int? StudentsNumber { get; set; }
        public string? Location { get; set; }
        public int? GlobalRanking { get; set; }

        [Required(ErrorMessage = "صورة الجامعة مطلوبة")]
        public IFormFile UniversityImage { get; set; }

        // التواصل
        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "بريد إلكتروني غير صالح")]
        public string Email { get; set; }

        [Url(ErrorMessage = "رابط الموقع غير صالح")]
        public string? Website { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        public string PhoneNumber { get; set; }

        [Url(ErrorMessage = "رابط الفيسبوك غير صالح")]
        public string? FacebookPage { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب")]
        public string Address { get; set; }

        public string? City { get; set; }
        public string? Governate { get; set; }
        public string? PostalCode { get; set; }

        // السكن (arrays)
        public List<string>? HousingNames { get; set; }
        public List<string>? HousingPhones { get; set; }
        public List<string>? HousingDescriptions { get; set; }
        public List<IFormFile>? HousingImages { get; set; }

        // المستندات (arrays)
        public List<string>? DocumentNames { get; set; }
        public List<string>? DocumentDescriptions { get; set; }
    }
}
