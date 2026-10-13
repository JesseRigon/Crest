using Crest.ContentFields.Fields;
using Crest.ContentFields.Relationships;
using Crest.ContentManagement;
using Crest.Parties.Constants;

namespace Crest.Parties.Services;

/// <summary>
/// The rule of an Organization's parent picker (ruling 2026-10-05): the parent is an
/// Organization (also enforced by the picker's allowed types), never the organization
/// itself, and never one of its own descendants. Runs on every save path through the
/// content picker save guard.
/// </summary>
public sealed class ParentOrganizationRelationshipHandler(IContentManager contentManager) : IContentPickerRelationshipHandler
{
    // Deeper than any real group structure; a longer walk is treated as a cycle.
    private const int MaxDepth = 64;

    public async Task ValidateAsync(ContentPickerRelationshipContext context)
    {
        var self = context.Item.ContentItemId;
        foreach (var parentId in context.SelectedIds)
        {
            if (string.Equals(parentId, self, StringComparison.Ordinal))
            {
                context.Fail("An organization cannot be its own parent.");
                continue;
            }

            var current = parentId;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var depth = 0; current is not null; depth++)
            {
                if (depth >= MaxDepth || !seen.Add(current))
                {
                    context.Fail("The parent organization chain is too deep or already cyclic.");
                    break;
                }

                var organization = await contentManager.GetAsync(current, VersionOptions.Latest);
                if (organization is null)
                {
                    break;
                }

                if (organization.ContentType != PartiesConstants.ContentTypes.Organization)
                {
                    context.Fail("A parent organization must be an Organization.");
                    break;
                }

                var next = organization.Get<ContentPart>(PartiesConstants.ContentTypes.Organization)
                    ?.Get<ContentPickerField>(PartiesConstants.OrganizationFields.ParentOrganization)
                    ?.ContentItemIds.FirstOrDefault();
                if (string.Equals(next, self, StringComparison.Ordinal))
                {
                    context.Fail($"'{organization.DisplayText}' is already below this organization; it cannot be its parent.");
                    break;
                }

                current = next;
            }
        }
    }
}
