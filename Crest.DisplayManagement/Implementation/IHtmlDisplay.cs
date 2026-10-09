using Microsoft.AspNetCore.Html;

namespace Crest.DisplayManagement.Implementation;

/// <summary>
/// Coordinates the rendering of shapes.
/// </summary>
public interface IHtmlDisplay
{
    Task<IHtmlContent> ExecuteAsync(DisplayContext context);
}
