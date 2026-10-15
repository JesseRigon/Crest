using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Fluid.Values;
using Grove.Access;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Data.Scoping;
using Grove.Display.Liquid;
using Grove.Models;
using Grove.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Grove.Contents.OptionPicker.Tests;

// The content items source as the one way to pick items (docs/server.md § 3): which types its
// qualifier names, labels from the title pattern, and create links for the allowed creatable types
// the caller may create.
public class ContentItemSourceTests
{
    [Fact]
    public void A_qualifier_names_types_stereotypes_or_every_type()
    {
        var scope = ContentItemSourceScope.Parse("Customer, @Party ,Vendor");
        Assert.Equal(["Customer", "Vendor"], scope.Types);
        Assert.Equal(["Party"], scope.Stereotypes);
        Assert.False(scope.AllTypes);

        Assert.True(ContentItemSourceScope.Parse("*").AllTypes);
        Assert.True(ContentItemSourceScope.Parse(string.Empty).IsEmpty);
        Assert.Equal("contentitem:Customer,@Party", OptionSourceKeys.ForContentItems(["Customer"], ["Party"]));
        Assert.Equal("contentitem:*", OptionSourceKeys.ForContentItems(["Customer"], allTypes: true));
    }

    [Fact]
    public async Task Labels_come_from_the_title_pattern()
    {
        var liquid = Substitute.For<ILiquidTemplateManager>();
        liquid.RenderStringAsync("{{ x }}", Arg.Any<TextEncoder>(), Arg.Any<object>(), Arg.Any<IEnumerable<KeyValuePair<string, FluidValue>>>())
            .Returns(call => Task.FromResult(" Title of " + ((ContentItem)call[2]!).ContentItemId + " "));
        var provider = Provider(services => services.GetService(typeof(ILiquidTemplateManager)).Returns(liquid));
        var item = new ContentItem { ContentItemId = "a", DisplayText = "Plain", ContentType = "Customer" };

        var titles = await provider.RenderTitlesAsync([item], "{{ x }}");
        var row = ContentItemOptionSourceProvider.ToRow(item, [], titles);

        Assert.Equal("Title of a", row.Values["DisplayText"]);
        Assert.Equal("Plain", ContentItemOptionSourceProvider.ToRow(item, [], await provider.RenderTitlesAsync([item], null)).Values["DisplayText"]);
    }

    [Fact]
    public async Task Create_links_are_the_creatable_allowed_types_the_caller_may_create()
    {
        var definitions = Substitute.For<IContentDefinitionManager>();
        definitions.GetTypeDefinitionAsync("Customer").Returns(TypeDefinition("Customer", creatable: true));
        definitions.GetTypeDefinitionAsync("Vendor").Returns(TypeDefinition("Vendor", creatable: false));
        definitions.GetTypeDefinitionAsync("Secret").Returns(TypeDefinition("Secret", creatable: true));

        var contentManager = Substitute.For<IContentManager>();
        contentManager.NewAsync(Arg.Any<string>()).Returns(call => new ContentItem { ContentType = (string)call[0]! });

        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(call => ((ContentItem)call[1]!).ContentType == "Secret" ? AuthorizationResult.Failed() : AuthorizationResult.Success());

        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) } };
        var provider = Provider(services =>
        {
            services.GetService(typeof(IHttpContextAccessor)).Returns(http);
            services.GetService(typeof(IContentManager)).Returns(contentManager);
            services.GetService(typeof(IAuthorizationService)).Returns(authorization);
        }, definitions);

        var links = await provider.GetCreateLinksAsync("Customer,Vendor,Secret", TestContext.Current.CancellationToken);

        var link = Assert.Single(links);
        Assert.Equal("New Customer", link.Label);
        Assert.Equal("Contents/ContentTypes/Customer/Create", link.Url);
    }

    private static ContentItemOptionSourceProvider Provider(Action<IServiceProvider> setup, IContentDefinitionManager? definitions = null)
    {
        var services = Substitute.For<IServiceProvider>();
        setup(services);
        return new ContentItemOptionSourceProvider(
            Substitute.For<YesSql.ISession>(),
            Substitute.For<ICallerContextAccessor>(),
            Substitute.For<IScopeSetProvider>(),
            definitions ?? Substitute.For<IContentDefinitionManager>(),
            services);
    }

    private static ContentTypeDefinition TypeDefinition(string name, bool creatable) =>
        new(name, name, [], new JsonObject { ["ContentTypeSettings"] = new JsonObject { ["Creatable"] = creatable } });
}
