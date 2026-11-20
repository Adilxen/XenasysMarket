namespace Xenasys.Core.Abstractions.Validation;

// ==========================================================================================
// WHAT: Contract for validating that a foreign-key INT refers to an active dropdown row.
// WHY: BRD §2 — FKs must come from active PK options, never free-text / inactive ids.
// EXAMPLE: EnsureActiveAsync("tenants", companyId) before login membership bind.
// ==========================================================================================
public interface IActiveForeignKeyValidator
{
    Task<bool> IsActiveTenantAsync(int companyId, CancellationToken cancellationToken = default);

    Task EnsureActiveTenantAsync(int companyId, CancellationToken cancellationToken = default);
}
