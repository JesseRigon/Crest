using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Crest.Json;

namespace Crest.Contents.Core.Services;

internal sealed class MvcOptionsConfiguration : IConfigureOptions<MvcOptions>
{
    private readonly DocumentJsonSerializerOptions _documentOptions;

    public MvcOptionsConfiguration(IOptions<DocumentJsonSerializerOptions> documentOptions)
    {
        _documentOptions = documentOptions.Value;
    }

    public void Configure(MvcOptions options)
    {
        options.OutputFormatters.Insert(0, new ContentSystemTextJsonOutputFormatter(_documentOptions.SerializerOptions));
    }
}
