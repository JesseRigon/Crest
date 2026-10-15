using System.Runtime.CompilerServices;
using Grove.ContentManagement;
using Grove.ContentManagement.Records;
using Grove.Entities;
using Grove.Media.Files;
using Grove.Members.Constants;
using Grove.Members.Models;
using Grove.Parties.Constants;
using Grove.Users.Indexes;
using Grove.Users.Models;
using YesSql;
using DriveType = Grove.Media.Files.DriveType;

namespace Grove.Members.Services;

/// <summary>
/// Who gets an organization or personal drive automatically (docs/media.md § 3, requirements
/// 12 and 18): every published organization, and every staff user (members never have a
/// personal drive). Media knows neither user classes nor organizations; this is where they are.
/// </summary>
public sealed class MemberDriveOwnerSource(ISession session) : IMediaDriveOwnerSource
{
    public async Task<bool?> IsStaffAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await session.Query<User, UserIndex>(index => index.UserId == userId).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        return UserClassSet.Has(user, UserClasses.Staff);
    }

    public async IAsyncEnumerable<MediaDriveOwner> GetOwnersAsync(DriveType type, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (type == DriveType.Organization)
        {
            var organizations = await session.Query<ContentItem, ContentItemIndex>(index =>
                    index.ContentType == PartiesConstants.ContentTypes.Organization && index.Published)
                .ListAsync(cancellationToken);
            foreach (var organization in organizations)
            {
                yield return new MediaDriveOwner(organization.ContentItemId, organization.DisplayText ?? organization.ContentItemId);
            }
        }
        else if (type == DriveType.Personal)
        {
            foreach (var user in await session.Query<User, UserIndex>().ListAsync(cancellationToken))
            {
                if (UserClassSet.Has(user, UserClasses.Staff))
                {
                    yield return new MediaDriveOwner(user.UserId, user.UserName);
                }
            }
        }
    }
}
