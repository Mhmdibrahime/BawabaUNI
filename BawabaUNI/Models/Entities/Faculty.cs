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
        [MaxLength(20)]

        public string Type { get; set; } = "كلية";
        public bool? HasHousing { get; set; } // هل يوجد سكن؟


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


        [Required]
        public bool RequireAcceptanceTests { get; set; }
        public decimal Expenses { get; set; }
        public decimal Coordination  { get; set; }

        [MaxLength(500)]
        public string? GroupLink { get; set; }

        [MaxLength(500)]
        public string? InstitutePageLink { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }
        [MaxLength(500)]
        public string? DescriptionOfStudyPlan { get; set; }
        // Foreign Key
        public int? UniversityId { get; set; }

        // Navigation properties
        [ForeignKey("UniversityId")]
        public virtual University? University { get; set; }

        // Updated: A faculty can have multiple study plan years
        public virtual ICollection<StudyPlanYear> StudyPlanYears { get; set; }
        public virtual ICollection<Specialization> SpecializationList { get; set; }
        public virtual ICollection<JobOpportunity> JobOpportunities { get; set; }
        public virtual ICollection<FacultyHousingOption> FacultyHousingOption { get; set; }
    }
}
