namespace Xenasys.Core.Security;

// ==========================================================================================
// WHAT: Maps Form Path + Action Name ↔ legacy PermissionCatalog module.action codes.
// WHY: RoleFormActions is SSoT for UI; [RequirePermission] still needs module.action strings.
// EXAMPLE: (/settings/company, Read) → company.read; View → read.
// ==========================================================================================
public static class FormPermissionBridge
{
    public static string NormalizeAction(string? actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return string.Empty;
        }

        return actionName.Trim().ToLowerInvariant() switch
        {
            "view" or "read" => "read",
            "create" => "create",
            "update" or "edit" => "update",
            "delete" or "remove" => "delete",
            "manage" => "manage",
            "export" => "export",
            var other => other,
        };
    }

    public static string? ModuleForPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var p = path.Trim().ToLowerInvariant();
        if (p.Length > 1 && p.EndsWith('/'))
        {
            p = p.TrimEnd('/');
        }

        return p switch
        {
            "/settings/company" or "/company-profile" => "company",
            "/settings/roles" or "/settings/permission-audit"
                or "/permission-management/roles"
                or "/permission-management/permission-audit" => "roles",
            "/settings/users" or "/permission-management/users" => "users",
            "/settings/forms" or "/permission-management/forms" => "forms",
            "/settings/actions" or "/permission-management/actions" => "actions",
            "/settings/system" => "platform",
            "/categories" or "/sub-categories" => "categories",
            "/brands" => "brands",
            "/products" => "products",
            "/units" => "categories",
            "/stock" => "stock",
            "/vendors" => "vendors",
            "/sales" => "sales",
            "/chart-of-accounts" or "/finance/coa" => "coa.masters",
            "/control-accounts" => "coa.controls",
            "/subsidiary-accounts" => "coa.subsidiaries",
            "/dashboard" => "dashboard",
            "/super/dashboard" => "platform",
            _ => null,
        };
    }

    public static string? ToPermissionCode(string? formPath, string? actionName)
    {
        var module = ModuleForPath(formPath);
        var action = NormalizeAction(actionName);
        if (module is null || action.Length == 0)
        {
            return null;
        }

        // Catalog uses forms.manage / actions.manage / users.manage / roles.manage aliases.
        if (action is "update" or "create" or "delete")
        {
            if (module is "forms" or "actions")
            {
                return $"{module}.manage";
            }

            if (module is "users" && action is "create" or "update" or "delete")
            {
                return "users.manage";
            }
        }

        if (module == "roles" && action is "create" or "update" or "delete" or "manage")
        {
            return action == "read" ? "roles.read" : $"roles.{action}";
        }

        return $"{module}.{action}";
    }

    /// <summary>Maps a legacy permission code back to preferred form path + action name.</summary>
    public static (string Path, string ActionName)? FromPermissionCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var c = code.Trim().ToLowerInvariant();
        var path = c switch
        {
            var x when x.StartsWith("company.", StringComparison.Ordinal) => "/settings/company",
            var x when x.StartsWith("roles.", StringComparison.Ordinal) => "/settings/roles",
            var x when x.StartsWith("users.", StringComparison.Ordinal) => "/settings/users",
            var x when x.StartsWith("forms.", StringComparison.Ordinal) => "/settings/forms",
            var x when x.StartsWith("actions.", StringComparison.Ordinal) => "/settings/actions",
            var x when x.StartsWith("categories.", StringComparison.Ordinal) => "/categories",
            var x when x.StartsWith("brands.", StringComparison.Ordinal) => "/brands",
            var x when x.StartsWith("products.", StringComparison.Ordinal) => "/products",
            var x when x.StartsWith("stock.", StringComparison.Ordinal) => "/stock",
            var x when x.StartsWith("dashboard.", StringComparison.Ordinal) => "/dashboard",
            var x when x.StartsWith("platform.", StringComparison.Ordinal) => "/super/dashboard",
            var x when x.StartsWith("vendors.", StringComparison.Ordinal) => "/vendors",
            var x when x.StartsWith("sales.", StringComparison.Ordinal) => "/sales",
            var x when x.StartsWith("coa.masters.", StringComparison.Ordinal) => "/chart-of-accounts",
            var x when x.StartsWith("coa.controls.", StringComparison.Ordinal) => "/control-accounts",
            var x when x.StartsWith("coa.subsidiaries.", StringComparison.Ordinal) => "/subsidiary-accounts",
            _ => null,
        };

        if (path is null)
        {
            return null;
        }

        var actionPart = c.Contains('.', StringComparison.Ordinal)
            ? c[(c.LastIndexOf('.') + 1)..]
            : c;

        var actionName = actionPart switch
        {
            "read" => "Read",
            "create" => "Create",
            "update" or "manage" => actionPart == "manage" && (c.StartsWith("forms.") || c.StartsWith("actions.") || c.StartsWith("users."))
                ? "Update"
                : actionPart == "manage"
                    ? "Update"
                    : "Update",
            "delete" => "Delete",
            "export" => "Export",
            _ => char.ToUpperInvariant(actionPart[0]) + actionPart[1..],
        };

        if (c is "users.manage")
        {
            return (path, "Update");
        }

        if (c is "forms.manage" or "actions.manage")
        {
            return (path, "Update");
        }

        if (c is "roles.manage")
        {
            return (path, "Update");
        }

        return (path, actionName);
    }
}
