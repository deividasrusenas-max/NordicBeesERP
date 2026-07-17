using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

public interface IAuthService
{
    Task<ErpUser?> ValidateUserAsync(string email, string password);
    Task SeedAdminAsync(string email, string password);
    Task<ErpUser?> GetAuthenticatedUserAsync();
    Task<int?> GetCustomerIdAsync();
    Task<int?> GetUserIdAsync();
    Task<ErpUser?> GetUserByIdAsync(int userId);

    /// <summary>
    /// Returns the display name of the currently authenticated user for audit-log "performed_by" fields.
    /// Throws UnauthorizedAccessException instead of silently falling back to a generic "system" label,
    /// so audit trails never mask a missing user context (see standards §6 AUDIT LOG).
    /// </summary>
    Task<string> GetRequiredActorNameAsync();
}

public class AuthService : IAuthService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public AuthService(IDbContextFactory<NordicBeesERPContext> contextFactory, AuthenticationStateProvider authenticationStateProvider)
    {
        _contextFactory = contextFactory;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<ErpUser?> ValidateUserAsync(string email, string password)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var user = await context.ErpUsers.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        if (user == null) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;
        return user;
    }

    public async Task SeedAdminAsync(string email, string password)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.ErpUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (existing == null)
        {
            context.ErpUsers.Add(new ErpUser
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FullName = "Administratorius",
                Role = "Admin",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }
        else
        {
            var newHash = BCrypt.Net.BCrypt.HashPassword(password);
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE erp_users SET password_hash = {0} WHERE email = {1}",
                newHash, email);
        }
    }

    public async Task<ErpUser?> GetAuthenticatedUserAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var emailClaim = authState.User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrEmpty(emailClaim))
            return null;
        
        return await context.ErpUsers.FirstOrDefaultAsync(u => u.Email == emailClaim && u.IsActive);
    }

    public async Task<int?> GetCustomerIdAsync()
    {
        var user = await GetAuthenticatedUserAsync();
        return null;
    }

    public async Task<int?> GetUserIdAsync()
    {
        var user = await GetAuthenticatedUserAsync();
        return user?.Id;
    }

    public async Task<ErpUser?> GetUserByIdAsync(int userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ErpUsers.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
    }

    public async Task<string> GetRequiredActorNameAsync()
    {
        var user = await GetAuthenticatedUserAsync();
        if (user == null)
            throw new UnauthorizedAccessException("No authenticated user found for audit trail. Please sign in again.");

        return !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Email;
    }
}
