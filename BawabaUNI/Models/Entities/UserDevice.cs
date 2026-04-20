using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class UserDevice : BaseEntity
    {
        [Required]
        public string UserId { get; set; }
        
        [Required]
        public string FingerprintId { get; set; }
        
        public string DeviceName { get; set; }
        
        public DateTime LastLogin { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
}
