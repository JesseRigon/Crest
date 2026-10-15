using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management;

public interface IStereotypesProvider
{
    Task<IEnumerable<StereotypeDescription>> GetStereotypesAsync();
}
