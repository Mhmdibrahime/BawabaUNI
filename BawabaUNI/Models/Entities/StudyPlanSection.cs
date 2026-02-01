using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    // 10. Study Plan Section (renamed for clarity)
    public class StudyPlanSection : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(20)]
        public string Code { get; set; } // e.g., "SECTION-A", "MAJOR-COURSES"

        [Range(1, 100)]
        public int? CreditHours { get; set; }

        // Foreign Key to StudyPlanYear
        public int StudyPlanYearId { get; set; }

        // Navigation properties
        [ForeignKey("StudyPlanYearId")]
        public virtual StudyPlanYear StudyPlanYear { get; set; }

        public virtual ICollection<AcademicMaterial> AcademicMaterials { get; set; }
    }
}
