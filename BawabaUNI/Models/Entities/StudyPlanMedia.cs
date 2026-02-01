using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    // 9. Study Plan Media (updated foreign key)
    public class StudyPlanMedia : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string MediaType { get; set; }

        [Required]
        [MaxLength(500)]
        public string MediaLink { get; set; }

        [MaxLength(500)]
        [Url]
        public string VisitLink { get; set; }

        // Foreign Key to StudyPlanYear
        public int StudyPlanYearId { get; set; }

        // Navigation property
        [ForeignKey("StudyPlanYearId")]
        public virtual StudyPlanYear StudyPlanYear { get; set; }
    }
}
