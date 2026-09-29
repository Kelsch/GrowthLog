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
            [FromForm] string? returnUrl,
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
                var errorUrl = $"/account/register?error={Uri.EscapeDataString(errors)}";
                if (!string.IsNullOrWhiteSpace(returnUrl))
                {
                    errorUrl += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
                }
                return Results.Redirect(errorUrl);
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            return Results.Redirect(SafeReturnUrl(returnUrl));
        });

        app.MapPost("/account/login", async (
            [FromForm] string email,
            [FromForm] string password,
            [FromForm] bool rememberMe,
            [FromForm] string? returnUrl,
            SignInManager<ApplicationUser> signInManager) =>
        {
            var result = await signInManager.PasswordSignInAsync(
                email, password, rememberMe, lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                var errorUrl = "/account/login?error=1";
                if (!string.IsNullOrWhiteSpace(returnUrl))
                {
                    errorUrl += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
                }
                return Results.Redirect(errorUrl);
            }

            return Results.Redirect(SafeReturnUrl(returnUrl));
        });

        app.MapPost("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/");
        });
    }

    /// <summary>
    /// Only allow local (relative) return URLs to avoid open-redirect issues.
    /// </summary>
    private static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return "/";
        if (!returnUrl.StartsWith('/')) return "/";
        if (returnUrl.StartsWith("//")) return "/";
        return returnUrl;
    }
}
