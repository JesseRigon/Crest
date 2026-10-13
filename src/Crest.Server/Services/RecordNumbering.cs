using System.Globalization;
using Crest.Data.Documents;
using Crest.Documents;

namespace Crest.Services;

/// <summary>
/// Gap-free record numbers: one counter per named sequence per tenant ("transactions:invoice",
/// "orders"), starting at 1. A number is gap-free when it is taken only at the moment the record
/// it numbers is committed (at posting, never at draft creation) and in the same unit of work,
/// so a cancelled request takes none: the counter lives in a tenant document that is written
/// with the record or not at all.
/// </summary>
public interface IRecordNumbering
{
    /// <summary>Takes the next number in <paramref name="sequence"/> (1 for a new one) and advances it.</summary>
    Task<long> NextAsync(string sequence);

    /// <summary>The number <see cref="NextAsync"/> would take now, without taking it.</summary>
    Task<long> PeekAsync(string sequence);

    /// <summary>Formats a taken number with its prefix, zero-padded: ("INV-", 42) → "INV-00042".</summary>
    static string Format(string prefix, long number, int digits = 5) =>
        prefix + number.ToString(new string('0', Math.Max(1, digits)), CultureInfo.InvariantCulture);
}

public sealed class RecordNumbering(IDocumentManager<RecordSequencesDocument> sequences) : IRecordNumbering
{
    public async Task<long> NextAsync(string sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sequence);

        var document = await sequences.GetOrCreateMutableAsync();
        document.Next.TryGetValue(sequence, out var next);
        next = next <= 0 ? 1 : next;
        document.Next[sequence] = next + 1;
        await sequences.UpdateAsync(document);
        return next;
    }

    public async Task<long> PeekAsync(string sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sequence);

        var document = await sequences.GetOrCreateImmutableAsync();
        return document.Next.TryGetValue(sequence, out var next) && next > 0 ? next : 1;
    }
}

/// <summary>Every sequence's next number, one document per tenant.</summary>
public sealed class RecordSequencesDocument : Document
{
    public Dictionary<string, long> Next { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
