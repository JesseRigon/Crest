using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Grove.Environment.Shell;
using Grove.Routing;
using Grove.Security.Permissions;
using Grove.Infrastructure.Settings;
using Grove.Infrastructure.Settings.Groups;
using Grove.Validation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Grove.Tests;

/// <summary>
/// The settings form's server half (docs/MVC/parity.md › SettingsForm): the schema derived from
/// the model type, the values read and applied by wire name, the rules' errors keyed by field,
/// secrets write-only and protected, a save only when something changed.
/// </summary>
public sealed class SettingsGroupTests
{
    private static readonly Permission s_manage = new("ManageSampleSettings", "Manage sample settings");

    private static readonly EphemeralDataProtectionProvider s_protection = new();

    public enum Mode
    {
        [Display(Name = "Off entirely")]
        Off,
        On,
    }

    public sealed class Limit
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [InclusiveBetween(1, 100)]
        public int Permits { get; set; } = 1;
    }

    public sealed class SampleSettings
    {
        [Display(Name = "Site title", Description = "Shown in the header.", Order = 1)]
        [NotEmpty, MaximumLength(20)]
        public string Title { get; set; } = "Grove";

        [Display(GroupName = "Limits", Order = 2)]
        [GreaterThanOrEqual(0)]
        public int RetentionDays { get; set; } = 30;

        public bool Enabled { get; set; }

        public Mode Mode { get; set; }

        [Setting(Separator = " ")]
        public string Scopes { get; set; } = "openid profile";

        public string[] Hosts { get; set; } = [];

        public Uri? Authority { get; set; }

        [Setting(Secret = true, ProtectedBy = "Sample")]
        public string? ClientSecret { get; set; }

        [Setting(VisibleWhen = "Mode=On")]
        public string? OnlyWhenOn { get; set; }

        public List<Limit> Limits { get; set; } = [];

        [ScaffoldColumn(false)]
        public string Hidden { get; set; } = "kept";
    }

    private sealed class Store
    {
        public SampleSettings Current { get; set; } = new();

        public int Saves { get; set; }
    }

    private static (SettingsGroupService Service, SettingsGroup Group, Store Store, IShellReleaseManager Release) Create(bool reloads = false)
    {
        var store = new Store();
        var release = Substitute.For<IShellReleaseManager>();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        services.AddSingleton(release);
        services.AddSingleton<IDataProtectionProvider>(s_protection);
        services.AddSettingsGroup<SampleSettings>("sample", "Sample", s_manage, group =>
        {
            group.StoredBy(
                (provider, _) => Task.FromResult(Clone(provider.GetRequiredService<Store>().Current)),
                (provider, model, _) =>
                {
                    var target = provider.GetRequiredService<Store>();
                    target.Current = model;
                    target.Saves++;
                    return Task.CompletedTask;
                })
                .ValidatedBy((_, model, errors, _) =>
                {
                    if (model.Title == "taken")
                    {
                        errors.Add(nameof(SampleSettings.Title), "That title is taken.");
                    }

                    return Task.CompletedTask;
                });
            if (reloads)
            {
                group.ReloadsTenant();
            }
        });
        services.AddScoped<SettingsGroupService>();
        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<SettingsGroupService>();
        return (service, service.Find("sample")!, store, release);
    }

    private static SampleSettings Clone(SampleSettings settings) =>
        System.Text.Json.JsonSerializer.Deserialize<SampleSettings>(System.Text.Json.JsonSerializer.Serialize(settings))!;

    [Fact]
    public async Task TheSchemaIsDerivedFromTheModelType()
    {
        var (service, group, _, _) = Create();

        var form = await service.GetFormAsync(group, TestContext.Current.CancellationToken);
        var fields = form.Schema.Fields.ToDictionary(field => field.Name);

        Assert.Equal(["title", "retentionDays", "enabled", "mode", "scopes", "hosts", "authority", "clientSecret", "onlyWhenOn", "limits"], form.Schema.Fields.Select(field => field.Name));
        Assert.DoesNotContain("hidden", fields.Keys);

        Assert.Equal("Site title", fields["title"].Label);
        Assert.Equal("Shown in the header.", fields["title"].Description);
        Assert.True(fields["title"].Required);
        Assert.Equal(20, fields["title"].MaxLength);
        Assert.Equal("text", fields["title"].Kind);

        Assert.Equal("Retention days", fields["retentionDays"].Label);
        Assert.Equal("Limits", fields["retentionDays"].Section);
        Assert.Equal("integer", fields["retentionDays"].Kind);
        Assert.Equal(0, fields["retentionDays"].Min);

        Assert.Equal("boolean", fields["enabled"].Kind);
        Assert.Equal("select", fields["mode"].Kind);
        Assert.Equal([new SettingOption("Off", "Off entirely"), new SettingOption("On", "On")], fields["mode"].Options);
        Assert.Equal("list", fields["scopes"].Kind);
        Assert.Equal("list", fields["hosts"].Kind);
        Assert.Equal("url", fields["authority"].Kind);
        Assert.True(fields["clientSecret"].Secret);
        Assert.Equal("password", fields["clientSecret"].Kind);
        Assert.Equal("mode", fields["onlyWhenOn"].VisibleWhen!.Field);
        Assert.Equal(["On"], fields["onlyWhenOn"].VisibleWhen!.Values);

        Assert.Equal("objects", fields["limits"].Kind);
        Assert.Equal(["name", "permits"], fields["limits"].Fields!.Select(field => field.Name));
        Assert.Equal(100, fields["limits"].Fields![1].Max);
    }

    [Fact]
    public async Task ValuesAreReadByWireNameAndSecretsAreNeverRead()
    {
        var (service, group, store, _) = Create();
        store.Current.ClientSecret = "protected";

        var form = await service.GetFormAsync(group, TestContext.Current.CancellationToken);

        Assert.Equal("Grove", form.Values["title"]!.GetValue<string>());
        Assert.Equal("Off", form.Values["mode"]!.GetValue<string>());
        Assert.Equal(["openid", "profile"], form.Values["scopes"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.False(form.Values.ContainsKey("clientSecret"));
        Assert.True(form.Secrets["clientSecret"]);
    }

    [Fact]
    public async Task ASaveAppliesTheValuesAndProtectsTheSecret()
    {
        var (service, group, store, release) = Create(reloads: true);

        var result = await service.SaveAsync(group, new JsonObject
        {
            ["title"] = "Docs",
            ["retentionDays"] = 7,
            ["enabled"] = true,
            ["mode"] = "on",
            ["scopes"] = new JsonArray("openid", "email"),
            ["hosts"] = "a.example\nb.example",
            ["authority"] = "https://login.example/",
            ["clientSecret"] = "s3cret",
            ["limits"] = new JsonArray(new JsonObject { ["name"] = "api", ["permits"] = 5 }),
            ["hidden"] = "changed",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.Changed);
        Assert.Equal(1, store.Saves);
        Assert.Equal("Docs", store.Current.Title);
        Assert.Equal(7, store.Current.RetentionDays);
        Assert.True(store.Current.Enabled);
        Assert.Equal(Mode.On, store.Current.Mode);
        Assert.Equal("openid email", store.Current.Scopes);
        Assert.Equal(["a.example", "b.example"], store.Current.Hosts);
        Assert.Equal(new Uri("https://login.example/"), store.Current.Authority);
        Assert.Equal("api", Assert.Single(store.Current.Limits).Name);
        Assert.Equal("kept", store.Current.Hidden);
        Assert.NotEqual("s3cret", store.Current.ClientSecret);
        Assert.Equal("s3cret", s_protection.CreateProtector("Sample").Unprotect(store.Current.ClientSecret!));
        release.Received(1).RequestRelease();

        // An empty secret keeps the stored one; nothing else changed, so nothing is saved.
        var again = await service.SaveAsync(group, new JsonObject { ["clientSecret"] = "", ["title"] = "Docs" }, TestContext.Current.CancellationToken);
        Assert.True(again.Succeeded);
        Assert.False(again.Changed);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task RuleFailuresAreKeyedByField()
    {
        var (service, group, store, _) = Create();

        var result = await service.SaveAsync(group, new JsonObject
        {
            ["title"] = " ",
            ["retentionDays"] = -1,
            ["mode"] = "Sideways",
            ["authority"] = "not a url",
            ["limits"] = new JsonArray(new JsonObject { ["name"] = "", ["permits"] = 500 }),
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("mode", result.Errors.Keys);
        Assert.Contains("authority", result.Errors.Keys);
        Assert.Equal(0, store.Saves);

        // Once the values convert, the model's rules run.
        var rules = await service.SaveAsync(group, new JsonObject { ["title"] = " ", ["retentionDays"] = -1 }, TestContext.Current.CancellationToken);
        Assert.Contains("title", rules.Errors.Keys);
        Assert.Contains("retentionDays", rules.Errors.Keys);

        // The group's own rules run after the model's passed.
        var taken = await service.SaveAsync(group, new JsonObject { ["title"] = "taken" }, TestContext.Current.CancellationToken);
        Assert.Equal(["That title is taken."], taken.Errors["title"]);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void EachGroupIsAPageAtItsConcreteRoute()
    {
        var services = new ServiceCollection();
        services.AddSettingsGroup<SampleSettings>("sample", "Sample", s_manage, group => group.InMenu("settings-integrations", "5", entryId: "sample-entry"));
        var registered = services.BuildServiceProvider().GetRequiredService<IOptions<SettingsGroupOptions>>();

        var entry = Assert.Single(new SettingsGroupPages(registered).GetPages());

        Assert.Equal("sample-entry", entry.Id);
        Assert.Equal(["/Settings/sample"], entry.Routes);
        Assert.Equal([s_manage.Name], entry.Permissions);
        Assert.Equal("settings-integrations", entry.Menu!.Parent);
        Assert.Equal("/Settings/sample", entry.Menu.Url);
        Assert.Null(entry.Component);

        var permissions = services.BuildServiceProvider().GetRequiredService<IOptions<SiteSettingsPermissionOptions>>().Value;
        Assert.Equal(s_manage.Name, Assert.Single(permissions.GroupPermissions["sample"]).Name);
    }

    public sealed class ProviderSettings
    {
        [Required]
        public string? AccountId { get; set; }
    }

    [Fact]
    public async Task AGroupOfSeveralModelsHasASectionPerModel()
    {
        var stored = new ProviderSettings { AccountId = "a1" };
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IShellReleaseManager>());
        services.AddSettingsGroup<SampleSettings>("sms", "SMS", s_manage, group => group.StoredBy((_, _) => Task.FromResult(new SampleSettings()), (_, _, _) => Task.CompletedTask));
        services.AddSettingsModel<ProviderSettings>("sms", "Provider", model => model
            .ReloadsTenant()
            .StoredBy((_, _) => Task.FromResult(new ProviderSettings { AccountId = stored.AccountId }), (_, value, _) =>
            {
                stored = value;
                return Task.CompletedTask;
            }));
        services.AddScoped<SettingsGroupService>();
        var service = services.BuildServiceProvider().GetRequiredService<SettingsGroupService>();
        var group = service.Find("sms")!;

        var form = await service.GetFormAsync(group, TestContext.Current.CancellationToken);
        Assert.Equal(["sampleSettings", "providerSettings"], form.Schema.Fields.Select(field => field.Name));
        Assert.Equal("Provider", form.Schema.Fields[1].Label);
        Assert.Equal("object", form.Schema.Fields[1].Kind);
        Assert.True(form.Schema.ReloadsTenant);
        Assert.Equal("a1", form.Values["providerSettings"]!["accountId"]!.GetValue<string>());

        var invalid = await service.SaveAsync(group, new JsonObject { ["providerSettings"] = new JsonObject { ["accountId"] = null } }, TestContext.Current.CancellationToken);
        Assert.Contains("providerSettings.accountId", invalid.Errors.Keys);

        var saved = await service.SaveAsync(group, new JsonObject { ["providerSettings"] = new JsonObject { ["accountId"] = "b2" } }, TestContext.Current.CancellationToken);
        Assert.True(saved.Succeeded);
        Assert.True(saved.Reloaded);
        Assert.Equal("b2", stored.AccountId);
    }

    [Fact]
    public async Task APageDeclaredBySourceServesOnlyTheRoutesSourcesDeclare()
    {
        var generic = new PageEntry { Id = "form", Routes = ["/Settings/{GroupId}"], Component = typeof(SettingsGroupTests), DeclaredBySource = true };
        var native = new PageEntry { Id = "general", Routes = ["/Settings/general"], Component = typeof(string) };
        var declared = new PageEntry { Id = "sample", Routes = ["/Settings/sample"] };
        var registry = Substitute.For<IPageRegistry>();
        registry.GetAsync(Arg.Any<CancellationToken>()).Returns(new PageSet([generic, native, declared]));

        var table = await new DefaultRouteComponentTableManager(registry).GetRouteComponentTableAsync();

        Assert.True(table.TryMatch("/Settings/sample", ShellId.Admin, out var sample));
        Assert.Equal(typeof(SettingsGroupTests), sample.ComponentType);
        Assert.True(table.TryMatch("/Settings/general", ShellId.Admin, out var general));
        Assert.Equal(typeof(string), general.ComponentType);
        Assert.False(table.TryMatch("/Settings/legacy-group", ShellId.Admin, out _));
    }
}
