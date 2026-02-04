namespace BawabaUNI.Models.DTOs.Admin.CoursesDTOs
{
    public class CourseUpdateRequest
    {
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal? Discount { get; set; }
        public int LessonsNumber { get; set; }
        public int? HoursNumber { get; set; }
        public IFormFile? PosterImage { get; set; }
        public string Classification { get; set; }
        public string InstructorName { get; set; }
        public IFormFile? InstructorImage { get; set; }
        public string InstructorDescription { get; set; }
    }
}
