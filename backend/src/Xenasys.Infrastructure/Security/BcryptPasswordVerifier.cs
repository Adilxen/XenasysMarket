using BCrypt.Net;
using Xenasys.Core.Abstractions.Auth;

namespace Xenasys.Infrastructure.Security;

// ==========================================================================================
// WHAT: Verifies login passwords against PasswordHash (BCrypt or TEMP plaintext).
// WHY: T0.12 allows plaintext in DB for local debugging; BCrypt path kept for hashed rows.
// EXAMPLE: Hash "$2a$12$..." → BCrypt.Verify; value "SuperAdmin@12345" → ordinal equals.
// ==========================================================================================
public sealed class BcryptPasswordVerifier : IPasswordVerifier
{
    public bool Verify(string plainPassword, string passwordHash)
    {
        if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        // T0.12 TEMP: accept plaintext PasswordHash rows until BCrypt seed is restored.
        if (!LooksLikeBcryptHash(passwordHash))
        {
            return string.Equals(plainPassword, passwordHash, StringComparison.Ordinal);
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, passwordHash);
        }
        catch (SaltParseException)
        {
            return false;
        }
    }

    private static bool LooksLikeBcryptHash(string value) =>
        value.StartsWith("$2a$", StringComparison.Ordinal)
        || value.StartsWith("$2b$", StringComparison.Ordinal)
        || value.StartsWith("$2y$", StringComparison.Ordinal);
}
