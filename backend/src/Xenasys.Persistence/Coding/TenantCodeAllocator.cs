using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Entities;
using Xenasys.Core.Services;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Coding;

// ==========================================================================================
// WHAT: Allocates TenantCode via UPDLOCK sequence + UNIQUE index safety net.
// WHY: Fast O(1) next-value; no Tenants table scan; safe under concurrent Super Admin creates.
// EXAMPLE: AllocateAsync("Acme") → UPDATE…OUTPUT → "ACM008" (retry if UX collision).
// ==========================================================================================
public sealed class TenantCodeAllocator : ITenantCodeAllocator
{
    private const int MaxAttempts = 8;

    private readonly ApplicationDbContext _db;

    public TenantCodeAllocator(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> AllocateAsync(
        string companyName,
        CancellationToken cancellationToken = default)
    {
        await EnsureSequenceRowAsync(cancellationToken);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var next = await NextValueAsync(cancellationToken);
            var code = TenantCodeGenerator.Generate(companyName, next);

            var taken = await _db.Tenants
                .AsNoTracking()
                .AnyAsync(t => t.TenantCode == code, cancellationToken);

            if (!taken)
            {
                return code;
            }
        }

        throw new InvalidOperationException(
            "Unable to allocate a unique TenantCode after multiple sequence advances. " +
            "Sequence space may be exhausted (max 9999) or flooded with collisions.");
    }

    private async Task EnsureSequenceRowAsync(CancellationToken cancellationToken)
    {
        var exists = await _db.PlatformSequences
            .AsNoTracking()
            .AnyAsync(s => s.SequenceKey == TenantCodeGenerator.SequenceKey, cancellationToken);

        if (exists)
        {
            return;
        }

        // Bootstrap above any existing auto-style codes so we never collide on first mint.
        var bootstrap = await _db.Tenants.AsNoTracking().CountAsync(cancellationToken);
        _db.PlatformSequences.Add(new PlatformSequence
        {
            SequenceKey = TenantCodeGenerator.SequenceKey,
            LastValue = bootstrap,
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent first-insert — row now exists.
            _db.ChangeTracker.Clear();
        }
    }

    private async Task<long> NextValueAsync(CancellationToken cancellationToken)
    {
        // Single-row hot path: UPDLOCK serializes issuers without scanning Tenants.
        const string sql =
            """
            UPDATE [PlatformSequences] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            SET [LastValue] = [LastValue] + 1
            OUTPUT INSERTED.[LastValue] AS [LastValue]
            WHERE [SequenceKey] = @key;
            """;

        var key = new SqlParameter("@key", TenantCodeGenerator.SequenceKey);
        var rows = await _db.Database
            .SqlQueryRaw<SequenceAdvanceResult>(sql, key)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                $"Platform sequence '{TenantCodeGenerator.SequenceKey}' is missing.");
        }

        return rows[0].LastValue;
    }

    private sealed class SequenceAdvanceResult
    {
        public long LastValue { get; set; }
    }
}
