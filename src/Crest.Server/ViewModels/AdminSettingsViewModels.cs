using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Crest.Admin;
using Crest.Admin.Models;
using Crest.Entities;
using Crest.Settings;

namespace Crest.ViewModels;

public sealed record AdminSettingsUpdate(
    bool DisplayThemeToggler,
    bool DisplayMenuFilter,
    bool DisplayNewMenu,
    bool DisplayTitlesInTopbar);
