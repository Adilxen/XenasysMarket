using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Entities;
using Xenasys.Core.Security;
using Xenasys.Core.Time;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Seeding;

// ==========================================================================================
// WHAT: Platform TENANT_ADMIN only (CompanyId NULL) — shared Company Admin role.
// WHY: T1.47/T1.48 — no per-company TENANT_* duplicates; TENANT_STAFF retired (T1.48).
// EXAMPLE: New company → no role rows; assign shared TENANT_ADMIN to the company user.
// ==========================================================================================
public static class TenantRoleBootstrap
{
    /// <summary>
    /// Ensures shared TENANT_ADMIN exists (idempotent). Does not create TENANT_STAFF.
    /// </summary>
    public static async Task EnsurePlatformTenantRolesAsync(
        ApplicationDbContext db,
        int actorId,
        DateTime utcNow,
        bool mapFormActions,
        CancellationToken cancellationToken = default)
    {
        await EnsurePlatformRoleAsync(
            db,
            SeedConstants.RoleTenantAdmin,
            "Tenant Administrator",
            "Company administrator with full tenant RBAC (shared across companies)",
            actorId,
            utcNow,
            cancellationToken);

        await EnsurePlatformRoleAsync(
            db,
            SeedConstants.RoleFinance,
            "Finance",
            "Operational finance — Control & Subsidiary Accounts (BRD §3.2 configurable)",
            actorId,
            utcNow,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        // Retire TENANT_STAFF (platform + any leftover company-scoped copies).
        await RetireTenantStaffRolesAsync(db, cancellationToken);

        if (mapFormActions)
        {
            await FormActionRbacSeed.EnsureSystemRoleFormActionsAsync(db, companyId: null, cancellationToken);
        }
    }

    /// <summary>
    /// Remap company-scoped TENANT_ADMIN duplicates onto platform TENANT_ADMIN; retire TENANT_STAFF.
    /// </summary>
    public static async Task ConsolidateCompanyScopedTenantSystemRolesAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var platformAdmin = await db.Roles.FirstOrDefaultAsync(
            r => r.CompanyId == null && r.RoleCode == SeedConstants.RoleTenantAdmin,
            cancellationToken);

        if (platformAdmin is null)
        {
            return;
        }

        if (!platformAdmin.IsActive)
        {
            platformAdmin.IsActive = true;
        }

        var now = AuditClock.Now;

        var adminDuplicates = await db.Roles
            .Where(r => r.CompanyId != null && r.RoleCode == SeedConstants.RoleTenantAdmin)
            .ToListAsync(cancellationToken);

        var activeAdminUserRoles = await db.UserRoles
            .Where(ur => ur.RoleId == platformAdmin.Id && ur.IsActive)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);
        var activeAdminUsers = activeAdminUserRoles.ToHashSet();

        foreach (var dup in adminDuplicates)
        {
            await RemapRoleToTargetAsync(db, dup, platformAdmin.Id, activeAdminUsers, now, cancellationToken);
            dup.IsActive = false;
            dup.UpdateDate = now;
        }

        await RetireTenantStaffRolesAsync(db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Soft-deactivate all TENANT_STAFF roles; remap memberships/UserRoles onto TENANT_ADMIN.
    /// </summary>
    public static async Task RetireTenantStaffRolesAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var platformAdmin = await db.Roles.FirstOrDefaultAsync(
            r => r.CompanyId == null && r.RoleCode == SeedConstants.RoleTenantAdmin,
            cancellationToken);

        var staffRoles = await db.Roles
            .Where(r => r.RoleCode == SeedConstants.RoleTenantStaff)
            .ToListAsync(cancellationToken);

        if (staffRoles.Count == 0)
        {
            return;
        }

        var now = AuditClock.Now;
        var activeAdminUsers = new HashSet<int>();
        if (platformAdmin is not null)
        {
            var ids = await db.UserRoles
                .Where(ur => ur.RoleId == platformAdmin.Id && ur.IsActive)
                .Select(ur => ur.UserId)
                .ToListAsync(cancellationToken);
            activeAdminUsers = ids.ToHashSet();
        }

        foreach (var staff in staffRoles)
        {
            if (platformAdmin is not null)
            {
                await RemapRoleToTargetAsync(
                    db,
                    staff,
                    platformAdmin.Id,
                    activeAdminUsers,
                    now,
                    cancellationToken);
            }
            else
            {
                var grants = await db.RoleFormActions
                    .Where(rfa => rfa.RoleId == staff.Id && rfa.IsActive)
                    .ToListAsync(cancellationToken);
                foreach (var g in grants)
                {
                    g.IsActive = false;
                    g.UpdateDate = now;
                }

                var userRoles = await db.UserRoles
                    .Where(ur => ur.RoleId == staff.Id && ur.IsActive)
                    .ToListAsync(cancellationToken);
                foreach (var ur in userRoles)
                {
                    ur.IsActive = false;
                    ur.UpdateDate = now;
                }
            }

            staff.IsActive = false;
            staff.UpdateDate = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task RemapRoleToTargetAsync(
        ApplicationDbContext db,
        Role source,
        int targetRoleId,
        HashSet<int> usersAlreadyOnTarget,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var memberships = await db.UserTenantMemberships
            .Where(m => m.RoleId == source.Id)
            .ToListAsync(cancellationToken);
        foreach (var m in memberships)
        {
            m.RoleId = targetRoleId;
        }

        var userRoles = await db.UserRoles
            .Where(ur => ur.RoleId == source.Id)
            .ToListAsync(cancellationToken);
        foreach (var ur in userRoles)
        {
            if (usersAlreadyOnTarget.Contains(ur.UserId))
            {
                ur.IsActive = false;
                ur.UpdateDate = now;
            }
            else
            {
                ur.RoleId = targetRoleId;
                ur.IsActive = true;
                ur.UpdateDate = now;
                usersAlreadyOnTarget.Add(ur.UserId);
            }
        }

        var grants = await db.RoleFormActions
            .Where(rfa => rfa.RoleId == source.Id && rfa.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var g in grants)
        {
            g.IsActive = false;
            g.UpdateDate = now;
        }
    }

    private static async Task EnsurePlatformRoleAsync(
        ApplicationDbContext db,
        string roleCode,
        string name,
        string description,
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var existing = await db.Roles.FirstOrDefaultAsync(
            r => r.CompanyId == null && r.RoleCode == roleCode,
            cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.UpdateDate = utcNow;
            }

            return;
        }

        db.Roles.Add(new Role
        {
            CompanyId = null,
            RoleCode = roleCode,
            Name = name,
            Description = description,
            DefaultRoute = DefaultRouteNormalizer.Fallback,
            IsActive = true,
            InsertDate = utcNow,
            InsertUserId = actorId,
        });
    }
}
