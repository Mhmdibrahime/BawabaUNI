using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class  FacultyForAbroad : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string NameArabic { get; set; }

        [Required]
        [MaxLength(200)]
        public string NameEnglish { get; set; }
        [MaxLength(255)]
        public string ImageUrl { get; set; }
        public decimal Expenses { get; set; }
        public decimal Coordination { get; set; }
        public int StudyAbroadId { get; set; }

        [ForeignKey("StudyAbroadId")]
        public virtual StudyAbroad StudyAbroad { get; set; }
    }
}
