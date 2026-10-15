using Microsoft.AspNetCore.Html;
using Grove.DisplayManagement.Implementation;

namespace Grove.DisplayManagement.Descriptors.ShapeTemplateStrategy;

public interface IShapeTemplateViewEngine
{
    IEnumerable<string> TemplateFileExtensions { get; }
    Task<IHtmlContent> RenderAsync(string relativePath, DisplayContext displayContext);
}
