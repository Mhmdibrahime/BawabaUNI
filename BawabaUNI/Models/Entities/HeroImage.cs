using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Models.Entities
{
    // 1. Hero Image
    public class HeroImage : BaseEntity
    {
        [Required]
        [MaxLength(500)]
        public string ImagePath { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

       
    }
}
