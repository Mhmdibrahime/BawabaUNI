using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Models.Entities
{
    // 2. Partner
    public class Partner : BaseEntity
    {
        [Required]
        [MaxLength(500)]
        public string ImagePath { get; set; }

        [MaxLength(500)]
        [Url]
        public string Link { get; set; }

        
    }
}
