using System.Text.Json.Nodes;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Builders;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes;
using Crest.ContentTypes.Api;
using Microsoft.AspNetCore.Http.HttpResults;
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
    public void ATypeNeedsATechnicalName(string name)
    {
        // The request's rule (docs/MVC/api.md › Validation) refuses the name before the handler runs.
        var request = new CreateContentTypeRequest(name, "Display");
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, new System.ComponentModel.DataAnnotations.ValidationContext(request), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, result => result.MemberNames.Contains("name"));
    }

    [Fact]
    public void ATechnicalNamePasses()
    {
        var request = new CreateContentTypeRequest("Article", "Display");
        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, new System.ComponentModel.DataAnnotations.ValidationContext(request), [], validateAllProperties: true));
    }
}
