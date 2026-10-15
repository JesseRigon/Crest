using Grove.Api;
using Grove.Shortcodes.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Grove.Shortcodes.Api;

/// <summary>
/// The shortcodes an editor can insert (the HTML and Markdown editors' shortcode tool): every
/// registered shortcode with its default value, hint and usage. For signed-in callers.
/// </summary>
public sealed class ShortcodesApi : ApiContributor
{
    public override string Name => "Shortcodes";

    public override string FeatureId => "Grove.Shortcodes";

    public override void Map(IEndpointRouteBuilder group) =>
        group.MapGroup("shortcodes").MapGet("", ApiHandlers.Handle<ShortcodesHandlers>(h => h.ListAsync()));
}

public sealed record ShortcodeEntry(string Name, string DefaultValue, string Hint, string Usage, string[] Categories);

public sealed class ShortcodesHandlers(IShortcodeDescriptorManager descriptors) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        return Ok((await descriptors.GetShortcodeDescriptors())
            .OrderBy(descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase)
            .Select(descriptor => new ShortcodeEntry(descriptor.Name, descriptor.DefaultValue, descriptor.Hint, descriptor.Usage, descriptor.Categories ?? []))
            .ToArray());
    }
}
