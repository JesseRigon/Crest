using Crest.Entities;

namespace Crest.AuditTrail.Services;

public class AuditTrailIdGenerator : IAuditTrailIdGenerator
{
    private readonly IIdGenerator _generator;

    public AuditTrailIdGenerator(IIdGenerator generator)
    {
        _generator = generator;
    }

    public string GenerateUniqueId() => _generator.GenerateUniqueId();
}
