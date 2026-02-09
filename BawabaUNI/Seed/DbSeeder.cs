using BawabaUNI.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Digital_Mall_API.Seed
{
    public static class DbSeeder
    {
        public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            const string adminRole = "Admin";
            const string adminUserName = "BawabaUNIAdmin";
            const string adminEmail = "admin@bawabauni.com";
            const string adminPassword = "Admin@bawabauni";

           
            if (!await roleManager.RoleExistsAsync(adminRole))
            {
                await roleManager.CreateAsync(new IdentityRole(adminRole));
            }

            
            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminUserName,
                    FullName = "Admin",
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, adminRole);
                }
            }
        }
    }
}
