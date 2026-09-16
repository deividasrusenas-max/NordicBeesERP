using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Integration tests for ErpUserService.UpdateUserAsync's self-protection
/// guards (last-active-admin and self-modification), run against the real
/// nordic_bees_erp_test database via DbTestFixture.
/// </summary>
public class ErpUserServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ErpUserServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private ErpUserService MakeService(ErpUser? actor) => new(_fixture.Factory, new FakeAuthService(actor));

    private async Task<ErpUser> InsertUserAsync(string role, bool isActive)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var user = new ErpUser
        {
            Email = $"test-{Guid.NewGuid():N}@example.com",
            PasswordHash = "test-hash",
            FullName = "Test User",
            Role = role,
            IsActive = isActive
        };
        ctx.ErpUsers.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    private async Task DeleteUserAsync(int id)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM erp_users WHERE id = {0}", id);
    }

    [Fact]
    public async Task UpdateUser_LastActiveAdmin_CannotDeactivate()
    {
        var a = await InsertUserAsync("Admin", true);
        try
        {
            var service = MakeService(actor: null);
            var incoming = new ErpUser
            {
                Id = a.Id,
                Email = a.Email,
                PasswordHash = a.PasswordHash,
                FullName = a.FullName,
                Role = "Admin",
                IsActive = false
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateUserAsync(incoming));
            Assert.Equal("Negalite išjungti paskutinio aktyvaus administratoriaus", ex.Message);

            await using var verifyCtx = await _fixture.Factory.CreateDbContextAsync();
            var reloaded = await verifyCtx.ErpUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == a.Id);
            Assert.NotNull(reloaded);
            Assert.True(reloaded!.IsActive);
        }
        finally
        {
            await DeleteUserAsync(a.Id);
        }
    }

    [Fact]
    public async Task UpdateUser_LastActiveAdmin_CannotDemote()
    {
        var a = await InsertUserAsync("Admin", true);
        try
        {
            var service = MakeService(actor: null);
            var incoming = new ErpUser
            {
                Id = a.Id,
                Email = a.Email,
                PasswordHash = a.PasswordHash,
                FullName = a.FullName,
                Role = "Manager",
                IsActive = true
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateUserAsync(incoming));
            Assert.Equal("Negalite pakeisti paskutinio aktyvaus administratoriaus rolės", ex.Message);

            await using var verifyCtx = await _fixture.Factory.CreateDbContextAsync();
            var reloaded = await verifyCtx.ErpUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == a.Id);
            Assert.NotNull(reloaded);
            Assert.Equal("Admin", reloaded!.Role);
        }
        finally
        {
            await DeleteUserAsync(a.Id);
        }
    }

    [Fact]
    public async Task UpdateUser_Self_CannotDeactivate()
    {
        var a = await InsertUserAsync("Admin", true);
        var b = await InsertUserAsync("Admin", true);
        try
        {
            var service = MakeService(actor: a);
            var incoming = new ErpUser
            {
                Id = a.Id,
                Email = a.Email,
                PasswordHash = a.PasswordHash,
                FullName = a.FullName,
                Role = "Admin",
                IsActive = false
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateUserAsync(incoming));
            Assert.Equal("Negalite išjungti savo paskyros", ex.Message);

            await using var verifyCtx = await _fixture.Factory.CreateDbContextAsync();
            var reloaded = await verifyCtx.ErpUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == a.Id);
            Assert.NotNull(reloaded);
            Assert.True(reloaded!.IsActive);
        }
        finally
        {
            await DeleteUserAsync(a.Id);
            await DeleteUserAsync(b.Id);
        }
    }

    [Fact]
    public async Task UpdateUser_Self_CannotDemote()
    {
        var a = await InsertUserAsync("Admin", true);
        var b = await InsertUserAsync("Admin", true);
        try
        {
            var service = MakeService(actor: a);
            var incoming = new ErpUser
            {
                Id = a.Id,
                Email = a.Email,
                PasswordHash = a.PasswordHash,
                FullName = a.FullName,
                Role = "Manager",
                IsActive = true
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateUserAsync(incoming));
            Assert.Equal("Negalite pakeisti savo paskyros rolės", ex.Message);

            await using var verifyCtx = await _fixture.Factory.CreateDbContextAsync();
            var reloaded = await verifyCtx.ErpUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == a.Id);
            Assert.NotNull(reloaded);
            Assert.Equal("Admin", reloaded!.Role);
        }
        finally
        {
            await DeleteUserAsync(a.Id);
            await DeleteUserAsync(b.Id);
        }
    }

    [Fact]
    public async Task UpdateUser_NonLastAdmin_CanDeactivate()
    {
        var a = await InsertUserAsync("Admin", true);
        var b = await InsertUserAsync("Admin", true);
        try
        {
            var service = MakeService(actor: null);
            var incoming = new ErpUser
            {
                Id = a.Id,
                Email = a.Email,
                PasswordHash = a.PasswordHash,
                FullName = a.FullName,
                Role = "Admin",
                IsActive = false
            };

            await service.UpdateUserAsync(incoming); // must not throw

            await using var verifyCtx = await _fixture.Factory.CreateDbContextAsync();
            var reloadedA = await verifyCtx.ErpUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == a.Id);
            Assert.NotNull(reloadedA);
            Assert.False(reloadedA!.IsActive);

            var reloadedB = await verifyCtx.ErpUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == b.Id);
            Assert.NotNull(reloadedB);
            Assert.True(reloadedB!.IsActive);
        }
        finally
        {
            await DeleteUserAsync(a.Id);
            await DeleteUserAsync(b.Id);
        }
    }

    private sealed class FakeAuthService : IAuthService
    {
        private readonly ErpUser? _user;

        public FakeAuthService(ErpUser? user) => _user = user;

        public Task<ErpUser?> GetAuthenticatedUserAsync() => Task.FromResult(_user);

        public Task<ErpUser?> ValidateUserAsync(string email, string password) => throw new NotImplementedException();
        public Task SeedAdminAsync(string email, string password) => throw new NotImplementedException();
        public Task<int?> GetCustomerIdAsync() => throw new NotImplementedException();
        public Task<int?> GetUserIdAsync() => throw new NotImplementedException();
        public Task<ErpUser?> GetUserByIdAsync(int userId) => throw new NotImplementedException();
        public Task<string> GetRequiredActorNameAsync() => throw new NotImplementedException();
    }
}
