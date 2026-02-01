using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    // 5. Housing Option
    public class HousingOption : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [Phone]
        [MaxLength(20)]
        public string PhoneNumber { get; set; }

        [MaxLength(2000)]
        public string Description { get; set; }

        [MaxLength(500)]
        public string ImagePath { get; set; }

        // Foreign Key
        public int UniversityId { get; set; }

        // Navigation property
        [ForeignKey("UniversityId")]
        public virtual University University { get; set; }
    }
}
