using Grove.Access;
using Grove.Access.Services;
using Grove.Security.Permissions;

namespace Grove.Tests.Access.Conformance;

/// <summary>
/// The conformance suite's fixtures (docs/workflows.md › Tasks › 1, the conformance suite):
/// one small tenant - permissions with an implied-by chain, callers of every class and side,
/// resources that map, re-ask, are public or are ceilinged - and the verdict every pipeline
/// must give for each case. A pipeline that answers differently from the table fails.
/// </summary>
public static class AccessWorld
{
    public const string Tenant = "Default";

    // Manage → Edit → View: holding a stronger permission grants every weaker one. Declared
    // strongest first, so each implied-by list is built from initialized fields.
    public static readonly Permission ManageContent = new("ManageContent", "Manage content");
    public static readonly Permission EditContent = new("EditContent", "Edit content", [ManageContent]);
    public static readonly Permission ViewContent = new("ViewContent", "View content", [EditContent]);
    public static readonly Permission PublishContent = new("PublishContent", "Publish content");
    public static readonly Permission ManageUsers = new("ManageUsers", "Manage users");
    public static readonly Permission ImportRemote = new("ImportRemoteInstances", "Import from remote instances");

    public static readonly Permission[] All = [ViewContent, EditContent, ManageContent, PublishContent, ManageUsers, ImportRemote];

    /// <summary>A content item: its type decides the per-type variation, its owner the "own" one.</summary>
    public sealed record Item(string Id, string Type, string Owner, bool Locked = false)
    {
        public override string ToString() => $"item:{Id}";
    }

    /// <summary>A file attached to an item: decided by the item's own right (a re-ask).</summary>
    public sealed record Attachment(Item Parent)
    {
        public override string ToString() => $"attachment:{Parent.Id}";
    }

    /// <summary>A file outside every secured folder: public for viewing.</summary>
    public sealed record PublicFile(string Path)
    {
        public override string ToString() => $"file:{Path}";
    }

    /// <summary>A resource whose mapping re-asks itself forever: the depth limit must deny.</summary>
    public sealed record Loop(string Name)
    {
        public override string ToString() => $"loop:{Name}";
    }

    public static readonly Item HomePage = new("home", "Page", "u-editor");
    public static readonly Item DraftPost = new("draft", "Post", "u-author");
    public static readonly Item OtherPost = new("other", "Post", "u-editor");
    public static readonly Item LockedPage = new("locked", "Page", "u-editor", Locked: true);

    // The callers. Permissions are what the factory resolved from roles (the Anonymous role
    // for everyone, Authenticated for signed-in users); implied ones are the decision's.
    public static readonly CallerContext Anonymous = Caller(null, CallerSide.Site, permissions: ["ViewContent_Page"]);
    public static readonly CallerContext Editor = Caller("u-editor", CallerSide.Admin, roles: ["Editor"], permissions: ["ViewContent_Page", "EditContent"]);
    public static readonly CallerContext Manager = Caller("u-manager", CallerSide.Admin, roles: ["Manager"], permissions: ["ManageContent"]);
    public static readonly CallerContext Author = Caller("u-author", CallerSide.Admin, roles: ["Author"], permissions: ["ViewContent_Page", "EditOwnContent_Post"]);
    public static readonly CallerContext Admin = Caller("u-admin", CallerSide.Admin, roles: ["Administrator"], superUser: true);
    public static readonly CallerContext MemberAdmin = Caller("u-member", CallerSide.Member, roles: ["OrgAdmin"], superUser: true, userClass: CallerClasses.Member, organizationId: "org-1");
    public static readonly CallerContext Machine = Caller("app-1", CallerSide.Site, userClass: CallerClasses.Application, permissions: ["ImportRemoteInstances"]);
    public static readonly CallerContext SystemActor = new()
    {
        Tenant = Tenant,
        Side = CallerSide.System,
        IsSystem = true,
        IsSuperUser = true,
        UserName = "system",
    };

    public static CallerContext Caller(
        string userId,
        CallerSide side,
        string[] roles = null,
        string[] permissions = null,
        bool superUser = false,
        string userClass = null,
        string organizationId = null,
        long version = 1) => new()
        {
            Tenant = Tenant,
            Side = side,
            UserId = userId,
            UserName = userId,
            IsSuperUser = superUser,
            UserClass = userClass,
            OrganizationId = organizationId,
            Roles = new HashSet<string>(roles ?? [], StringComparer.OrdinalIgnoreCase),
            Permissions = new HashSet<string>(permissions ?? [], StringComparer.OrdinalIgnoreCase),
            PermissionVersion = version,
        };

    /// <summary>What a case must come out as.</summary>
    public enum Expect
    {
        Allow,
        Deny,
        /// <summary>A ceiling: denied, and no other grant may override it.</summary>
        DenyFinal,
    }

    public sealed record Case(string Name, CallerContext Caller, string Permission, object Resource, Expect Expected)
    {
        public override string ToString() => Name;
    }

