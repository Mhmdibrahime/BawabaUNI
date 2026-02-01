using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    // 4. Document Required for Submission
    public class DocumentRequired : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string DocumentName { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        // Foreign Key
        public int UniversityId { get; set; }

        // Navigation property
        [ForeignKey("UniversityId")]
        public virtual University University { get; set; }
    }
}
