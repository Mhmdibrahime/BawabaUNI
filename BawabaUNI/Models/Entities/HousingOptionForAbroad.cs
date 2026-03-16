using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class HousingOptionForAbroad : BaseEntity
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
        public int StudyAbroadId { get; set; }

        // Navigation property
        [ForeignKey("StudyAbroadId")]
        public virtual StudyAbroad StudyAbroad { get; set; }
    }
}
