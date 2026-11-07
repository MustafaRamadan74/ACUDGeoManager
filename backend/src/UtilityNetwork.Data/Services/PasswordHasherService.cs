using Microsoft.AspNetCore.Identity;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object DummyUser = new();

    public string HashPassword(string password)
    {
        return _hasher.HashPassword(DummyUser, password);
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(DummyUser, hashedPassword, providedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
