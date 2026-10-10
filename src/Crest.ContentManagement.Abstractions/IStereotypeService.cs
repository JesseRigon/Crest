using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentManagement;

public interface IStereotypeService
{
    Task<IEnumerable<StereotypeDescription>> GetStereotypesAsync();
}
