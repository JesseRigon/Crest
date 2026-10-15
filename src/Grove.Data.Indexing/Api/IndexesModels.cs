#nullable enable

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Indexing;
using Grove.Data.Indexing;
using Grove.Data.Indexing.Models;

namespace Grove.Data.Indexing.Api;

public sealed record IndexSummary(string Id, string? Name, string? Provider, string? IndexName, string? Type, string? CreatedUtc)
{ public static IndexSummary From(IndexProfile profile) => new(profile.Id, profile.Name, profile.ProviderName, profile.IndexName, profile.Type, profile.CreatedUtc.ToString("O")); }
