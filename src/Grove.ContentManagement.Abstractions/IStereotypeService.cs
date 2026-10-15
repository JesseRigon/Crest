using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentManagement;

public interface IStereotypeService
{
    Task<IEnumerable<StereotypeDescription>> GetStereotypesAsync();
}
