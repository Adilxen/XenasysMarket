using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenasys.Core.Entities;
using Xenasys.Core.Security;
using Xenasys.Core.Services;
using Xenasys.Core.Time;
using Xenasys.Persistence.Coding;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Seeding;

// ==========================================================================================
// WHAT: Idempotent baseline seeder for Super Admin, master tenant, RBAC, and COA nodes.
// WHY: Fresh DBs (and upgrades) need a known platform identity + permission matrix to boot.
// EXAMPLE: SeedAsync() ensures SuperAdmin user, MST001 tenant, roles, Inventory/AP controls.
// ==========================================================================================
public sealed class DatabaseSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(ApplicationDbContext db, ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ==========================================================================================
    // WHAT: Orchestrates seed steps in dependency order, then SaveChanges once at the end.
    // WHY: Actor â†’ tenant â†’ roles â†’ permissions â†’ memberships â†’ COA â†’ product sequence.
    // EXAMPLE: EnsureSuperAdminActorAsync first so later InsertUserId values are real PKs.
    // ==========================================================================================
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = AuditClock.Now;
        var actorId = await EnsureSuperAdminActorAsync(now, cancellationToken);

        var tenantId = await EnsureMasterTenantAsync(actorId, now, cancellationToken);
        await EnsurePlatformRolesAsync(tenantId, actorId, now, cancellationToken);
        await EnsureCatalogActionsAsync(actorId, now, cancellationToken);
        await EnsureCatalogFormsAsync(actorId, now, cancellationToken);
        await FormActionRbacSeed.EnsureFormActionCatalogAsync(_db, actorId, now, cancellationToken);
        await FormActionRbacSeed.EnsureSystemRoleFormActionsAsync(_db, companyId: null, cancellationToken);
        await EnsureSuperAdminPlatformMembershipAsync(actorId, now, cancellationToken);
        // T1.50 â€” do not auto-seed tenantadmin user; Super Admin provisions Tenant Admins via UI.
        await EnsureUserRolesBackfillAsync(actorId, now, cancellationToken);
        await EnsureCoaAutomationNodesAsync(actorId, now, cancellationToken);
        await EnsureDemoSubsidiaryAccountsAsync(actorId, now, cancellationToken);
        await EnsureProductCodeSequenceAsync(tenantId, cancellationToken);
        await EnsureTenantCodeSequenceAsync(cancellationToken);
        await EnsureUnitsAsync(cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seed engine complete (actor={ActorId})", actorId);
    }

    /// <summary>
    /// Super Admin is created first so every subsequent InsertUserId is a real PK.
    /// Id left at 0 for IDENTITY; InsertUserId patched after insert.
    /// </summary>
    private async Task<int> EnsureSuperAdminActorAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Users
            .FirstOrDefaultAsync(
                u => u.UserName == SeedConstants.SuperAdminUserName,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.InsertUserId == 0)
            {
                existing.InsertUserId = existing.Id;
            }

            if (string.IsNullOrWhiteSpace(existing.UserNn))
            {
                existing.UserNn = SeedConstants.SuperAdminUserNn;
            }

            // T0.12 TEMP: force plaintext password for local debugging (replace before M0 gate).
            existing.PasswordHash = SeedConstants.SuperAdminPlaceholderPassword;

            await _db.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var user = new User
        {
            UserName = SeedConstants.SuperAdminUserName,
            Email = SeedConstants.SuperAdminEmail,
            // T0.12 TEMP: store plaintext â€” restore BCrypt before production / Module 0 Done gate.
            PasswordHash = SeedConstants.SuperAdminPlaceholderPassword,
            UserNn = SeedConstants.SuperAdminUserNn,
            IsActive = true,
            IsSuperAdmin = true,
            InsertDate = utcNow,
            InsertUserId = 0,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        user.InsertUserId = user.Id;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded Super Admin '{User}' Id={Id}",
            SeedConstants.SuperAdminUserName,
            user.Id);
        return user.Id;
    }

    private async Task<int> EnsureMasterTenantAsync(
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Tenants
            .FirstOrDefaultAsync(t => t.TenantCode == SeedConstants.MasterTenantCode, cancellationToken);

        if (existing is not null)
        {
            if (existing.InsertUserId == 0)
            {
                existing.InsertUserId = actorId;
                existing.UpdateDate = utcNow;
                existing.UpdateUserId = actorId;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var tenant = new Tenant
        {
            Name = SeedConstants.MasterTenantName,
            TenantCode = SeedConstants.MasterTenantCode,
            IsActive = true,
            ContactEmail = "platform@xenasys.local",
            InsertDate = utcNow,
            InsertUserId = actorId,
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Seeded master tenant {Code} Id={Id}",
            SeedConstants.MasterTenantCode,
            tenant.Id);
        return tenant.Id;
    }

    private async Task EnsurePlatformRolesAsync(
        int masterTenantId,
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!await _db.Roles.AnyAsync(
                r => r.RoleCode == SeedConstants.RoleSuperAdmin && r.CompanyId == null,
                cancellationToken))
        {
            _db.Roles.Add(new Role
            {
                CompanyId = null,
                RoleCode = SeedConstants.RoleSuperAdmin,
                Name = "Super Admin",
                Description = "Platform super administrator",
                DefaultRoute = DefaultRouteNormalizer.Fallback,
                IsActive = true,
                InsertDate = utcNow,
                InsertUserId = actorId,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        await TenantRoleBootstrap.EnsurePlatformTenantRolesAsync(
            _db,
            actorId,
            utcNow,
            mapFormActions: false,
            cancellationToken);

        await TenantRoleBootstrap.ConsolidateCompanyScopedTenantSystemRolesAsync(
            _db,
            cancellationToken);

        // Backfill DefaultRoute on existing roles (T1.40).
        var missingRoute = await _db.Roles
            .Where(r => r.DefaultRoute == null || r.DefaultRoute == "")
            .ToListAsync(cancellationToken);
        foreach (var role in missingRoute)
        {
            role.DefaultRoute = DefaultRouteNormalizer.Fallback;
        }

        if (missingRoute.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Backfilled DefaultRoute=/dashboard on {Count} role(s)",
                missingRoute.Count);
        }
    }

    /// <summary>
    /// Seeds baseline action verbs (idempotent by Name). Claim gates come later.
    /// </summary>
    private async Task EnsureCatalogActionsAsync(
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        string[] names =
        [
            "Create",
            "Read",
            "Update",
            "Delete",
            "View",
            "Export",
        ];

        foreach (var name in names)
        {
            if (await _db.Actions.AnyAsync(a => a.Name == name, cancellationToken))
            {
                continue;
            }

            _db.Actions.Add(new ActionItem
            {
                Name = name,
                IsActive = true,
                InsertDate = utcNow,
                InsertUserId = actorId,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded Actions catalog ({Count} baseline verbs)", names.Length);
    }

    /// <summary>
    /// Seeds navigable application forms from the sidebar IA (idempotent by Path).
    /// </summary>
    private async Task EnsureCatalogFormsAsync(
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        (string Name, string Path)[] forms =
        [
            ("Dashboard", "/dashboard"),
            ("Categories", "/categories"),
            ("Sub Categories", "/sub-categories"),
            ("Brands", "/brands"),
            ("Products / SKU", "/products"),
            ("Stock Management", "/stock"),
            ("Sales Orders", "/sales"),
            ("Vendors", "/vendors"),
            ("Chart of Accounts", "/chart-of-accounts"),
            ("Control Accounts", "/control-accounts"),
            ("Subsidiary Accounts", "/subsidiary-accounts"),
            ("COA Tree", "/finance/coa"),
            ("Company", "/settings/company"),
            // Header-only company profile (Tenant Admin) â€” not in sidebar; maps to company.* claims.
            ("My Company Profile", "/company-profile"),
            ("Roles", "/settings/roles"),
            ("Actions", "/settings/actions"),
            ("Forms", "/settings/forms"),
            ("Users", "/settings/users"),
            ("Permission Audit", "/settings/permission-audit"),
            ("System Settings", "/settings/system"),
        ];

        foreach (var (name, path) in forms)
        {
            if (await _db.Forms.AnyAsync(f => f.Path == path, cancellationToken))
            {
                continue;
            }

            _db.Forms.Add(new AppForm
            {
                Name = name,
                Path = path,
                IsActive = true,
                InsertDate = utcNow,
                InsertUserId = actorId,
            });
        }

        // Retired extra screens â€” soft-deactivate catalog rows if previously seeded.
        string[] retiredPaths =
        [
            "/settings/form-mapping",
            "/settings/user-company",
        ];
        foreach (var path in retiredPaths)
        {
            var retired = await _db.Forms
                .FirstOrDefaultAsync(f => f.Path == path, cancellationToken);
            if (retired is { IsActive: true })
            {
                retired.IsActive = false;
                retired.UpdateDate = utcNow;
                retired.UpdateUserId = actorId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded Forms catalog ({Count} navigation routes)", forms.Length);
    }

    /// <summary>
    /// Super Admin membership is globally isolated â€” CompanyId MUST be NULL.
    /// </summary>
    private async Task EnsureSuperAdminPlatformMembershipAsync(
        int superAdminUserId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var role = await _db.Roles.SingleAsync(
            r => r.RoleCode == SeedConstants.RoleSuperAdmin && r.CompanyId == null,
            cancellationToken);

        var legacy = await _db.UserTenantMemberships
            .Where(m => m.UserId == superAdminUserId && m.CompanyId != null)
            .ToListAsync(cancellationToken);
        if (legacy.Count > 0)
        {
            _db.UserTenantMemberships.RemoveRange(legacy);
            _logger.LogWarning(
                "Removed {Count} legacy Super Admin tenant membership(s)",
                legacy.Count);
        }

        var platformMembership = await _db.UserTenantMemberships
            .FirstOrDefaultAsync(
                m => m.UserId == superAdminUserId && m.CompanyId == null,
                cancellationToken);

        if (platformMembership is null)
        {
            _db.UserTenantMemberships.Add(new UserTenantMembership
            {
                UserId = superAdminUserId,
                CompanyId = null,
                RoleId = role.Id,
                IsActive = true,
                InsertDate = utcNow,
                InsertUserId = superAdminUserId,
            });
            _logger.LogInformation("Seeded Super Admin platform membership (CompanyId=NULL)");
        }
        else
        {
            platformMembership.RoleId = role.Id;
            if (platformMembership.InsertUserId == 0)
            {
                platformMembership.InsertUserId = superAdminUserId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTenantAdminIdentityAsync(
        int masterTenantId,
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var role = await _db.Roles.SingleAsync(
            r => r.RoleCode == SeedConstants.RoleTenantAdmin && r.CompanyId == null,
            cancellationToken);

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserName == SeedConstants.TenantAdminUserName, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                UserName = SeedConstants.TenantAdminUserName,
                Email = SeedConstants.TenantAdminEmail,
                // T0.12 TEMP: store plaintext â€” restore BCrypt before production / Module 0 Done gate.
                PasswordHash = SeedConstants.TenantAdminPlaceholderPassword,
                UserNn = SeedConstants.TenantAdminUserNn,
                IsActive = true,
                IsSuperAdmin = false,
                InsertDate = utcNow,
                InsertUserId = actorId,
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded Tenant Admin '{User}'", SeedConstants.TenantAdminUserName);
        }
        else
        {
            // T0.12 TEMP: force plaintext password for local debugging (replace before M0 gate).
            user.PasswordHash = SeedConstants.TenantAdminPlaceholderPassword;

            if (string.IsNullOrWhiteSpace(user.UserNn))
            {
                user.UserNn = SeedConstants.TenantAdminUserNn;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        var membership = await _db.UserTenantMemberships
            .FirstOrDefaultAsync(
                m => m.UserId == user.Id && m.CompanyId == masterTenantId,
                cancellationToken);

        if (membership is null)
        {
            _db.UserTenantMemberships.Add(new UserTenantMembership
            {
                UserId = user.Id,
                CompanyId = masterTenantId,
                RoleId = role.Id,
                IsActive = true,
                InsertDate = utcNow,
                InsertUserId = actorId,
            });
            _logger.LogInformation(
                "Seeded Tenant Admin membership â†’ company {CompanyId}",
                masterTenantId);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Backfill UserRoles from UserTenantMemberships (T1.38) â€” one row per membership RoleId.
    /// </summary>
    private async Task EnsureUserRolesBackfillAsync(
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var memberships = await _db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => new { m.UserId, m.RoleId })
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return;
        }

        var existing = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.IsActive)
            .Select(ur => new { ur.UserId, ur.RoleId })
            .ToListAsync(cancellationToken);
        var existingSet = existing
            .Select(e => (e.UserId, e.RoleId))
            .ToHashSet();

        var added = 0;
        foreach (var m in memberships)
        {
            if (existingSet.Contains((m.UserId, m.RoleId)))
            {
                continue;
            }

            // Reactivate soft-deleted pair if present.
            var inactive = await _db.UserRoles
                .FirstOrDefaultAsync(
                    ur => ur.UserId == m.UserId && ur.RoleId == m.RoleId && !ur.IsActive,
                    cancellationToken);
            if (inactive is not null)
            {
                inactive.IsActive = true;
                inactive.UpdateDate = utcNow;
                inactive.UpdateUserId = actorId;
                existingSet.Add((m.UserId, m.RoleId));
                added++;
                continue;
            }

            _db.UserRoles.Add(new UserRole
            {
                UserId = m.UserId,
                RoleId = m.RoleId,
                IsActive = true,
                InsertDate = utcNow,
                InsertUserId = actorId,
            });
            existingSet.Add((m.UserId, m.RoleId));
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Backfilled {Count} UserRole row(s) from memberships", added);
        }
    }

    private async Task EnsureCoaAutomationNodesAsync(
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await MigrateLegacyLiabilityMasterCodeAsync(actorId, utcNow, cancellationToken);

        await EnsureMasterAccountAsync(
            SeedConstants.MasterAssetsCode,
            "Assets",
            "AST",
            actorId,
            utcNow,
            cancellationToken);
        await EnsureMasterAccountAsync(
            SeedConstants.MasterEquityCode,
            "Equity",
            "EQY",
            actorId,
            utcNow,
            cancellationToken);
        await EnsureMasterAccountAsync(
            SeedConstants.MasterLiabilitiesCode,
            "Liabilities",
            "LIA",
            actorId,
            utcNow,
            cancellationToken);
        await EnsureMasterAccountAsync(
            SeedConstants.MasterIncomeCode,
            "Income",
            "INC",
            actorId,
            utcNow,
            cancellationToken);
        await EnsureMasterAccountAsync(
            SeedConstants.MasterExpenseCode,
            "Expense",
            "EXP",
            actorId,
            utcNow,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        await MigratePlatformControlsToTenantsAsync(actorId, utcNow, cancellationToken);

        // Skip per-tenant baseline when INV already exists (T1.79 cloned controls for all tenants).
        var companyIdsWithInv = await _db.ControlAccounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.ControlKind == ControlAccountKinds.Inventory && c.IsActive)
            .Select(c => c.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var invSet = companyIdsWithInv.ToHashSet();

        var activeCompanyIds = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var companyIdsNeedingBaseline = activeCompanyIds
            .Where(id => !invSet.Contains(id))
            .ToList();

        foreach (var companyId in companyIdsNeedingBaseline)
        {
            await TenantCoaBootstrap.EnsureCompanyBaselineAsync(
                _db,
                companyId,
                actorId,
                utcNow,
                _logger,
                cancellationToken);
        }

        if (companyIdsNeedingBaseline.Count > 0)
        {
            _logger.LogInformation(
                "Tenant COA baseline ensured for {Count} companies missing INV control",
                companyIdsNeedingBaseline.Count);
        }

        await RewriteLegacySubsidiaryAccountNosAsync(actorId, utcNow, cancellationToken);
        await RewriteBaselineSubsidiaryCodesAsync(actorId, utcNow, cancellationToken);
    }

    /// <summary>
    /// Clones legacy platform controls (CompanyId=0) onto each tenant, remaps subsidiaries, deletes orphans.
    /// </summary>
    private async Task MigratePlatformControlsToTenantsAsync(
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var platformControls = await _db.ControlAccounts
            .IgnoreQueryFilters()
            .Where(c => c.CompanyId == 0)
            .ToListAsync(cancellationToken);

        if (platformControls.Count == 0)
        {
            return;
        }

        var companies = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var companyId in companies)
        {
            foreach (var plat in platformControls)
            {
                var clone = await _db.ControlAccounts.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(
                        c => c.CompanyId == companyId && c.Code == plat.Code,
                        cancellationToken);
                if (clone is null)
                {
                    clone = new ControlAccount
                    {
                        CompanyId = companyId,
                        MasterAccountId = plat.MasterAccountId,
                        Code = plat.Code,
                        Title = plat.Title,
                        ControlKind = plat.ControlKind,
                        IsActive = plat.IsActive,
                        InsertDate = utcNow,
                        InsertUserId = actorId,
                    };
                    _db.ControlAccounts.Add(clone);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                var subs = await _db.SubsidiaryAccounts.IgnoreQueryFilters()
                    .Where(s => s.CompanyId == companyId && s.ControlAccountId == plat.Id)
                    .ToListAsync(cancellationToken);
                foreach (var sub in subs)
                {
                    sub.ControlAccountId = clone.Id;
                    sub.UpdateDate = utcNow;
                    sub.UpdateUserId = actorId;
                }

                if (subs.Count > 0)
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }

        // Orphan subsidiaries still pointing at platform ids â†’ best-effort remap by code.
        var leftoverSubs = await _db.SubsidiaryAccounts.IgnoreQueryFilters()
            .Where(s => platformControls.Select(p => p.Id).Contains(s.ControlAccountId))
            .ToListAsync(cancellationToken);
        foreach (var sub in leftoverSubs)
        {
            var plat = platformControls.FirstOrDefault(p => p.Id == sub.ControlAccountId);
            if (plat is null)
            {
                continue;
            }

            var target = await _db.ControlAccounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    c => c.CompanyId == sub.CompanyId && c.Code == plat.Code,
                    cancellationToken);
            if (target is null)
            {
                continue;
            }

            sub.ControlAccountId = target.Id;
            sub.UpdateDate = utcNow;
            sub.UpdateUserId = actorId;
        }

        if (leftoverSubs.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        _db.ControlAccounts.RemoveRange(platformControls);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Migrated {Count} platform ControlAccounts onto tenant scopes",
            platformControls.Count);
    }

    /// <summary>
    /// Ensures per-company Cash / Bank subsidiaries under tenant Cash/Bank controls.
    /// </summary>
    private async Task EnsureDemoSubsidiaryAccountsAsync(
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        // AccountNo / baseline code rewrites already ran in EnsureCoaAutomationNodesAsync.
        await Task.CompletedTask;
        _ = actorId;
        _ = utcNow;
        _ = cancellationToken;
    }

    /// <summary>
    /// Pre-T1.61 seed used Code 2000 for Liabilities; Equity now owns 2000 — remap LIA row to 3000.
    /// </summary>
    private async Task MigrateLegacyLiabilityMasterCodeAsync(
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        const string legacyLiabilityCode = "2000";
        if (string.Equals(SeedConstants.MasterLiabilitiesCode, legacyLiabilityCode, StringComparison.Ordinal))
        {
            return;
        }

        var legacy = await _db.MasterAccounts
            .FirstOrDefaultAsync(m => m.Code == legacyLiabilityCode, cancellationToken);
        if (legacy is null)
        {
            return;
        }

        var isLiabilityRow =
            string.Equals(legacy.Prefix, "LIA", StringComparison.OrdinalIgnoreCase)
            || legacy.Title.Contains("Liabilit", StringComparison.OrdinalIgnoreCase);
        if (!isLiabilityRow)
        {
            return;
        }

        var targetExists = await _db.MasterAccounts
            .AnyAsync(m => m.Code == SeedConstants.MasterLiabilitiesCode, cancellationToken);
        if (targetExists)
        {
            return;
        }

        legacy.Code = SeedConstants.MasterLiabilitiesCode;
        legacy.Title = "Liabilities";
        legacy.Prefix = "LIA";
        legacy.UpdateDate = utcNow;
        legacy.UpdateUserId = actorId;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Migrated legacy MasterAccount Code {Legacy} â†’ {Target} (Liabilities)",
            legacyLiabilityCode,
            SeedConstants.MasterLiabilitiesCode);
    }

    private async Task<MasterAccount> EnsureMasterAccountAsync(
        string code,
        string title,
        string prefix,
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var row = await _db.MasterAccounts
            .FirstOrDefaultAsync(m => m.Code == code, cancellationToken);
        if (row is null)
        {
            row = new MasterAccount
            {
                Code = code,
                Title = title,
                Prefix = prefix,
                IsActive = true,
                InsertDate = utcNow,
                InsertUserId = actorId,
            };
            _db.MasterAccounts.Add(row);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded MasterAccount {Code} {Title}", code, title);
            return row;
        }

        var dirty = false;
        if (row.InsertUserId == 0)
        {
            row.InsertUserId = actorId;
            dirty = true;
        }

        if (!string.Equals(row.Title, title, StringComparison.Ordinal)
            && (row.Title.StartsWith("Current ", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(row.Title)))
        {
            row.Title = title;
            dirty = true;
        }

        if (!string.Equals(row.Prefix, prefix, StringComparison.OrdinalIgnoreCase))
        {
            row.Prefix = prefix;
            dirty = true;
        }

        if (dirty)
        {
            row.UpdateDate = utcNow;
            row.UpdateUserId = actorId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return row;
    }


    /// <summary>
    /// Rewrites shared OTH-CASH / OTH-BANK codes into {TenantCode}-CASH / {TenantCode}-BANK.
    /// </summary>
    private async Task RewriteBaselineSubsidiaryCodesAsync(
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var rows = await _db.SubsidiaryAccounts
            .IgnoreQueryFilters()
            .Include(s => s.Company)
            .Where(s =>
                s.Company != null
                && (s.SubsidiaryAccountCode == "OTH-CASH"
                    || s.SubsidiaryAccountCode == "OTH-BANK"))
            .ToListAsync(cancellationToken);

        var rewritten = 0;
        foreach (var row in rows)
        {
            var suffix = row.SubsidiaryAccountCode.EndsWith("CASH", StringComparison.OrdinalIgnoreCase)
                ? SubsidiaryAccountCoding.BaselineCash
                : SubsidiaryAccountCoding.BaselineBank;
            var next = SubsidiaryAccountCoding.FormatBaselineCode(row.Company!.TenantCode, suffix);
            if (string.Equals(row.SubsidiaryAccountCode, next, StringComparison.Ordinal))
            {
                continue;
            }

            var clash = await _db.SubsidiaryAccounts
                .IgnoreQueryFilters()
                .AnyAsync(
                    s => s.CompanyId == row.CompanyId
                        && s.Id != row.Id
                        && s.SubsidiaryAccountCode == next,
                    cancellationToken);
            if (clash)
            {
                continue;
            }

            row.SubsidiaryAccountCode = next;
            row.UpdateDate = utcNow;
            row.UpdateUserId = actorId;
            rewritten++;
        }

        if (rewritten > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Rewrote {Count} baseline SubsidiaryAccountCode values to TenantCode-CASH/BANK",
                rewritten);
        }
    }

    /// <summary>
    /// Rewrites legacy / duplicate AccountNo values into globally unique BRD ACC-{yyyy}-{seq5}.
    /// </summary>
    private async Task RewriteLegacySubsidiaryAccountNosAsync(
        int actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var yearNow = AuditClock.Now.Year;
        var rows = await _db.SubsidiaryAccounts
            .IgnoreQueryFilters()
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var needsRenumber = false;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.AccountNo)
                || SubsidiaryAccountCoding.AccountNoNeedsRewrite(row.AccountNo)
                || !seen.Add(row.AccountNo.Trim()))
            {
                needsRenumber = true;
                break;
            }
        }

        if (!needsRenumber)
        {
            return;
        }

        long seq = 0;
        foreach (var row in rows)
        {
            seq++;
            var year = yearNow;
            if (SubsidiaryAccountCoding.TryParseAccountNoYear(row.AccountNo, out var parsedYear))
            {
                year = parsedYear;
            }

            var next = SubsidiaryAccountCoding.FormatAccountNo(year, seq);
            if (!string.Equals(row.AccountNo.Trim(), next, StringComparison.Ordinal))
            {
                row.AccountNo = next;
                row.UpdateDate = utcNow;
                row.UpdateUserId = actorId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var sequenceKey = SubsidiaryAccountNoAllocator.SequenceKey(yearNow);
        var platformSeq = await _db.PlatformSequences
            .FirstOrDefaultAsync(s => s.SequenceKey == sequenceKey, cancellationToken);
        if (platformSeq is null)
        {
            _db.PlatformSequences.Add(new PlatformSequence
            {
                SequenceKey = sequenceKey,
                LastValue = seq,
            });
        }
        else if (platformSeq.LastValue < seq)
        {
            platformSeq.LastValue = seq;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Renumbered {Count} Subsidiary AccountNo values to globally unique ACC-yyyy-seq",
            rows.Count);
    }

    private async Task EnsureProductCodeSequenceAsync(
        int companyId,
        CancellationToken cancellationToken)
    {
        if (await _db.ProductCodeSequences
                .IgnoreQueryFilters()
                .AnyAsync(s => s.CompanyId == companyId, cancellationToken))
        {
            return;
        }

        _db.ProductCodeSequences.Add(new ProductCodeSequence
        {
            CompanyId = companyId,
            LastValue = 0,
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded product_code sequence for company {CompanyId}", companyId);
    }

    private async Task EnsureTenantCodeSequenceAsync(CancellationToken cancellationToken)
    {
        if (await _db.PlatformSequences.AnyAsync(
                s => s.SequenceKey == TenantCodeGenerator.SequenceKey,
                cancellationToken))
        {
            return;
        }

        var bootstrap = await _db.Tenants.AsNoTracking().CountAsync(cancellationToken);
        _db.PlatformSequences.Add(new PlatformSequence
        {
            SequenceKey = TenantCodeGenerator.SequenceKey,
            LastValue = bootstrap,
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Seeded platform sequence {Key} at {Value}",
            TenantCodeGenerator.SequenceKey,
            bootstrap);
    }

    /// <summary>
    /// Seeds platform Units (Pcs/Kg/Ltr/Box/Pack) — idempotent by Code.
    /// </summary>
    private async Task EnsureUnitsAsync(CancellationToken cancellationToken)
    {
        (string Code, string Name)[] units =
        [
            ("Pcs", "Pieces"),
            ("Kg", "Kilogram"),
            ("Ltr", "Litre"),
            ("Box", "Box"),
            ("Pack", "Pack"),
        ];

        foreach (var (code, name) in units)
        {
            if (await _db.Units.AnyAsync(u => u.Code == code, cancellationToken))
            {
                continue;
            }

            _db.Units.Add(new Unit
            {
                Code = code,
                Name = name,
                IsActive = true,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded Units catalog ({Count} system UoM)", units.Length);
    }
}
