using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Http;

namespace NordicBeesERP.Services;

public class BlazorAuthStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ProtectedLocalStorage _localStorage;
    private ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());

    public BlazorAuthStateProvider(IHttpContextAccessor httpContextAccessor, ProtectedLocalStorage localStorage)
    {
        _httpContextAccessor = httpContextAccessor;
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Cookie auth is authoritative whenever an HttpContext exists:
        // during static SSR this is the current request; during an
        // interactive circuit it is the circuit-establishing request,
        // captured at SignalR connection start (MS docs: blazor/components/httpcontext).
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
            return new AuthenticationState(httpContext.User);

        // Fallback only when HttpContext is unavailable: legacy
        // ProtectedLocalStorage state from the pre-cookie login flow.
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

    public async Task LogoutAsync()
    {
        await _localStorage.DeleteAsync("userId");
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }
}
