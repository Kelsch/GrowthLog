using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GrowthLog.Web.Identity;

public static class IdentityEndpoints
{
    public static void MapAdditionalIdentityEndpoints(this WebApplication app)
    {
        app.MapPost("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/");
        }).RequireAuthorization();

        app.MapPost("/account/login", async (
            [FromForm] string email,
            [FromForm] string password,
            [FromForm] bool rememberMe,
            SignInManager<ApplicationUser> signInManager) =>
        {
            var result = await signInManager.PasswordSignInAsync(
                email, password, rememberMe, lockoutOnFailure: false);

            return result.Succeeded
                ? Results.Redirect("/")
                : Results.Redirect("/account/login?error=1");
        });
    }
}
