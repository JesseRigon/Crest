using System.Text.Json.Nodes;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Builders;
using Crest.ContentManagement.Metadata.Models;
using Crest.Controllers;
using Crest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Crest.Server.Tests;

// The content definition editor (step 7): settings are replaced as a whole from their JSON, and a
// new type's technical name is validated before anything is written.
public sealed class DefinitionSettingsTests
{
    [Fact]
    public void ReplaceSettingsDropsSectionsTheJsonNoLongerHas()
    {
        var existing = new ContentTypeDefinitionBuilder()
            .WithName("Article")
            .MergeSettings(new JsonObject { ["ContentTypeSettings"] = new JsonObject { ["Creatable"] = true }, ["OldSection"] = new JsonObject { ["Value"] = 1 } })
            .Build();

        var replaced = new ContentTypeDefinitionBuilder(existing)
            .ReplaceSettings(new JsonObject { ["ContentTypeSettings"] = new JsonObject { ["Listable"] = true } })
            .Build();

        Assert.Null(replaced.Settings["OldSection"]);
        Assert.Null(replaced.Settings["ContentTypeSettings"]?["Creatable"]);
        Assert.True(replaced.Settings["ContentTypeSettings"]?["Listable"]?.GetValue<bool>());
    }

    [Theory]
    [InlineData("1Article")]
    [InlineData("Blog Post")]
    [InlineData("")]
    public async Task ATypeNeedsATechnicalName(string name)
    {
        var definitions = Substitute.For<IContentDefinitionManager>();
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(AuthorizationResult.Success());
        var controller = new ContentDefinitionsController(definitions, authorization, new DefinitionLockGuard(definitions))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.CreateTypeAsync(new CreateContentTypeRequest(name, "Display"));

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(result).StatusCode);
        await definitions.DidNotReceiveWithAnyArgs().StoreTypeDefinitionAsync(default!);
    }
}
