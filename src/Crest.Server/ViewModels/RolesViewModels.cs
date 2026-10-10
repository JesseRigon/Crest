using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Crest.Roles;
using Crest.Security.Services;

namespace Crest.ViewModels;

public sealed record Role(string Name, string Description, bool IsAdmin, bool IsSystem);
