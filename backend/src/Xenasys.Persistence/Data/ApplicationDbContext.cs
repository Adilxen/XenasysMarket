using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions;
using Xenasys.Core.Entities;
using Xenasys.Core.Entities.Common;
using Xenasys.Core.Security;
using Xenasys.Core.Time;

namespace Xenasys.Persistence.Data;

// ==========================================================================================
// WHAT: Primary EF Core DbContext for Xenasys multi-tenant persistence.
// WHY: Centralizes DbSets, fluent configs, tenant query filters, and audit/stamping on save.
// EXAMPLE: Injected into AuthRepository / PermissionGate; CurrentTenantId from JWT provider.
// ==========================================================================================
public sealed class ApplicationDbContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUser _currentUser;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantProvider tenantProvider,
        ICurrentUser currentUser)
        : base(options)
    {
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    public int CurrentTenantId => _tenantProvider.TenantId ?? 0;

    /// <summary>Super Admin bypasses tenant filters; unbound callers see no tenant rows.</summary>
    private bool BypassTenantFilter => _tenantProvider.IsSuperAdmin;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<ActionItem> Actions => Set<ActionItem>();

    public DbSet<AppForm> Forms => Set<AppForm>();

    public DbSet<FormAction> FormActions => Set<FormAction>();

    public DbSet<RoleFormAction> RoleFormActions => Set<RoleFormAction>();

    public DbSet<UserTenantMembership> UserTenantMemberships => Set<UserTenantMembership>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<MasterAccount> MasterAccounts => Set<MasterAccount>();

    public DbSet<ControlAccount> ControlAccounts => Set<ControlAccount>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<NotificationOutbox> NotificationOutbox => Set<NotificationOutbox>();

    public DbSet<Vendor> Vendors => Set<Vendor>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<SubCategory> SubCategories => Set<SubCategory>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();

    public DbSet<SubsidiaryAccount> SubsidiaryAccounts => Set<SubsidiaryAccount>();

    public DbSet<ProductCodeSequence> ProductCodeSequences => Set<ProductCodeSequence>();

    public DbSet<PlatformSequence> PlatformSequences => Set<PlatformSequence>();

    public DbSet<PlatformAuditLog> PlatformAuditLogs => Set<PlatformAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // ==========================================================================================
        // WHAT: Global query filters for all ITenantScoped entities (+ ProductCodeSequence).
        // WHY: BRD §2 — CompanyId always comes from ITenantProvider (JWT), never client payload.
        // EXAMPLE: Products query auto-adds WHERE CompanyId == CurrentTenantId (Super Admin bypass).
        // ==========================================================================================
        modelBuilder.Entity<Product>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);

        modelBuilder.Entity<StockMovement>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);

        modelBuilder.Entity<NotificationOutbox>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);

        modelBuilder.Entity<Vendor>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);

        modelBuilder.Entity<Category>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);

        modelBuilder.Entity<SubCategory>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);

        modelBuilder.Entity<Brand>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);

        modelBuilder.Entity<SalesOrder>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);

        modelBuilder.Entity<SalesOrderLine>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);

        modelBuilder.Entity<SubsidiaryAccount>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);

        modelBuilder.Entity<ControlAccount>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);

        modelBuilder.Entity<ProductCodeSequence>()
            .HasQueryFilter(e => BypassTenantFilter || e.CompanyId == CurrentTenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditMetadata();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditMetadata();
        return base.SaveChanges();
    }

    // ==========================================================================================
    // WHAT: Stamps Insert/Update audit fields and CompanyId on BaseAuditableEntity changes.
    // WHY: Keeps audit trails consistent without callers remembering to set metadata.
    // EXAMPLE: Added Product with CompanyId=0 → filled from tenant provider; UpdateDate set on edit.
    // ==========================================================================================
    private void ApplyAuditMetadata()
    {
        var now = AuditClock.Now;
        var userId = _currentUser.UserId ?? 0;

        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                // Preserve explicit seed/system stamps; only fill blanks.
                if (entry.Entity.InsertDate == default)
                {
                    entry.Entity.InsertDate = now;
                }

                if (entry.Entity.InsertUserId == 0 && userId != 0)
                {
                    entry.Entity.InsertUserId = userId;
                }

                if (entry.Entity is ITenantScoped tenantScoped
                    && tenantScoped.CompanyId == 0
                    && _tenantProvider.TenantId is int tid and > 0)
                {
                    tenantScoped.CompanyId = tid;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdateDate = now;
                if (userId != 0)
                {
                    entry.Entity.UpdateUserId = userId;
                }
            }
        }

        CapturePlatformAuditLogs(now, userId);
        GuardSystemRoleFormActionMaps();
    }

    // ==========================================================================================
    // WHAT: Appends PlatformAuditLogs rows for Super Admin mutations in this SaveChanges unit.
    // WHY: BRD §3.1 central ops trail (T1.58) — complements row-level Insert/Update stamps.
    // EXAMPLE: SA updates Tenant → Action=Update, EntityType=Tenant, Summary includes name.
    // ==========================================================================================
    private void CapturePlatformAuditLogs(DateTime now, int userId)
    {
        if (!_tenantProvider.IsSuperAdmin || userId <= 0)
        {
            return;
        }

        var actorName = string.IsNullOrWhiteSpace(_currentUser.UserName)
            ? SeedConstants.SuperAdminUserName
            : _currentUser.UserName.Trim();

        var logs = new List<PlatformAuditLog>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is PlatformAuditLog or PlatformSequence)
            {
                continue;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (!IsPlatformAuditedEntity(entry.Entity))
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Deleted => "Delete",
                _ => "Update",
            };

            var entityType = entry.Entity.GetType().Name;
            var entityId = TryGetIntId(entry.Entity);
            var companyId = TryGetCompanyId(entry.Entity);
            var summary = BuildAuditSummary(action, entityType, entityId, entry.Entity);

            logs.Add(new PlatformAuditLog
            {
                ActorUserId = userId,
                ActorUserName = actorName.Length > 50 ? actorName[..50] : actorName,
                Action = action,
                EntityType = entityType.Length > 64 ? entityType[..64] : entityType,
                EntityId = entityId,
                CompanyId = companyId,
                Summary = summary.Length > 500 ? summary[..500] : summary,
                PayloadJson = null,
                InsertDate = now,
            });
        }

        if (logs.Count == 0)
        {
            return;
        }

        PlatformAuditLogs.AddRange(logs);
    }

    private static bool IsPlatformAuditedEntity(object entity) =>
        entity is Tenant
            or User
            or Role
            or ActionItem
            or AppForm
            or FormAction
            or RoleFormAction
            or UserTenantMembership
            or UserRole;

    private static int? TryGetIntId(object entity)
    {
        var prop = entity.GetType().GetProperty("Id");
        if (prop?.PropertyType == typeof(int) && prop.GetValue(entity) is int id)
        {
            return id;
        }

        return null;
    }

    private static int? TryGetCompanyId(object entity) =>
        entity switch
        {
            ITenantScoped scoped when scoped.CompanyId > 0 => scoped.CompanyId,
            UserTenantMembership m => m.CompanyId,
            Role r => r.CompanyId,
            _ => null,
        };

    private static string BuildAuditSummary(string action, string entityType, int? entityId, object entity)
    {
        var idPart = entityId is int id && id > 0 ? $" #{id}" : string.Empty;
        var label = entity switch
        {
            Tenant t => $" {t.Name}",
            User u => $" {u.UserName}",
            Role r => $" {r.RoleCode}",
            ActionItem a => $" {a.Name}",
            AppForm f => $" {f.Path}",
            UserTenantMembership m => $" user={m.UserId} company={m.CompanyId}",
            UserRole ur => $" user={ur.UserId} role={ur.RoleId}",
            FormAction fa => $" form={fa.FormId} action={fa.ActionId}",
            RoleFormAction rfa => $" role={rfa.RoleId} form={rfa.FormId}",
            _ => string.Empty,
        };

        return $"{action} {entityType}{idPart}{label}".Trim();
    }

    /// <summary>
    /// System role ↔ Form×Action maps cannot be dropped/altered by non–Super Admin callers.
    /// Seed / unauthenticated startup path is allowed.
    /// </summary>
    private void GuardSystemRoleFormActionMaps()
    {
        var touch = ChangeTracker.Entries<RoleFormAction>()
            .Where(e => e.State is EntityState.Deleted or EntityState.Modified or EntityState.Added)
            .ToList();

        if (touch.Count == 0)
        {
            return;
        }

        var allow =
            !_currentUser.IsAuthenticated
            || string.Equals(
                _currentUser.Role,
                SeedConstants.RoleSuperAdmin,
                StringComparison.OrdinalIgnoreCase);

        if (allow)
        {
            return;
        }

        var roleIds = touch.Select(e => e.Entity.RoleId).Distinct().ToList();
        var locked = Roles.Local
            .Where(r => roleIds.Contains(r.Id) && SeedConstants.IsSystemRoleCode(r.RoleCode))
            .Select(r => r.Id)
            .ToHashSet();

        if (locked.Count == 0)
        {
            locked = Roles
                .AsNoTracking()
                .Where(r => roleIds.Contains(r.Id)
                    && (r.RoleCode == SeedConstants.RoleSuperAdmin
                        || r.RoleCode == SeedConstants.RoleTenantAdmin
                        || r.RoleCode == SeedConstants.RoleTenantStaff))
                .Select(r => r.Id)
                .ToHashSet();
        }

        if (touch.Any(e => locked.Contains(e.Entity.RoleId)))
        {
            throw new InvalidOperationException(
                "System role Form×Action maps are locked and cannot be modified by tenant users.");
        }
    }
}
