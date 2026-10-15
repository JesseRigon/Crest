#nullable enable
using YesSql;
using YesSql.Indexes;

namespace Grove.Media.Files;

/// <summary>One administrator access to a personal drive, with what happened to its notice and
/// which of its steps the audit trail already holds.</summary>
public sealed class PersonalDriveAccessDocument
{
    public long Id { get; set; }

    public PersonalDriveAccess Access { get; set; } = null!;

    /// <summary>How the notice went: the methods it was sent by, or why it failed (requirement 25).</summary>
    public string? Notice { get; set; }

    public bool OpenRecorded { get; set; }

    public bool EndRecorded { get; set; }
}

public sealed class PersonalDriveAccessIndex : MapIndex
{
    public string AccessId { get; set; } = string.Empty;

    public string DriveId { get; set; } = string.Empty;

    public string AdministratorUserId { get; set; } = string.Empty;

    public bool IsClosed { get; set; }
}

public sealed class PersonalDriveAccessIndexProvider : IndexProvider<PersonalDriveAccessDocument>
{
    public override void Describe(DescribeContext<PersonalDriveAccessDocument> context) =>
        context.For<PersonalDriveAccessIndex>()
            .Map(document => new PersonalDriveAccessIndex
            {
                AccessId = document.Access.Id,
                DriveId = document.Access.DriveId,
                AdministratorUserId = document.Access.AdministratorUserId,
                IsClosed = document.Access.ClosedUtc is not null,
            });
}

public interface IPersonalDriveAccessStore
{
    Task<PersonalDriveAccessDocument?> GetAsync(string accessId);

    /// <summary>The administrator's accesses to the drive not closed by hand (some may have ended).</summary>
    Task<IReadOnlyList<PersonalDriveAccessDocument>> ListOpenAsync(string driveId, string administratorUserId);

    Task SaveAsync(PersonalDriveAccessDocument document);
}

public sealed class YesSqlPersonalDriveAccessStore(ISession session) : IPersonalDriveAccessStore
{
    public async Task<PersonalDriveAccessDocument?> GetAsync(string accessId) =>
        await session.Query<PersonalDriveAccessDocument, PersonalDriveAccessIndex>(index => index.AccessId == accessId).FirstOrDefaultAsync();

    public async Task<IReadOnlyList<PersonalDriveAccessDocument>> ListOpenAsync(string driveId, string administratorUserId) =>
        [.. await session.Query<PersonalDriveAccessDocument, PersonalDriveAccessIndex>(index => index.DriveId == driveId && index.AdministratorUserId == administratorUserId && !index.IsClosed).ListAsync()];

    public Task SaveAsync(PersonalDriveAccessDocument document) => session.SaveAsync(document);
}
