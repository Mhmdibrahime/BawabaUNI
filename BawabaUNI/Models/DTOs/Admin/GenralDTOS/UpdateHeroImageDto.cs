using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Models.DTOs.Admin.GenralDTOS
{
    public class UpdateHeroImageDto
    {
        public IFormFile? ImageFile { get; set; }

        [Required(ErrorMessage = "IsActive status is required")]
        public bool IsActive { get; set; }

      
    }
}
