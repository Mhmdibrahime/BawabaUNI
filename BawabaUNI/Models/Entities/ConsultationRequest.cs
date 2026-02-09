using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class ConsultationRequest : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; }

        [Phone]
        [MaxLength(20)]
        public string PhoneNumber { get; set; }

        

        [Required]
        [Column(TypeName = "nvarchar(MAX)")]
        public string Message { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Cancelled

       


        // Foreign Key للطالب (اختياري - إذا كان مسجلاً)
        public int? StudentId { get; set; }

       

 

        [Column(TypeName = "decimal(10,2)")]
        public decimal? ConsultationFee { get; set; }

        public bool IsPaid { get; set; } = false;
        public DateTime? PaymentDate { get; set; }

        [MaxLength(100)]
        public string PaymentReference { get; set; } = "0";

        // Navigation properties
        [ForeignKey("StudentId")]
        public virtual Student? Student { get; set; }

       
    }
}
