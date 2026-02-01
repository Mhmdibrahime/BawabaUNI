namespace BawabaUNI.Models.DTOs.Admin.GenralDTOS
{
    public class HeroImageResponseDto
    {
        public int Id { get; set; }
        public string ImagePath { get; set; }
    
        public bool IsActive { get; set; }
     
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string ImageUrl { get; set; }
    }
}
