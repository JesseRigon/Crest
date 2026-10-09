using Microsoft.AspNetCore.Mvc.Filters;

namespace Crest.Forms.Filters;

public abstract class ModelStateTransferAttribute : ActionFilterAttribute
{
    internal const string Key = nameof(ModelStateTransferAttribute);
}
