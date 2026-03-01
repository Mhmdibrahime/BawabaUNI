using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class FacultyHousingOption : BaseEntity
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
        public int FacultyId { get; set; }

        // Navigation property
        [ForeignKey("FacultyId")]
        public virtual Faculty Faculty { get; set; }
    }
}
