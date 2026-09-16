using System.Security.Claims;

namespace NordicBeesERP.Helpers;

public static class AuthClaims
{
    public const string CookieScheme = "Cookies";
    public const string FullNameClaimType = "FullName";

    // Builds the same claims principal shape as BlazorAuthStateProvider.GetAuthenticationStateAsync.
    public static ClaimsPrincipal CreateClaimsPrincipal(string email, string role, string fullName)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(FullNameClaimType, fullName),
        };
        var identity = new ClaimsIdentity(claims, "Blazor");
        return new ClaimsPrincipal(identity);
    }
}
