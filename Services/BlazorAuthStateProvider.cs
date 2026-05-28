using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace NordicBeesERP.Services;

public class BlazorAuthStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedLocalStorage _localStorage;
    private ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());

    public BlazorAuthStateProvider(ProtectedLocalStorage localStorage)
    {
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>("userId");
            if (!result.Success || string.IsNullOrEmpty(result.Value))
                return new AuthenticationState(_anonymous);

            var parts = result.Value.Split('|');
            if (parts.Length < 3) return new AuthenticationState(_anonymous);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, parts[0]),
                new Claim(ClaimTypes.Role, parts[1]),
                new Claim("FullName", parts[2]),
            };
            var identity = new ClaimsIdentity(claims, "Blazor");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return new AuthenticationState(_anonymous);
        }
    }

    public async Task LoginAsync(string email, string role, string fullName)
    {
        await _localStorage.SetAsync("userId", $"{email}|{role}|{fullName}");
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Role, role),
            new Claim("FullName", fullName)
        };
        var identity = new ClaimsIdentity(claims, "Blazor");
        var user = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public async Task LogoutAsync()
    {
        await _localStorage.DeleteAsync("userId");
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }
}