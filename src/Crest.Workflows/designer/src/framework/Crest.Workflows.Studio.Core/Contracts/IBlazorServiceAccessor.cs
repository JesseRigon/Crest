namespace Crest.Workflows.Studio.Contracts;

/// <summary>
/// Represents an interface for accessing the current IServiceProvider in Blazor.
/// </summary>
public interface IBlazorServiceAccessor
{
    /// Gets or sets the current IServiceProvider.
    IServiceProvider Services { get; set; }
}