namespace Xenasys.Core.Abstractions.Auth;

using Xenasys.Core.Contracts.Permissions;

// ==========================================================================================
// WHAT: Permission check port for fine-grained RBAC beyond role policies.
// WHY: Feature gates need async permission lookups; T1.34 also exposes Form×Action for FE.
// EXAMPLE: HasAsync("products.create"); ListFormsAsync() → nested forms/actions.
// ==========================================================================================
public interface IPermissionGate
{
    Task<bool> HasAsync(string permissionCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListCodesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FormWithActionsDto>> ListFormsAsync(CancellationToken cancellationToken = default);
}
