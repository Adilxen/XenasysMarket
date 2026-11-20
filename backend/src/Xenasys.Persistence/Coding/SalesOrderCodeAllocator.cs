using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Entities;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Coding;

// ==========================================================================================
// WHAT: Allocates SO + 6-digit codes via PlatformSequences UPDLOCK.
// WHY: T8.1 — unique OrderCode per company without race conditions.
// EXAMPLE: AllocateAsync(12) → "SO000007".
// ==========================================================================================
public sealed class SalesOrderCodeAllocator : ISalesOrderCodeAllocator
{
    private readonly ApplicationDbContext _db;

    public SalesOrderCodeAllocator(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> AllocateAsync(
        int companyId,
        CancellationToken cancellationToken = default)
    {
        var sequenceKey = $"SO_CODE_{companyId}";
        await EnsureSequenceRowAsync(sequenceKey, cancellationToken);
        var next = await NextValueAsync(sequenceKey, cancellationToken);
        if (next < 1)
        {
            next = 1;
        }

        if (next > 999999)
        {
            throw new InvalidOperationException(
                $"Sales order sequence for company {companyId} exceeded 999999.");
        }

        return $"SO{next.ToString().PadLeft(6, '0')}";
    }

    private async Task EnsureSequenceRowAsync(string sequenceKey, CancellationToken cancellationToken)
    {
        var exists = await _db.PlatformSequences
            .AsNoTracking()
            .AnyAsync(s => s.SequenceKey == sequenceKey, cancellationToken);

        if (exists)
        {
            return;
        }

        _db.PlatformSequences.Add(new PlatformSequence
        {
            SequenceKey = sequenceKey,
            LastValue = 0,
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

    private async Task<long> NextValueAsync(string sequenceKey, CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE [PlatformSequences] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            SET [LastValue] = [LastValue] + 1
            OUTPUT INSERTED.[LastValue] AS [LastValue]
            WHERE [SequenceKey] = @key;
            """;

        var key = new SqlParameter("@key", sequenceKey);
        var rows = await _db.Database
            .SqlQueryRaw<SequenceAdvanceResult>(sql, key)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                $"Platform sequence '{sequenceKey}' is missing.");
        }

        return rows[0].LastValue;
    }

    private sealed class SequenceAdvanceResult
    {
        public long LastValue { get; set; }
    }
}
