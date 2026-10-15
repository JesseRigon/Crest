using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management;

public interface IStereotypeService
{
    Task<IEnumerable<StereotypeDescription>> GetStereotypesAsync();
}
