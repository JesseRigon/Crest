using Crest.Workflows.Studio.Models;

namespace Crest.Workflows.Studio.Contracts;

/// <summary>
/// Allows extensions to customize Monaco editor behavior, such as configuring intellisense.
/// </summary>
public interface IMonacoHandler
{
    /// <summary>
    /// Invoked when the Monaco editor is initialized.
    /// </summary>
    ValueTask InitializeAsync(MonacoContext context);
}