using Crest.ContentManagement;

namespace Crest.Models;

/// <summary>
/// The organization a content item belongs to (ruling 2026-10-10: ownership on the row, as a
/// part with an index; assignments are grants). A type that carries this part is scoped by
/// organization on the member side: a member sees the rows of their organization.
/// </summary>
public class CrestOrganizationPart : ContentPart
{
    public string OrganizationId { get; set; } = string.Empty;
}
