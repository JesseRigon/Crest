using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Media.Fields;
using Crest.Media.Services;
using Crest.Media.ViewModels;
using Crest.Mvc.ModelBinding;

namespace Crest.Media.Settings;

public sealed class MediaFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<MediaField>
{
    private readonly IContentTypeProvider _contentTypeProvider;
    private readonly MediaOptions _mediaOptions;

    internal readonly IStringLocalizer S;

    public MediaFieldSettingsDriver(
        IContentTypeProvider contentTypeProvider,
        IOptions<MediaOptions> mediaOptions,
        IStringLocalizer<MediaFieldSettingsDriver> stringLocalizer)
    {
        _contentTypeProvider = contentTypeProvider;
        _mediaOptions = mediaOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<MediaFieldSettingsViewModel>("MediaFieldSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<MediaFieldSettings>();

            model.Hint = settings.Hint;
            model.Required = settings.Required;
            model.Multiple = settings.Multiple;
            model.AllowMediaText = settings.AllowMediaText;
            model.AllowAnchors = settings.AllowAnchors;
            model.AllowAllDefaultMediaTypes = settings.AllowedExtensions == null || settings.AllowedExtensions.Length == 0;

            var items = new List<MediaTypeViewModel>();
            AddMediaTypes(_mediaOptions.AllowedFileExtensions, settings, items);
            AddMediaTypes(_mediaOptions.RestrictedFileExtensions, settings, items);

            model.MediaTypes = items
                .OrderBy(vm => vm.ContentType)
                .ToArray();
        }).Location("Content");
    }

    private void AddMediaTypes(
        IEnumerable<string> extensions,
        MediaFieldSettings settings,
        List<MediaTypeViewModel> items)
    {
        foreach (var extension in extensions)
        {
            if (_contentTypeProvider.TryGetContentType(extension, out var contentType))
            {
                var item = new MediaTypeViewModel()
                {
                    Extension = extension,
                    ContentType = contentType,
                    IsSelected = settings.AllowedExtensions != null && settings.AllowedExtensions.Contains(extension),
                };

                var index = contentType.IndexOf('/');

                if (index > -1)
                {
                    item.Type = contentType[..index];
                }

                items.Add(item);
            }
        }
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        var model = new MediaFieldSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        var settings = new MediaFieldSettings()
        {
            Hint = model.Hint,
            Required = model.Required,
            Multiple = model.Multiple,
            AllowMediaText = model.AllowMediaText,
            AllowAnchors = model.AllowAnchors,
        };

        if (!model.AllowAllDefaultMediaTypes)
        {
            var selectedExtensions = model.MediaTypes.Where(vm =>
                    vm.IsSelected &&
                    _mediaOptions.IsFileExtensionAllowed(vm.Extension, hasAdditionalPermission: true))
                .Select(x => x.Extension)
                .ToArray();

            if (selectedExtensions.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, string.Empty, S["Please select at least one extension."]);
            }

            settings.AllowedExtensions = selectedExtensions;
        }

        if (context.Updater.ModelState.IsValid)
        {
            context.Builder.WithSettings(settings);
        }

        return Edit(partFieldDefinition, context);
    }
}
