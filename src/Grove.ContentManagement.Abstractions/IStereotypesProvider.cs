using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentManagement;

public interface IStereotypesProvider
{
    Task<IEnumerable<StereotypeDescription>> GetStereotypesAsync();
}
