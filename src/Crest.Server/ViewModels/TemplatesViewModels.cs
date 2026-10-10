using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Crest.Templates;
using Crest.Templates.Models;
using Crest.Templates.Services;
using TemplatesPermissions = Crest.Templates.Permissions;

namespace Crest.ViewModels;

public sealed record CrestTemplate(string Name, string? Description, string Content);

public sealed record CrestTemplateWrite(string? Description, string? Content);
