#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grove.Media;
using Grove.Media.Processing;
using Grove.Media.Models;
using Grove.Media.Services;

namespace Grove.Media.Api;

public sealed record MediaProfileDto(string Name, string? Hint, int Width, int Height, ResizeMode Mode, Format Format, int Quality, string? BackgroundColor, bool AutoOrient)
{
    public static MediaProfileDto From(string name, MediaProfile profile) => new(name, profile.Hint, profile.Width, profile.Height, profile.Mode, profile.Format, profile.Quality, profile.BackgroundColor, profile.AutoOrient);
}

public sealed record MediaProfileWriteRequest(string? Hint, int Width, int Height, ResizeMode Mode, Format Format, int Quality, string? BackgroundColor, bool AutoOrient);
