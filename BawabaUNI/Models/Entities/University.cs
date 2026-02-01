using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    // 3. University
    public class University : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string Type { get; set; } // Public, Private, International

        [Required]
        [MaxLength(200)]
        public string NameArabic { get; set; }

        [Required]
        [MaxLength(200)]
        public string NameEnglish { get; set; }

        [Required]
        public bool IsTrending { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(MAX)")]
        public string Description { get; set; }

        [Range(1000, 2100)]
        public int FoundingYear { get; set; }

        [Range(0, int.MaxValue)]
        public int? StudentsNumber { get; set; }

        [MaxLength(500)]
        public string Location { get; set; }

        [Range(1, int.MaxValue)]
        public int? GlobalRanking { get; set; }

        [Required]
        [MaxLength(500)]
        public string UniversityImage { get; set; }

        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; }

        [Url]
        [MaxLength(500)]
        public string Website { get; set; }

        [Phone]
        [MaxLength(20)]
        public string PhoneNumber { get; set; }

        [Url]
        [MaxLength(500)]
        public string FacebookPage { get; set; }

        [MaxLength(500)]
        public string Address { get; set; }

        [MaxLength(100)]
        public string City { get; set; }

        [MaxLength(100)]
        public string Governate { get; set; }

        [MaxLength(20)]
        public string PostalCode { get; set; }

        // Navigation properties
        public virtual ICollection<DocumentRequired> DocumentsRequired { get; set; }
        public virtual ICollection<HousingOption> HousingOptions { get; set; }
        public virtual ICollection<Faculty> Faculties { get; set; }
    }
}
