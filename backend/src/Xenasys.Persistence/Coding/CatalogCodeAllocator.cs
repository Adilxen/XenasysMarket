using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Entities;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence.Coding;

// ==========================================================================================
// WHAT: Per-company CAT/SUB code allocator via PlatformSequences UPDLOCK.
// WHY: M2.7 — CAT_CODE_{companyId} / SUB_CODE_{companyId} → CAT01, SUB01 (2–4 digits).
// EXAMPLE: AllocateCategoryAsync(12) → UPDATE…OUTPUT → "CAT03".
// ==========================================================================================
public sealed class CatalogCodeAllocator : ICatalogCodeAllocator
{
    private readonly ApplicationDbContext _db;

    public CatalogCodeAllocator(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<string> AllocateCategoryAsync(
        int companyId,
        CancellationToken cancellationToken = default) =>
        AllocateAsync("CAT", $"CAT_CODE_{companyId}", cancellationToken);

    public Task<string> AllocateSubCategoryAsync(
        int companyId,
        CancellationToken cancellationToken = default) =>
        AllocateAsync("SUB", $"SUB_CODE_{companyId}", cancellationToken);

    private async Task<string> AllocateAsync(
        string prefix,
        string sequenceKey,
        CancellationToken cancellationToken)
    {
        await EnsureSequenceRowAsync(sequenceKey, cancellationToken);
        var next = await NextValueAsync(sequenceKey, cancellationToken);
        return FormatCode(prefix, next);
    }

    internal static string FormatCode(string prefix, long value)
    {
        if (value < 1)
        {
            value = 1;
        }

        var digits = value switch
        {
            < 100 => 2,
            < 1000 => 3,
            _ => 4,
        };

        if (value > 9999)
        {
            throw new InvalidOperationException(
                $"Catalog sequence for '{prefix}' exceeded 9999.");
        }

        return $"{prefix}{value.ToString().PadLeft(digits, '0')}";
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
