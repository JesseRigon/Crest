#nullable enable

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Crest.Indexing;
using Crest.Indexing;
using Crest.Indexing.Models;

namespace Crest.Indexing.Api;

public sealed record IndexSummary(string Id, string? Name, string? Provider, string? IndexName, string? Type, string? CreatedUtc)
{ public static IndexSummary From(IndexProfile profile) => new(profile.Id, profile.Name, profile.ProviderName, profile.IndexName, profile.Type, profile.CreatedUtc.ToString("O")); }
