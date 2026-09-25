using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GrowthLog.Web.Identity;

/// <summary>
/// Auth endpoints that must run outside the interactive circuit so they can
/// set/clear the auth cookie before the response starts streaming.
/// </summary>
public static class IdentityEndpoints
{
    public static void MapAdditionalIdentityEndpoints(this WebApplication app)
    {
        app.MapPost("/account/register", async (
            HttpContext http,
            [FromForm] string displayName,
            [FromForm] string email,
            [FromForm] string password,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager) =>
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = displayName
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join("|", result.Errors.Select(e => e.Description));
                return Results.Redirect($"/account/register?error={Uri.EscapeDataString(errors)}");
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            return Results.Redirect("/");
        }).DisableAntiforgery();

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
        }).DisableAntiforgery();

        app.MapPost("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/");
        }).DisableAntiforgery();
    }
}
