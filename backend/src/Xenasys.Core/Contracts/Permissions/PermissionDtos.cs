using Xenasys.Core.Contracts.Forms;

namespace Xenasys.Core.Contracts.Permissions;

// ==========================================================================================
// WHAT: Permission catalog / audit / current-user DTOs for RBAC management module.
// WHY: T1.34 — Form×Action nested payload + legacy permissionCodes bridge for RequirePermission.
// EXAMPLE: GET /api/permissions/me → forms[{ path, actions[] }] + permissionCodes[].
// ==========================================================================================

public sealed class PermissionDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsSystem { get; set; }
}

public sealed class PermissionModuleGroupDto
{
    public string Module { get; set; } = string.Empty;

    public IReadOnlyList<PermissionDto> Permissions { get; set; } = [];
}

public sealed class PermissionAuditCellDto
{
    public string PermissionCode { get; set; } = string.Empty;

    public bool Granted { get; set; }
}

public sealed class PermissionAuditRoleColumnDto
{
    public int RoleId { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public IReadOnlyList<PermissionAuditCellDto> Cells { get; set; } = [];
}

public sealed class PermissionAuditMatrixDto
{
    public IReadOnlyList<PermissionDto> Permissions { get; set; } = [];

    public IReadOnlyList<PermissionAuditRoleColumnDto> Roles { get; set; } = [];
}

public sealed class FormActionRefDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class FormWithActionsDto
{
    public int Id { get; set; }

    public string Path { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public IReadOnlyList<FormActionRefDto> Actions { get; set; } = [];
}

public sealed class CurrentUserPermissionsDto
{
    public string Role { get; set; } = string.Empty;

    public int? CompanyId { get; set; }

    /// <summary>Effective Form×Action grants for the JWT role (SSoT for FE hasForm/hasAction).</summary>
    public IReadOnlyList<FormWithActionsDto> Forms { get; set; } = [];

    /// <summary>Derived module.action codes for ClaimGate / RequirePermission bridge.</summary>
    public IReadOnlyList<string> PermissionCodes { get; set; } = [];
}

public sealed class FormActionAssignmentDto
{
    public int ActionId { get; set; }

    public string ActionName { get; set; } = string.Empty;

    public bool IsAssigned { get; set; }
}

public sealed class SyncFormActionsRequest
{
    public IReadOnlyList<int> ActionIds { get; set; } = [];
}

public sealed class RoleFormActionItemDto
{
    public int FormId { get; set; }

    public IReadOnlyList<int> ActionIds { get; set; } = [];
}

public sealed class SyncRoleFormActionsRequest
{
    public IReadOnlyList<RoleFormActionItemDto> Items { get; set; } = [];
}

public sealed class RoleFormActionsDto
{
    public int RoleId { get; set; }

    public IReadOnlyList<FormWithActionsDto> Forms { get; set; } = [];
}

/// <summary>Role chip on a Form×Action audit row (newSalon RoleSummary).</summary>
public sealed class FormActionAuditRoleRefDto
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string RoleCode { get; set; } = string.Empty;
}

/// <summary>Action + assigned roles for permission audit (newSalon FormActionAudit).</summary>
public sealed class FormActionAuditActionDto
{
    public int ActionId { get; set; }

    public string ActionName { get; set; } = string.Empty;

    public IReadOnlyList<FormActionAuditRoleRefDto> Roles { get; set; } = [];
}

/// <summary>Form accordion row — actions with role chips (newSalon FormAudit).</summary>
public sealed class FormActionAuditFormDto
{
    public int FormId { get; set; }

    public string FormPath { get; set; } = string.Empty;

    public string FormName { get; set; } = string.Empty;

    public int TotalActionCount { get; set; }

    public int AssignedActionCount { get; set; }

    public IReadOnlyList<FormActionAuditActionDto> Actions { get; set; } = [];
}

/// <summary>Form-centric audit response (newSalon GetFormActionRoleAudit).</summary>
public sealed class FormActionAuditMatrixDto
{
    public IReadOnlyList<FormActionAuditFormDto> Forms { get; set; } = [];
}
