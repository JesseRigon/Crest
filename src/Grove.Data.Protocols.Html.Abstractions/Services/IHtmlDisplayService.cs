using Grove.Data.Protocols.Html.ViewModels;
using Shortcodes;

namespace Grove.Data.Protocols.Html.Services;

public interface IHtmlDisplayService
{
    Task UpdateModelHtmlAsync<TModel>(
        TModel model,
        bool renderLiquid,
        Context shortcodeContext,
        bool sanitizeHtml)
        where TModel : HtmlViewModelBase;
}
