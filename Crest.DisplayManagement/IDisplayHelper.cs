using Microsoft.AspNetCore.Html;

namespace Crest.DisplayManagement;

public interface IDisplayHelper
{
    Task<IHtmlContent> ShapeExecuteAsync(IShape shape);
}
