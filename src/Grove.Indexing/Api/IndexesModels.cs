#nullable enable

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Grove.Indexing;
using Grove.Indexing;
using Grove.Indexing.Models;

namespace Grove.Indexing.Api;

public sealed record IndexSummary(string Id, string? Name, string? Provider, string? IndexName, string? Type, string? CreatedUtc)
{ public static IndexSummary From(IndexProfile profile) => new(profile.Id, profile.Name, profile.ProviderName, profile.IndexName, profile.Type, profile.CreatedUtc.ToString("O")); }
