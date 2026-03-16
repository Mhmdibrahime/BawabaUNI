using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    // 4. Document Required for Submission
    public class DocumentRequiredForStudyAbroad : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string DocumentName { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        // Foreign Key
        public int StudyAbroadId { get; set; }

        // Navigation property
        [ForeignKey("StudyAbroadId")]
        public virtual StudyAbroad StudyAbroad { get; set; }
    }
}
