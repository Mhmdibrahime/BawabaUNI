using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    // 12. Job Opportunity 
    public class JobOpportunity : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        public int FacultyId { get; set; }

        [ForeignKey("FacultyId")]
        public virtual Faculty Faculty { get; set; }
    }
}
