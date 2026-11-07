using Microsoft.AspNetCore.Identity;
using SmartPension.Api.Models;

namespace SmartPension.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var r in new[] { "Admin", "Pensioner" })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        const string email = "admin@pension.local";
        if (await users.FindByEmailAsync(email) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = email, Email = email, FullName = "System Admin",
                EmailConfirmed = true, TwoFactorEnabled = true
            };
            await users.CreateAsync(admin, "Admin@12345");
            await users.AddToRoleAsync(admin, "Admin");
        }
    }
}
