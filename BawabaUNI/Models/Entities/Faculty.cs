using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    // 6. Faculty (updated to remove StudyPlanId foreign key)
    public class Faculty : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string NameArabic { get; set; }

        [Required]
        [MaxLength(200)]
        public string NameEnglish { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(MAX)")]
        public string Description { get; set; }

        [Range(0, int.MaxValue)]
        public int? StudentsNumber { get; set; }

        [MaxLength(100)]
        public string DurationOfStudy { get; set; }

        [Range(0, int.MaxValue)]
        public int? ProgramsNumber { get; set; }

        [Range(1, int.MaxValue)]
        public int? Rank { get; set; }

        [MaxLength(1000)]
        public string Specializations { get; set; }

        [Required]
        public bool RequireAcceptanceTests { get; set; }

        // Foreign Key
        public int UniversityId { get; set; }

        // Navigation properties
        [ForeignKey("UniversityId")]
        public virtual University University { get; set; }

        // Updated: A faculty can have multiple study plan years
        public virtual ICollection<StudyPlanYear> StudyPlanYears { get; set; }
        public virtual ICollection<Specialization> SpecializationList { get; set; }
        public virtual ICollection<JobOpportunity> JobOpportunities { get; set; }
    }
}
