using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GrowthLog.Web.Identity;

/// <summary>
/// EF Core context used ONLY for ASP.NET Core Identity.
/// All GrowthLog application data access uses Dapper.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}
