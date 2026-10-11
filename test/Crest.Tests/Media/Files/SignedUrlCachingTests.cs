#nullable enable
using Crest.Environment.Cache;
using Crest.Media;
using Crest.Media.Files;
using Crest.Media.Library;
using Crest.Sharing;
using Crest.Tests.Media.Migration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Crest.Tests.Media.Files;

/// <summary>
/// A page holding a private file's signed URL is never cached for anyone else (docs/media.md ›
/// Serving and CDNs): the dynamic cache scope around it varies by user and expires before the
/// URL does, and the response is marked private. A public file's URL leaves both alone.
/// </summary>
public class SignedUrlCachingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class RecordingCacheScopes : ICacheScopeManager
    {
        public List<string> Contexts { get; } = [];

        public DateTimeOffset? ExpiresOn { get; private set; }

        public void EnterScope(CacheContext context)
        {
        }

        public void ExitScope()
        {
        }

        public void AddDependencies(params string[] dependencies)
        {
        }

        public void AddContexts(params string[] contexts) => Contexts.AddRange(contexts);

        public void WithExpiryOn(DateTimeOffset expiryOn) => ExpiresOn = expiryOn;

        public void WithExpiryAfter(TimeSpan expiryAfter)
        {
        }

        public void WithExpirySliding(TimeSpan expirySliding)
        {
        }
    }

    /// <summary>The library world's services, plus a cache scope manager.</summary>
    private sealed class WithCacheScopes(IServiceProvider inner, ICacheScopeManager scopes) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(ICacheScopeManager) ? scopes : inner.GetService(serviceType);
    }

    private static (MediaReferenceResolver Resolver, RecordingCacheScopes Scopes, HttpContext Http) Resolver(LibraryWorld world, string userId)
    {
        var caller = FilesWorld.User(userId);
        var scopes = new RecordingCacheScopes();
        var http = new DefaultHttpContext();
        var resolver = new MediaReferenceResolver(
            world.Files.Service(caller),
            world.Library(),
            new WithCacheScopes(world.Services(caller), scopes),
            new HttpContextAccessor { HttpContext = http },
            new global::Crest.Environment.Shell.ShellSettings { Name = "Default" },
            world.Files.Sharing.Clock,
            Options.Create(new MediaOptions { AssetsRequestPath = "/media" }),
            Options.Create(new MediaLibraryOptions()));
        return (resolver, scopes, http);
    }

    [Fact]
    public async Task ASignedUrlKeepsThePagePrivateAndItsCachedPartsPerUser()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var file = await world.FileAsync(drive.Placement.Id, "secret.png");
        world.Files.Share(file.Placement.Id, ShareEntry.Grant(SharePrincipal.User("alice"), AccessLevel.Viewer));
        var (resolver, scopes, http) = Resolver(world, "alice");

        var url = await resolver.GetUrlAsync("secret.png", cancellationToken: Token);

        Assert.Contains(HmacMediaUrlSigner.SignatureParameter + "=", url);
        Assert.Contains("user", scopes.Contexts);
        var now = new DateTimeOffset(DateTime.SpecifyKind(world.Files.Sharing.Clock.UtcNow, DateTimeKind.Utc));
        Assert.True(scopes.ExpiresOn < now + new MediaLibraryOptions().SignedUrlLifetime);
        Assert.Equal("private, no-store", http.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task APublicUrlLeavesCachingAlone()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        await world.FileAsync(drive.Placement.Id, "logo.png");
        world.SharePublic(drive.Placement.Id);
        var (resolver, scopes, http) = Resolver(world, "alice");

        var url = await resolver.GetUrlAsync("logo.png", cancellationToken: Token);

        Assert.DoesNotContain(HmacMediaUrlSigner.SignatureParameter + "=", url);
        Assert.Empty(scopes.Contexts);
        Assert.Null(scopes.ExpiresOn);
        Assert.Equal(string.Empty, http.Response.Headers.CacheControl.ToString());
    }
}
