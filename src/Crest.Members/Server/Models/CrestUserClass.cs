using Crest.Members.Constants;

namespace Crest.Members.Models;

/// <summary>
/// The user-class aspect stored in <c>User.Properties</c> under this type's name (the
/// stock entity-aspect pattern - <c>EntityExtensions.GetOrCreate&lt;T&gt;</c>). NOT a
/// role and NOT a custom user setting: roles are grantable (the class must not be),
/// and custom user settings are unqueryable (the service enumerates every user in
/// memory). Queryability comes from <see cref="Indexes.UserClassIndex"/>.
/// </summary>
public class CrestUserClass
{
    /// <summary>One of <see cref="UserClasses"/>. Defaults to staff: every user
    /// created outside the member-portal provisioning flow is a tenant user.</summary>
    public string Class { get; set; } = UserClasses.Staff;
}
