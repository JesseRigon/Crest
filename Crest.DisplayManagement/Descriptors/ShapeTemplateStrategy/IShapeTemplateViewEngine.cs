using Microsoft.AspNetCore.Html;
using Crest.DisplayManagement.Implementation;

namespace Crest.DisplayManagement.Descriptors.ShapeTemplateStrategy;

public interface IShapeTemplateViewEngine
{
    IEnumerable<string> TemplateFileExtensions { get; }
    Task<IHtmlContent> RenderAsync(string relativePath, DisplayContext displayContext);
}
