#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Grove.FileStorage;
using Grove.Media;
using Grove.Security.Permissions;

namespace Grove.Media.Api;

public sealed record MediaDirectoryResult(string Path, string? ParentPath, MediaEntry[] Entries);

public sealed record MediaEntry(string Path, string Name, bool IsDirectory, long Length, DateTimeOffset LastModifiedUtc, string? PublicUrl);

public sealed record MediaFolderRequest(string? ParentPath, string? Name);
