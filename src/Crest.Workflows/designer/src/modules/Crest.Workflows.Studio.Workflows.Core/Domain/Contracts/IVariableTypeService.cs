using Crest.Workflows.Api.Client.Resources.VariableTypes.Models;

namespace Crest.Workflows.Studio.Workflows.Domain.Contracts;

/// <summary>
/// A services that provides variable types.
/// </summary>
public interface IVariableTypeService
{
    /// <summary>
    /// Gets the variable types.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IEnumerable<VariableTypeDescriptor>> GetVariableTypesAsync(CancellationToken cancellationToken = default);
}