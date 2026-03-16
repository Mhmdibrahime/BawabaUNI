using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class StudyAbroad : BaseEntity
    {
        

        [Required]
        [MaxLength(200)]
        public string NameArabic { get; set; }

        [Required]
        [MaxLength(200)]
        public string NameEnglish { get; set; }
        [MaxLength(2000)]
        public string Licenses { get; set; }
        [MaxLength(2000)]
        public string Partnership { get; set; }
        [MaxLength(2000)]
        public string Services { get; set; }


        [Required]
        [Column(TypeName = "nvarchar(MAX)")]
        public string Description { get; set; }

        public string? ImageUrl { get; set; }
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; }

        [Url]
        [MaxLength(500)]
        public string Website { get; set; }

        [Phone]
        [MaxLength(20)]
        public string PhoneNumber { get; set; }

        [Phone]
        [MaxLength(20)]
        public string WhatsAppNumber { get; set; }
        [Url]
        [MaxLength(500)]
        public string FacebookPage { get; set; }

       

       
        // Navigation properties
        public virtual ICollection<DocumentRequiredForStudyAbroad> DocumentsRequired { get; set; }
        public virtual ICollection<HousingOptionForAbroad> HousingOptions { get; set; }
        public virtual ICollection<FacultyForAbroad> Faculties { get; set; }
    }
}
