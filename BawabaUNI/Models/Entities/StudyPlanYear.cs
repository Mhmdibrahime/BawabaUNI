using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    // 8. Study Plan Year (renamed and updated)
    public class StudyPlanYear : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string YearName { get; set; } // e.g., "First Year", "Second Year"

        [Range(1, 10)]
        public int YearNumber { get; set; } // 1, 2, 3, 4...

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } // "General" or "Specialized"

        // Foreign Key to Faculty (many-to-one)
        public int FacultyId { get; set; }

        // Navigation properties
        [ForeignKey("FacultyId")]
        public virtual Faculty Faculty { get; set; }

        public virtual ICollection<StudyPlanMedia> StudyPlanMedia { get; set; }
        public virtual ICollection<StudyPlanSection> Sections { get; set; }
        public virtual ICollection<AcademicMaterial> AcademicMaterials { get; set; }
    }
}
