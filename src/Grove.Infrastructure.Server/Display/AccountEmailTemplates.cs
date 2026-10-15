using Grove.Display.Templates.Services;
using Grove.Access.Users.Services;

namespace Grove.Display;

/// <summary>
/// The shipped account email bodies (step 7: the Razor <c>TemplateUserConfirmEmail</c> and
/// <c>TemplateUserLostPassword</c> shapes are retired): the last level of the template chain, so a
/// tenant template of the same name (Design › Templates) replaces each. Free text becomes
/// sanitized markup; <c>Model</c> is what <see cref="UserEmailService"/> passes (<c>UserName</c>,
/// <c>Email</c>, <c>Url</c>, <c>ExpiresInMinutes</c>).
/// </summary>
public sealed class AccountEmailTemplates : IShippedTemplateProvider
{
    public const string ConfirmEmailSource = """
        <p>Dear {{ Model.UserName }},</p>
        <p>Please <a href="{{ Model.Url }}">click here</a> to confirm your account.</p>
        <p>Please be aware that this link will expire in {{ Model.ExpiresInMinutes }} minutes.</p>
        <p>Thank you</p>
        """;

    public const string LostPasswordSource = """
        <p>Someone recently requested that the password be reset for <b>{{ Model.UserName }}</b>.</p>
        <p>To reset your password please click this link: <a href="{{ Model.Url }}">Reset Password</a></p>
        <p>If you did not request a password reset please ignore this email - your password will not be changed.</p>
        <p>Please be aware that this link will expire in {{ Model.ExpiresInMinutes }} minutes.</p>
        """;

    private static readonly TemplateRecord[] s_templates =
    [
        Template(UserEmailService.ConfirmEmailTemplate, ConfirmEmailSource, "The email asking a new account to confirm its address."),
        Template(UserEmailService.LostPasswordTemplate, LostPasswordSource, "The email carrying a password reset link."),
    ];

    public Task<IReadOnlyList<TemplateRecord>> GetTemplatesAsync() => Task.FromResult<IReadOnlyList<TemplateRecord>>(s_templates);

    private static TemplateRecord Template(string name, string source, string description) =>
        new("shipped:" + name, "shipped:" + name, name, Theme: null, DisplayType: null, source, description, Published: true, Latest: true, ModifiedUtc: null)
        {
            Category = "Email",
        };
}
