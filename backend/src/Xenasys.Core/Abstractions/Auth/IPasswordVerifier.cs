namespace Xenasys.Core.Abstractions.Auth;

// ==========================================================================================
// WHAT: Port for verifying a plain password against a stored one-way hash.
// WHY: Auth must not depend on a concrete hasher; production uses BCrypt only.
// EXAMPLE: Verify(plain, user.PasswordHash) → false on mismatch or malformed hash.
// ==========================================================================================
public interface IPasswordVerifier
{
    bool Verify(string plainPassword, string passwordHash);
}
