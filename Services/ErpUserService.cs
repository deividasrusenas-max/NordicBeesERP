using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

public interface IErpUserService
{
    Task<List<ErpUser>> GetUsersAsync();
    Task<ErpUser?> GetUserAsync(int id);
    Task<ErpUser> CreateUserAsync(ErpUser user, string plainPassword);
    Task<ErpUser> UpdateUserAsync(ErpUser user);
    Task ResetPasswordAsync(int userId, string newPassword);
    Task<bool> DeleteUserAsync(int id);
}

public class ErpUserService : IErpUserService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
    private readonly IAuthService _authService;

    public ErpUserService(IDbContextFactory<NordicBeesERPContext> contextFactory, IAuthService authService)
    {
        _contextFactory = contextFactory;
        _authService = authService;
    }

    public async Task<List<ErpUser>> GetUsersAsync()
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        return await ctx.ErpUsers.OrderBy(u => u.FullName).ToListAsync();
    }

    public async Task<ErpUser?> GetUserAsync(int id)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        return await ctx.ErpUsers.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<ErpUser> CreateUserAsync(ErpUser user, string plainPassword)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
        user.CreatedAt = DateTime.Now;
        ctx.ErpUsers.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    public async Task<ErpUser> UpdateUserAsync(ErpUser user)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var existing = await ctx.ErpUsers.FirstOrDefaultAsync(u => u.Id == user.Id)
            ?? throw new InvalidOperationException("Vartotojas nerastas");

        var activeAdminCount = await ctx.ErpUsers.CountAsync(u => u.Role == "Admin" && u.IsActive);
        var isLastActiveAdmin = existing.Role == "Admin" && existing.IsActive && activeAdminCount == 1;

        // Guard 1: refuse to deactivate the last active Admin (would lock the system out).
        if (isLastActiveAdmin && !user.IsActive)
            throw new InvalidOperationException("Negalite išjungti paskutinio aktyvaus administratoriaus");

        // Guard 2: refuse to change the last active Admin's role away from Admin.
        if (isLastActiveAdmin && user.Role != "Admin")
            throw new InvalidOperationException("Negalite pakeisti paskutinio aktyvaus administratoriaus rolės");

        // Guard 3: refuse to let a user deactivate or demote their own account.
        var actor = await _authService.GetAuthenticatedUserAsync();
        if (actor != null && actor.Id == existing.Id)
        {
            if (existing.IsActive && !user.IsActive)
                throw new InvalidOperationException("Negalite išjungti savo paskyros");
            if (existing.Role == "Admin" && user.Role != "Admin")
                throw new InvalidOperationException("Negalite pakeisti savo paskyros rolės");
        }

        existing.FullName = user.FullName;
        existing.Email = user.Email;
        existing.Role = user.Role;
        existing.IsActive = user.IsActive;
        ctx.Entry(existing).State = EntityState.Modified;
        await ctx.SaveChangesAsync();
        return existing;
    }

    public async Task ResetPasswordAsync(int userId, string newPassword)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var user = await ctx.ErpUsers.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("Vartotojas nerastas");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        ctx.Entry(user).State = EntityState.Modified;
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var user = await ctx.ErpUsers.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return false;
        ctx.ErpUsers.Remove(user);
        await ctx.SaveChangesAsync();
        return true;
    }
}
