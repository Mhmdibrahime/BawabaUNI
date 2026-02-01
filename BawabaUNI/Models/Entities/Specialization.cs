using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    // 7. Specialization (remains the same)
    public class Specialization : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [Range(1, 10)]
        public int YearsNumber { get; set; }

        [Column(TypeName = "nvarchar(MAX)")]
        public string Description { get; set; }

        [MaxLength(200)]
        public string AcademicQualification { get; set; }

        // Foreign Key
        public int FacultyId { get; set; }

        // Navigation property
        [ForeignKey("FacultyId")]
        public virtual Faculty Faculty { get; set; }
    }
}
