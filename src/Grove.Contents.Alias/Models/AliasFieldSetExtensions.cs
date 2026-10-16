using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using Grove.Contents.Alias.Indexes;
using YesSql;

namespace Grove.Contents.Alias.Models;

public static class AliasFieldSetExtensions
{
    public static async IAsyncEnumerable<ValidationResult> ValidateAsync(this AliasFieldSet part, IStringLocalizer S, ISession session)
    {
        if (!string.IsNullOrWhiteSpace(part.Alias))
        {
            if (part.Alias.Length > AliasFieldSet.MaxAliasLength)
            {
                yield return new ValidationResult(S["Your alias is too long. The alias can only be up to {0} characters. \"{1}\"", AliasFieldSet.MaxAliasLength, part.Alias], new string[] { nameof(part.Alias) });
            }

            if (!await IsAliasUniqueAsync(part, session, part.Alias))
            {
                yield return new ValidationResult(S["Your alias is already in use. \"{0}\"", part.Alias], new[] { nameof(part.Alias) });
            }
        }
    }

    public static async Task<bool> IsAliasUniqueAsync(this AliasFieldSet context, ISession session, string alias)
    {
        return (await session.QueryIndex<AliasIndex>(o => o.Alias == alias && o.ContentItemId != context.ContentItem.ContentItemId).CountAsync()) == 0;
    }

}
