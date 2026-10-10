using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentManagement;

public interface IStereotypesProvider
{
    Task<IEnumerable<StereotypeDescription>> GetStereotypesAsync();
}
