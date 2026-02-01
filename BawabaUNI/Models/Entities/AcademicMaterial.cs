using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    // 11. Academic Material (updated foreign keys)
    public class AcademicMaterial : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; }

        [Range(1, 2)]
        public int Semester { get; set; }

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } // "Mandatory" or "Optional"

        [Range(1, 10)]
        public int CreditHours { get; set; }

       

        // Foreign Keys 
        public int? StudyPlanYearId { get; set; } // Null if it's a Specialized material
        public int? StudyPlanSectionId { get; set; } // Null if it's a general material

        // Navigation properties
        [ForeignKey("StudyPlanYearId")]
        public virtual StudyPlanYear? StudyPlanYear { get; set; }

        [ForeignKey("StudyPlanSectionId")]
        public virtual StudyPlanSection? StudyPlanSection { get; set; }
    }
}
