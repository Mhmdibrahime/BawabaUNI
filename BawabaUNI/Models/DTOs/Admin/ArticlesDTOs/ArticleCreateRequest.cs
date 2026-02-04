namespace BawabaUNI.Models.DTOs.Admin.ArticlesDTOs
{
    // DTOs
    public class ArticleCreateRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public IFormFile Image { get; set; }
        public string AuthorName { get; set; }
        public IFormFile AuthorImage { get; set; }
        public string Content { get; set; }
        public DateTime Date { get; set; }
        public int ReadTime { get; set; }
    }
}
