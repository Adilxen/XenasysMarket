using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Entities;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Coding;

// ==========================================================================================
// WHAT: Per-company ProductCodeSequences → CHAR(16) product_code.
// WHY: Unique SKU per tenant for inventory; COA subsidiary title uses Product Name only.
// EXAMPLE: Tenant ACM007, user 7, seq 42 → ACM0070700000042
// ==========================================================================================
public sealed class ProductCodeAllocator : IProductCodeAllocator
{
    private const int MaxAttempts = 8;

    private readonly ApplicationDbContext _db;

    public ProductCodeAllocator(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> AllocateAsync(
        int companyId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
        {
            throw new InvalidOperationException("Company context is required to allocate product codes.");
        }

        var tenantCode = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == companyId)
            .Select(t => t.TenantCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(tenantCode) || tenantCode.Trim().Length != 6)
        {
            throw new InvalidOperationException(
                $"Company {companyId} has no valid TenantCode — cannot allocate product code.");
        }

        await EnsureSequenceRowAsync(companyId, cancellationToken);
        var userNn = Math.Abs(userId % 100).ToString("D2");
        var prefix = tenantCode.Trim().ToUpperInvariant() + userNn;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var next = await NextValueAsync(companyId, cancellationToken);
            var code = prefix + next.ToString("D8");
            var taken = await _db.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(p => p.CompanyId == companyId && p.ProductCode == code, cancellationToken);

            if (!taken)
            {
                return code;
            }
        }

        throw new InvalidOperationException(
            "Unable to allocate unique product code after multiple sequence advances.");
    }

    private async Task EnsureSequenceRowAsync(int companyId, CancellationToken cancellationToken)
    {
        var exists = await _db.ProductCodeSequences
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(s => s.CompanyId == companyId, cancellationToken);

        if (exists)
        {
            return;
        }

        var maxExisting = await _db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .Select(p => p.ProductCode)
            .ToListAsync(cancellationToken);

        long bootstrap = 0;
        foreach (var code in maxExisting)
        {
            if (code.Length == 16 && long.TryParse(code.AsSpan(8, 8), out var seq) && seq > bootstrap)
            {
                bootstrap = seq;
            }
        }

        _db.ProductCodeSequences.Add(new ProductCodeSequence
        {
            CompanyId = companyId,
            LastValue = bootstrap,
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
        }
    }

    private async Task<long> NextValueAsync(int companyId, CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE [ProductCodeSequences] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            SET [LastValue] = [LastValue] + 1
            OUTPUT INSERTED.[LastValue] AS [LastValue]
            WHERE [CompanyId] = @companyId;
            """;

        var key = new SqlParameter("@companyId", companyId);
        var rows = await _db.Database
            .SqlQueryRaw<SequenceAdvanceResult>(sql, key)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                $"Product code sequence for company {companyId} is missing.");
        }

        return rows[0].LastValue;
    }

    private sealed class SequenceAdvanceResult
    {
        public long LastValue { get; set; }
    }
}