    /// <summary>The verdict table. Add a row for every rule the machinery gains.</summary>
    public static IEnumerable<Case> Cases()
    {
        // Direct grants and the implied-by chain.
        yield return new("anonymous has no unqualified view", Anonymous, "ViewContent", null, Expect.Deny);
        yield return new("editor's edit implies view", Editor, "ViewContent", null, Expect.Allow);
        yield return new("manager's manage implies view through edit", Manager, "ViewContent", null, Expect.Allow);
        yield return new("editor holds no publish", Editor, "PublishContent", null, Expect.Deny);
        yield return new("an unknown permission is denied", Editor, "NoSuchPermission", null, Expect.Deny);

        // Resource mapping: per-type and owner variations; the base permission still counts.
        yield return new("anonymous views a page through the per-type right", Anonymous, "ViewContent", HomePage, Expect.Allow);
        yield return new("anonymous may not view a post", Anonymous, "ViewContent", DraftPost, Expect.Deny);
        yield return new("the author edits their own post", Author, "EditContent", DraftPost, Expect.Allow);
        yield return new("the author may not edit someone else's post", Author, "EditContent", OtherPost, Expect.Deny);
        yield return new("the editor's base edit covers any post", Editor, "EditContent", OtherPost, Expect.Allow);

        // Re-asks: an attachment is decided by its item.
        yield return new("an attachment of a public page is viewable", Anonymous, "ViewContent", new Attachment(HomePage), Expect.Allow);
        yield return new("an attachment of a post is not", Anonymous, "ViewContent", new Attachment(DraftPost), Expect.Deny);
        yield return new("a public file needs no right", Anonymous, "ViewContent", new PublicFile("/logo.png"), Expect.Allow);
        yield return new("a mapping that re-asks forever is cut off and denied", Editor, "ViewContent", new Loop("x"), Expect.Deny);

        // The super user, and the ceilings above it.
        yield return new("the super user may manage users", Admin, "ManageUsers", null, Expect.Allow);
        yield return new("the super user may do what no role names", Admin, "NoSuchPermission", null, Expect.Allow);
        yield return new("a member is ceilinged even as an organization super user", MemberAdmin, "ManageUsers", null, Expect.DenyFinal);
        yield return new("a locked item may not be edited, even by the super user", Admin, "EditContent", LockedPage, Expect.DenyFinal);
        yield return new("a ceiling met through a re-ask stays final", Admin, "EditContent", new Attachment(LockedPage), Expect.DenyFinal);
        yield return new("a locked item may still be viewed", Anonymous, "ViewContent", LockedPage, Expect.Allow);

        // The system actor and machines.
        yield return new("the system actor may manage content", SystemActor, "ManageContent", null, Expect.Allow);
        yield return new("an application holds exactly what it was granted", Machine, "ImportRemoteInstances", null, Expect.Allow);
        yield return new("an application holds nothing else", Machine, "ViewContent", null, Expect.Deny);
        yield return new("an application is not the super user", Machine, "ManageUsers", null, Expect.Deny);
    }

    public static TheoryData<Case> Theory()
    {
        var data = new TheoryData<Case>();
        foreach (var item in Cases())
        {
            data.Add(item);
        }

        return data;
    }

    public sealed class PermissionRegistry : IPermissionService
    {
        public ValueTask<Permission> FindByNameAsync(string name)
            => ValueTask.FromResult(All.FirstOrDefault(permission => string.Equals(permission.Name, name, StringComparison.OrdinalIgnoreCase)));

        public ValueTask<IEnumerable<Permission>> GetPermissionsAsync() => ValueTask.FromResult<IEnumerable<Permission>>(All);
    }

    /// <summary>The member class ceiling and the locked-item rule.</summary>
    public sealed class Ceilings : IAccessCeiling
    {
        public string Deny(CallerContext caller, string permission, object resource = null)
        {
            if (caller.UserClass == CallerClasses.Member && permission == ManageUsers.Name)
            {
                return "Members never manage users.";
            }

            if (resource is Item { Locked: true } && permission == EditContent.Name)
            {
                return "The item is locked.";
            }

            return null;
        }
    }

    /// <summary>One mapper per resource kind, as the modules register them.</summary>
    public sealed class ItemMapper : IResourcePermissionMapper
    {
        public ValueTask<IReadOnlyList<PermissionCandidate>> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
        {
            if (resource is not Item item)
            {
                return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>>(null);
            }

            var candidates = new List<PermissionCandidate> { permission, $"{permission}_{item.Type}" };
            if (caller.UserId is not null && caller.UserId == item.Owner)
            {
                candidates.Add($"{permission.Replace("Content", "OwnContent", StringComparison.Ordinal)}_{item.Type}");
            }

            return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>>(candidates);
        }
    }

    public sealed class FileMapper : IResourcePermissionMapper
    {
        public ValueTask<IReadOnlyList<PermissionCandidate>> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<PermissionCandidate>>(resource switch
            {
                Attachment attachment => [new PermissionCandidate(permission, attachment.Parent)],
                PublicFile => [PermissionCandidate.Granted],
                Loop loop => [new PermissionCandidate(permission, loop)],
                _ => null,
            });
    }

    /// <summary>Records every event, so a test can count them.</summary>
    public sealed class RecordingAuditor : IAccessAuditor
    {
        public List<AccessEvent> Events { get; } = [];

        public Task RecordAsync(AccessEvent accessEvent, CancellationToken cancellationToken = default)
        {
            lock (Events)
            {
                Events.Add(accessEvent);
            }

            return Task.CompletedTask;
        }
    }

    public static AccessDecisionService Decision(IAccessAuditor auditor = null)
        => new(new PermissionRegistry(), [new Ceilings()], [new ItemMapper(), new FileMapper()], auditor ?? new RecordingAuditor());
}
