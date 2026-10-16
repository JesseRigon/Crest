using Grove.Models;

namespace Grove.Services;

/// <summary>One option in an import: its key (the identity the import upserts by), the label it
/// gets when it is new, and the data the source owns.</summary>
public sealed record OptionListImportRow(
    string Key,
    string DisplayText,
    string? Category = null,
    string? Value = null,
    IReadOnlyDictionary<string, string?>? Fields = null,
    string? Parent = null);

public sealed record OptionListImportResult(int Read, int Added, int Updated, int Skipped, IReadOnlyList<string> Errors);

/// <summary>
/// Imports options into an existing option list, upserting by key (docs/architecture.md ›
/// Still to build, 6). A new key is added with the row's label at the end of the list; an
/// existing key gets the source's data (category, value, fields, parent) and keeps its label,
/// position and hidden flag: the label is the tenant's once the option exists, so a re-import
/// never overwrites a relabel. A row that fails is reported and the rest go on.
/// </summary>
public interface IOptionListImporter
{
    /// <param name="include">Optional row filter (a selective import); rows it rejects count as skipped.</param>
    Task<OptionListImportResult> ImportAsync(string listKey, IEnumerable<OptionListImportRow> rows, Func<OptionListImportRow, bool>? include = null, CancellationToken cancellationToken = default);
}

public sealed class OptionListImporter(IOptionListService lists) : IOptionListImporter
{
    /// <summary>At most this many row errors are kept in the result.</summary>
    public const int MaxErrors = 100;

    public async Task<OptionListImportResult> ImportAsync(string listKey, IEnumerable<OptionListImportRow> rows, Func<OptionListImportRow, bool>? include = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listKey);
        ArgumentNullException.ThrowIfNull(rows);

        var list = await lists.GetAsync(listKey, cancellationToken)
            ?? throw new InvalidOperationException($"The option list '{listKey}' does not exist.");
        var existing = list.Options.Select(option => option.Key).ToHashSet(StringComparer.Ordinal);
        var position = !list.Options.Any() ? 0 : list.Options.Max(option => option.Position) + 1;
        int read = 0, added = 0, updated = 0, skipped = 0;
        var errors = new List<string>();

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            read++;
            if (string.IsNullOrWhiteSpace(row.Key) || (include is not null && !include(row)))
            {
                skipped++;
                continue;
            }

            try
            {
                if (existing.Contains(row.Key))
                {
                    // displayText, position and hidden null: the tenant's own stay as they are.
                    await lists.UpdateOptionAsync(listKey, row.Key, displayText: null, position: null, hidden: null,
                        category: row.Category, value: row.Value, fields: row.Fields, parent: row.Parent, cancellationToken: cancellationToken);
                    updated++;
                }
                else
                {
                    await lists.AddOptionAsync(listKey, row.Key, string.IsNullOrWhiteSpace(row.DisplayText) ? row.Key : row.DisplayText, position++,
                        category: row.Category, value: row.Value, fields: row.Fields, parent: row.Parent, cancellationToken: cancellationToken);
                    existing.Add(row.Key);
                    added++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (errors.Count < MaxErrors)
                {
                    errors.Add($"{row.Key}: {exception.Message}");
                }
            }
        }

        return new OptionListImportResult(read, added, updated, skipped, errors);
    }
}
