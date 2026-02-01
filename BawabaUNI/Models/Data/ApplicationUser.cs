using BawabaUNI.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace BawabaUNI.Models.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }

        public virtual Student StudentProfile { get; set; }
    }

}
