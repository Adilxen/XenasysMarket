namespace Xenasys.Core.Abstractions;

public interface ICurrentUser
{
    int? UserId { get; }

    string? Role { get; }

    /// <summary>Display / login name from JWT when present.</summary>
    string? UserName { get; }

    bool IsAuthenticated { get; }
}
