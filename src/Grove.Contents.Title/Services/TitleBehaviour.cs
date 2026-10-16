using Fluid;
using Fluid.Values;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Title.Models;
using Grove.Contents.Title.ViewModels;
using Grove.Display.Liquid;
using Microsoft.Extensions.Localization;

namespace Grove.Contents.Title.Services;

/// <summary>
/// What the title's system flows do (<see cref="Workflows.SetDisplayTextFromTitle"/>,
/// <see cref="Workflows.CheckTitleGiven"/>): the display text follows the title, or the title is
/// computed from the attachment's pattern; an attachment that requires a title refuses without one.
/// </summary>
public sealed class TitleBehaviour(
    ILiquidTemplateManager liquidTemplateManager,
    IContentDefinitionManager contentDefinitionManager,
    IStringLocalizer<TitleBehaviour> S)
{
    /// <summary>Sets the item's display text from its title, or computes the title from the attachment's pattern.</summary>
    public async Task SetDisplayTextAsync(ContentItem item)
    {
        if (await SettingsAsync(item) is not { } settings)
        {
            return;
        }

        // The flow runs only where the field set is attached: an item that does not hold it yet gets it.
        var part = item.GetOrCreate<TitleFieldSet>();

        // The user writes the title: the display text follows it when nothing else set one.
        if (settings.Options == TitleFieldSetOptions.Editable || settings.Options == TitleFieldSetOptions.EditableRequired)
        {
            if (string.IsNullOrWhiteSpace(item.DisplayText))
            {
                item.DisplayText = part.Title;
            }

            return;
        }

        if (!string.IsNullOrEmpty(settings.Pattern))
        {
            var model = new TitleFieldSetViewModel
            {
                Title = part.Title,
                TitleFieldSet = part,
                ContentItem = item,
            };

            var title = await liquidTemplateManager.RenderStringAsync(settings.Pattern, NullEncoder.Default, model,
                new Dictionary<string, FluidValue>
                {
                    ["ContentItem"] = new ObjectValue(model.ContentItem),
                });

            title = title.ReplaceLineEndings(string.Empty);
            item.Alter<TitleFieldSet>(titleFieldSet => titleFieldSet.Title = title);
            item.DisplayText = title;
        }
    }

    /// <summary>The refusal of an item whose attachment requires a title it lacks; null when it is fine.</summary>
    public async Task<string> RequiredErrorAsync(ContentItem item)
    {
        if (await SettingsAsync(item) is not { Options: TitleFieldSetOptions.EditableRequired })
        {
            return null;
        }

        return item.TryGet<TitleFieldSet>(out var part) && !string.IsNullOrEmpty(part.Title)
            ? null
            : S["A value is required for Title."].Value;
    }

    private async Task<TitleFieldSetSettings> SettingsAsync(ContentItem item)
    {
        var type = await contentDefinitionManager.GetTypeDefinitionAsync(item.ContentType);
        return type?.FieldSets
            .FirstOrDefault(fieldSet => string.Equals(fieldSet.FieldSetDefinition.Name, ContentFieldSetName.Of<TitleFieldSet>(), StringComparison.Ordinal))?
            .GetSettings<TitleFieldSetSettings>();
    }
}
