using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;

namespace Spacearr.Auth;

public interface IUserService
{
    Task<bool> IsSetupCompleteAsync();
    Task<User> CreateAdminAsync(string username, string password);
    Task<User?> ValidateAsync(string username, string password);
    Task<User?> FindByApiKeyAsync(string apiKey);
    Task<User?> FindByIdAsync(int id);
    Task<string> RegenerateApiKeyAsync(int userId);
    Task ChangePasswordAsync(int userId, string newPassword);
}

public sealed class UserService : IUserService
{
    private readonly SpacearrDb _db;
    private readonly PasswordHasher<User> _hasher = new();

    public UserService(SpacearrDb db) => _db = db;

    public Task<bool> IsSetupCompleteAsync() => _db.Users.AnyAsync();

    public async Task<User> CreateAdminAsync(string username, string password)
    {
        var user = new User { Username = username.Trim(), ApiKey = NewApiKey(), CreatedAt = DateTime.UtcNow };
        user.PasswordHash = _hasher.HashPassword(user, password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<User?> ValidateAsync(string username, string password)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == username.Trim());
        if (user is null) return null;
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }

    public Task<User?> FindByApiKeyAsync(string apiKey) =>
        _db.Users.SingleOrDefaultAsync(u => u.ApiKey == apiKey);

    public Task<User?> FindByIdAsync(int id) =>
        _db.Users.SingleOrDefaultAsync(u => u.Id == id);

    public async Task<string> RegenerateApiKeyAsync(int userId)
    {
        var user = await _db.Users.SingleAsync(u => u.Id == userId);
        user.ApiKey = NewApiKey();
        await _db.SaveChangesAsync();
        return user.ApiKey;
    }

    public async Task ChangePasswordAsync(int userId, string newPassword)
    {
        var user = await _db.Users.SingleAsync(u => u.Id == userId);
        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        await _db.SaveChangesAsync();
    }

    private static string NewApiKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
