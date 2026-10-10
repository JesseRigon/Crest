using Microsoft.Extensions.Localization;
using Crest.Environment.Commands;
using Crest.Users.Models;
using Crest.Users.Services;

namespace Crest.Users.Commands;

public class UserCommands : DefaultCommandHandler
{
    private readonly IUserService _userService;

    public UserCommands(
        IUserService userService,
        IStringLocalizer<UserCommands> localizer) : base(localizer)
    {
        _userService = userService;
    }

    [PlatformSwitch]
    public string UserName { get; set; }

    [PlatformSwitch]
    public string Password { get; set; }

    [PlatformSwitch]
    public string Email { get; set; }

    [PlatformSwitch]
    public string PhoneNumber { get; set; }

    [PlatformSwitch]
    public string Roles { get; set; }

    [CommandName("createUser")]
    [CommandHelp("createUser /UserName:<username> /Password:<password> /Email:<email> /PhoneNumber:<phonenumber> /Roles:{rolename,rolename,...}\r\n\t" + "Creates a new User")]
    [PlatformSwitches("UserName,Password,Email,PhoneNumber,Roles")]
    public async Task CreateUserAsync()
    {
        var roleNames = (Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToArray();

        var valid = true;

        await _userService.CreateUserAsync(new User { UserName = UserName, Email = Email, PhoneNumber = PhoneNumber, RoleNames = roleNames, EmailConfirmed = true }, Password, (key, message) =>
        {
            valid = false;
            Context.Output.WriteLine(message);
        });

        if (valid)
        {
            Context.Output.WriteLine(S["User created successfully"]);
        }
    }
}
