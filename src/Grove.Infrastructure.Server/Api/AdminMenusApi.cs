using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Globalization;
using Grove.Display.Icons;
using Grove.Services;
using Microsoft.AspNetCore.Authorization;
using Grove.Contents.AdminMenu.AdminNodes;
using Grove.Contents.AdminMenu.Models;
using Grove.Contents.AdminMenu.Services;
using Grove.Display.Navigation;
using Grove.ViewModels;

namespace Grove.Api;

using AdminMenu = Grove.Contents.AdminMenu.Models.AdminMenu;

/// <summary>The host's <c>api/grove/admin-menus</c> endpoints.</summary>
public sealed class AdminMenusApi : ApiContributor
{
    public override string Name => "AdminMenus";

    public override string FeatureId => "Grove";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("admin-menus");
        routes.MapGet("", ApiHandlers.Handle<AdminMenusHandlers>(h => h.ListAsync()));
        routes.MapGet("{menuId}", (HttpContext http, [FromRoute] string menuId) => ApiHandlers.For<AdminMenusHandlers>(http).GetAsync(menuId));
        routes.MapPost("", (HttpContext http, [FromBody] AdminMenuEditModel model) => ApiHandlers.For<AdminMenusHandlers>(http).CreateMenuAsync(model));
        routes.MapPost("{menuId}/rename", (HttpContext http, [FromRoute] string menuId, [FromBody] AdminMenuEditModel model) => ApiHandlers.For<AdminMenusHandlers>(http).RenameMenuAsync(menuId, model));
        routes.MapPost("{menuId}/toggle", (HttpContext http, [FromRoute] string menuId) => ApiHandlers.For<AdminMenusHandlers>(http).ToggleMenuAsync(menuId));
        routes.MapPost("{menuId}/duplicate", (HttpContext http, [FromRoute] string menuId) => ApiHandlers.For<AdminMenusHandlers>(http).DuplicateMenuAsync(menuId));
        routes.MapDelete("{menuId}", (HttpContext http, [FromRoute] string menuId) => ApiHandlers.For<AdminMenusHandlers>(http).DeleteMenuAsync(menuId));
        routes.MapPost("{menuId}/nodes", (HttpContext http, [FromRoute] string menuId, [FromBody] AdminMenuNodeEditModel model) => ApiHandlers.For<AdminMenusHandlers>(http).CreateNodeAsync(menuId, model));
        routes.MapPost("{menuId}/separators", (HttpContext http, [FromRoute] string menuId, [FromBody] AdminMenuSeparatorEditModel model) => ApiHandlers.For<AdminMenusHandlers>(http).CreateSeparatorAsync(menuId, model));
        routes.MapPost("{menuId}/primary-nav-menu-settings", (HttpContext http, [FromRoute] string menuId, [FromBody] PrimaryNavMenuSettings settings) => ApiHandlers.For<AdminMenusHandlers>(http).UpdatePrimaryNavMenuSettingsAsync(menuId, settings));
        routes.MapDelete("{menuId}/separators/{separatorId}", (HttpContext http, [FromRoute] string menuId, [FromRoute] string separatorId) => ApiHandlers.For<AdminMenusHandlers>(http).DeleteSeparatorAsync(menuId, separatorId));
        routes.MapPost("{menuId}/separators/{separatorId}/move", (HttpContext http, [FromRoute] string menuId, [FromRoute] string separatorId, [FromBody] AdminMenuSeparatorEditModel model) => ApiHandlers.For<AdminMenusHandlers>(http).MoveSeparatorAsync(menuId, separatorId, model));
        routes.MapPut("{menuId}/nodes/{nodeId}", (HttpContext http, [FromRoute] string menuId, [FromRoute] string nodeId, [FromBody] AdminMenuNodeEditModel model) => ApiHandlers.For<AdminMenusHandlers>(http).UpdateNodeAsync(menuId, nodeId, model));
        routes.MapPost("{menuId}/nodes/{nodeId}/rename", (HttpContext http, [FromRoute] string menuId, [FromRoute] string nodeId, [FromBody] AdminMenuNodeRenameModel model) => ApiHandlers.For<AdminMenusHandlers>(http).RenameNodeAsync(menuId, nodeId, model));
        routes.MapPost("{menuId}/nodes/{nodeId}/promote-rename", (HttpContext http, [FromRoute] string menuId, [FromRoute] string nodeId) => ApiHandlers.For<AdminMenusHandlers>(http).PromoteRenameAsync(menuId, nodeId));
        routes.MapPost("{menuId}/nodes/{nodeId}/move", (HttpContext http, [FromRoute] string menuId, [FromRoute] string nodeId, [FromBody] AdminMenuNodeMoveModel model) => ApiHandlers.For<AdminMenusHandlers>(http).MoveNodeAsync(menuId, nodeId, model));
        routes.MapPost("{menuId}/nodes/{nodeId}/toggle", (HttpContext http, [FromRoute] string menuId, [FromRoute] string nodeId) => ApiHandlers.For<AdminMenusHandlers>(http).ToggleNodeAsync(menuId, nodeId));
        routes.MapPost("{menuId}/prune-overrides", (HttpContext http, [FromRoute] string menuId) => ApiHandlers.For<AdminMenusHandlers>(http).PruneOverridesAsync(menuId));
        routes.MapDelete("{menuId}/nodes/{nodeId}", (HttpContext http, [FromRoute] string menuId, [FromRoute] string nodeId) => ApiHandlers.For<AdminMenusHandlers>(http).DeleteNodeAsync(menuId, nodeId));
    }
}

public sealed class AdminMenusHandlers(
    IAuthorizationService authorizationService,
    IAdminMenuService adminMenuService,
    AdminMenuBuilder adminMenuBuilder,
    Grove.Routing.IPageRegistry pageRegistry,
    AdminMenuLayoutService layoutService,
    Grove.Data.Localization.Translations.Services.ITranslationsManager translationsManager,
    MenuPlacementService menuPlacementService,
    PrimaryNavMenuSettingsStore primaryNavMenuSettingsStore,
    AdminSettingsNormalizer adminSettingsNormalizer,
    IconController iconController) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        // The sidebar is the page registry's tree plus the tenant's edits (the Primary
        // Navigation); admin menu documents remain for the other placements only.
        var list = await adminMenuService.GetAdminMenuListAsync();
        var defaultMenu = await GetDefaultMenuSummaryAsync();
        var placements = await menuPlacementService.GetAllAsync();
        return Ok(new AdminMenusState([defaultMenu, .. list.AdminMenu
            .Where(menu => placements.TryGetValue(menu.Id, out var entry) && entry.Placement != MenuPlacement.Admin)
            .Select(menu => WithPlacement(AdminMenuSummary.From(menu), placements))]));
    }

    public async Task<IResult> GetAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == AdminMenuLayoutService.DefaultMenuId)
        {
            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var list = await adminMenuService.GetAdminMenuListAsync();
        var menu = adminMenuService.GetAdminMenuById(list, menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var entry = await menuPlacementService.GetAsync(menuId);
        return Ok(WithPlacement(AdminMenuSummary.From(menu), entry));
    }

    public async Task<IResult> CreateMenuAsync(AdminMenuEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        var name = model.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Menu name is required.");
        }

        // The sidebar takes custom items and links in the Primary Navigation itself, not
        // further admin menu documents. A document's own Enabled flag stays false: the
        // placement entry's Enabled is the visible/hidden flag.
        var placement = model.Placement;
        if (placement == MenuPlacement.Admin)
        {
            return BadRequest("Add custom items and links to the Primary Navigation instead.");
        }

        var menu = new AdminMenu { Name = name, Enabled = false };
        await adminMenuService.SaveAsync(menu);
        await menuPlacementService.SetAsync(menu.Id, placement, model.Enabled);

        return Ok(WithPlacement(AdminMenuSummary.From(menu), placement, model.Enabled));
    }

    public async Task<IResult> RenameMenuAsync(string menuId, AdminMenuEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == AdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("The Primary Navigation menu cannot be renamed.");
        }

        var name = model.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Menu name is required.");
        }

        var list = await adminMenuService.LoadAdminMenuListAsync();
        var menu = adminMenuService.GetAdminMenuById(list, menuId);
        if (menu is null)
        {
            return NotFound();
        }

        menu.Name = name;
        foreach (var node in menu.MenuItems.OfType<AdminNode>())
        {
            SetMenuName(node, name);
        }

        await adminMenuService.SaveAsync(menu);
        return Ok(AdminMenuSummary.From(menu));
    }

    public async Task<IResult> ToggleMenuAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == AdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("The primary navigation menu cannot be hidden.");
        }

        var list = await adminMenuService.LoadAdminMenuListAsync();
        var menu = adminMenuService.GetAdminMenuById(list, menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var entry = await menuPlacementService.GetAsync(menuId);

        // AdminMenu.Enabled stays false (see CreateMenuAsync) — the placement entry's own Enabled is the real visible/hidden flag here.
        var enabled = !entry.Enabled;
        await menuPlacementService.SetAsync(menuId, entry.Placement, enabled);
        return Ok(WithPlacement(AdminMenuSummary.From(menu), entry.Placement, enabled));
    }

    public async Task<IResult> DuplicateMenuAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == AdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("The default admin menu cannot be duplicated.");
        }

        var list = await adminMenuService.GetAdminMenuListAsync();
        var source = adminMenuService.GetAdminMenuById(list, menuId);
        if (source is null)
        {
            return NotFound();
        }

        var sourceEntry = await menuPlacementService.GetAsync(menuId);
        var copy = new AdminMenu
        {
            Name = $"{source.Name} Copy",
            Enabled = source.Enabled,
        };

        foreach (var node in source.MenuItems.OfType<AdminNode>())
        {
            copy.MenuItems.Add(CloneNode(node, copy.Name));
        }

        copy.Enabled = false;
        await adminMenuService.SaveAsync(copy);
        await menuPlacementService.SetAsync(copy.Id, sourceEntry.Placement, sourceEntry.Enabled);

        return Ok(WithPlacement(AdminMenuSummary.From(copy), sourceEntry));
    }

    public async Task<IResult> DeleteMenuAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == AdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("The default admin menu cannot be deleted.");
        }

        var list = await adminMenuService.GetAdminMenuListAsync();
        var menu = adminMenuService.GetAdminMenuById(list, menuId);
        if (menu is null)
        {
            return NotFound();
        }

        // The menu's translations go with it through the admin menu events.
        await adminMenuService.DeleteAsync(menu);
        await menuPlacementService.RemoveAsync(menuId);

        return NoContent();
    }

    public async Task<IResult> CreateNodeAsync(string menuId, AdminMenuNodeEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (RequiredNodeMembers(model) is { } missing)
        {
            return missing;
        }

        if (menuId == AdminMenuLayoutService.DefaultMenuId)
        {
            if (!IsPlaceholderNodeType(model.Type))
            {
                return BadRequest("Primary Navigation only supports custom placeholder nodes.");
            }

            var baseMenu = await BuildDefaultNavigationMenuAsync();
            if (await layoutService.IsLockedNewBranchAsync(baseMenu, model.ParentNodeId))
            {
                return BadRequest("The New menu branch is locked and cannot be edited.");
            }

            await layoutService.CreateCustomAsync(baseMenu, model.Text.Trim(), null, model.IconClass?.Trim(), model.ParentNodeId, model.Position);
            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var node = CreateNode(model.Type);
        if (node is null)
        {
            return BadRequest("Unsupported admin menu node type.");
        }

        node.UniqueId = Guid.NewGuid().ToString("n");
        node.MenuName = menu.Name;
        ApplyNode(node, model);

        if (!TryGetParent(menu, model.ParentNodeId, out var parent))
        {
            return BadRequest("The selected parent admin menu node was not found.");
        }

        var position = ClampPosition(model.Position ?? GetChildCount(menu, parent), GetChildCount(menu, parent));

        if (!menu.InsertMenuItemAt(node, parent, position))
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        await adminMenuService.SaveAsync(menu);
        return Ok(AdminMenuSummary.From(menu));
    }

    public async Task<IResult> CreateSeparatorAsync(string menuId, AdminMenuSeparatorEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != AdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("Separators are currently supported by the Primary Navigation menu only.");
        }

        var baseMenu = await BuildDefaultNavigationMenuAsync();
        if (await layoutService.IsLockedNewBranchAsync(baseMenu, model.ParentNodeId))
        {
            return BadRequest("The New menu branch is locked and cannot be edited.");
        }

        await layoutService.CreateSeparatorAsync(baseMenu, model.ParentNodeId, model.Position);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    public async Task<IResult> UpdatePrimaryNavMenuSettingsAsync(string menuId, PrimaryNavMenuSettings settings)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != AdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("Primary navigation settings are supported by the Primary Navigation menu only.");
        }

        var normalized = await primaryNavMenuSettingsStore.SaveAsync(settings, HttpContext.RequestAborted);
        return Ok((await GetDefaultMenuSummaryAsync()) with { PrimaryNavMenuSettings = normalized });
    }

    public async Task<IResult> DeleteSeparatorAsync(string menuId, string separatorId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != AdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("Separators are currently supported by the Primary Navigation menu only.");
        }

        await layoutService.DeleteSeparatorAsync(await BuildDefaultNavigationMenuAsync(), separatorId);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    public async Task<IResult> MoveSeparatorAsync(string menuId, string separatorId, AdminMenuSeparatorEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != AdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("Separators are currently supported by the Primary Navigation menu only.");
        }

        var baseMenu = await BuildDefaultNavigationMenuAsync();
        if (await layoutService.IsLockedNewBranchAsync(baseMenu, model.ParentNodeId))
        {
            return BadRequest("The New menu branch is locked and cannot be edited.");
        }

        await layoutService.MoveSeparatorAsync(baseMenu, separatorId, model.ParentNodeId, model.Position);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    public async Task<IResult> UpdateNodeAsync(string menuId, string nodeId, AdminMenuNodeEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (RequiredNodeMembers(model) is { } missing)
        {
            return missing;
        }

        if (menuId == AdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
            var editingNewBranch = await layoutService.IsLockedNewBranchAsync(baseMenu, nodeId);
            if (!editingNewBranch && await layoutService.IsLockedNewBranchAsync(baseMenu, model.ParentNodeId))
            {
                return BadRequest("The New menu branch has a fixed structure and cannot accept moved items.");
            }

            if (await layoutService.IsCustomAsync(nodeId))
            {
                await layoutService.UpdateCustomAsync(baseMenu, nodeId, model.Text.Trim(), null, model.IconClass?.Trim());
                if (!editingNewBranch && model.ParentNodeId is not null)
                {
                    await layoutService.MoveAsync(baseMenu, nodeId, model.ParentNodeId, model.Position);
                }
            }
            else
            {
                await layoutService.UpdateItemAsync(
                    baseMenu,
                    nodeId,
                    model.Text,
                    model.IconClass,
                    editingNewBranch ? null : model.ParentNodeId,
                    editingNewBranch ? null : model.Position);
            }

            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var node = menu.GetMenuItemById(nodeId);
        if (node is null)
        {
            return NotFound();
        }

        node.MenuName = menu.Name;
        ApplyNode(node, model);

        if (model.ParentNodeId is not null)
        {
            if (!MoveNode(menu, node, model.ParentNodeId, model.Position))
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        await adminMenuService.SaveAsync(menu);
        return Ok(AdminMenuSummary.From(menu));
    }

    public async Task<IResult> RenameNodeAsync(string menuId, string nodeId, AdminMenuNodeRenameModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != AdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("Primary navigation renames are only supported on the generated Primary Navigation menu.");
        }

        var baseMenu = await BuildDefaultNavigationMenuAsync();
        await layoutService.RenameAsync(baseMenu, nodeId, model.Text);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    // Promotes a rename from the Grove layout document into the tenant's translation store,
    // making it the tenant-wide translation of that caption rather than a Grove-only display
    // override. Separate from the rename endpoint above, and separately authorized: renaming an
    // item in Grove's own sidebar only needs ManageAdminMenu, but writing the tenant's
    // translation store changes what Grove's Razor admin renders for every user of this
    // tenant in that culture, so it additionally requires ManageTranslations (Administrator
    // only by default - see Grove.Data.Localization.Translations's Permissions).
    public async Task<IResult> PromoteRenameAsync(string menuId, string nodeId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu) ||
            !await authorizationService.AuthorizeAsync(User, Grove.Data.Localization.Data.DataLocalizationPermissions.ManageTranslations))
        {
            return Forbid();
        }

        var culture = CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrEmpty(culture))
        {
            // The invariant culture has no translation store entry to write to - a translation
            // has to be attributed to a culture. Callers hit this only when the tenant resolved
            // to invariant, which dev.sh and the Grove recipes now avoid by always configuring
            // a real default culture.
            return BadRequest("A rename can only be promoted while viewing a specific culture.");
        }

        // A Primary Navigation item is a page registry entry: its invariant caption under the
        // menu's translation context. An item of another admin menu document is resolved from
        // its owning menu, whose name scopes the IDataLocalizer context (Grove.Themes.TheAdmin's
        // NavigationItemText.cshtml keys on the item's own MenuName).
        var owner = menuId == AdminMenuLayoutService.DefaultMenuId
            ? await FindEntryAsync(nodeId)
            : await FindNodeAsync(nodeId);
        if (owner is null)
        {
            // A custom item's text is the tenant's own; a generated item (New › content type)
            // translates under its data's context.
            return BadRequest("Only declared menu items can be promoted; this caption is the tenant's own or comes from data.");
        }

        var (menuName, sourceText) = owner.Value;
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return NotFound();
        }

        // Follows whatever the layout currently says for this culture, including "nothing":
        // promoting after a rename has been cleared removes the translation, so the two stores
        // can be brought back into agreement the same way they were put into it.
        var layout = await layoutService.LoadAsync();
        var renamed = AdminMenuLayoutService.GetRename(layout, nodeId, culture);

        // The key is the caption the data localizer looks up within the menu context, not the
        // item's key: callers pass the pre-override caption for that reason.
        await translationsManager.SetTranslationAsync(culture, Grove.Contents.AdminMenu.DataLocalizationContext.AdminMenu(menuName), sourceText, renamed);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    public async Task<IResult> MoveNodeAsync(string menuId, string nodeId, AdminMenuNodeMoveModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == AdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
            if (await layoutService.IsLockedNewBranchAsync(baseMenu, nodeId) || await layoutService.IsLockedNewBranchAsync(baseMenu, model.ParentNodeId))
            {
                return BadRequest("The New menu branch is locked and cannot be edited.");
            }

            await layoutService.MoveAsync(baseMenu, nodeId, model.ParentNodeId, model.Position);
            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var node = menu.GetMenuItemById(nodeId);
        if (node is null)
        {
            return NotFound();
        }

        if (!MoveNode(menu, node, model.ParentNodeId, model.Position))
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        await adminMenuService.SaveAsync(menu);
        return Ok(AdminMenuSummary.From(menu));
    }

    public async Task<IResult> ToggleNodeAsync(string menuId, string nodeId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == AdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
            await layoutService.ToggleAsync(baseMenu, nodeId);
            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var node = menu.GetMenuItemById(nodeId);
        if (node is null)
        {
            return NotFound();
        }

        node.Enabled = !node.Enabled;
        await adminMenuService.SaveAsync(menu);
        return Ok(AdminMenuSummary.From(menu));
    }

    // Removes stored layout overrides that no longer match anything in the CURRENT menu
    // tree - e.g. rows left behind by the fixed Href-based Key bug (see
    // NavigationItem.Key in NavigationViewModels.cs). Only safe to run when all features
    // are enabled: with any feature disabled, that feature's items are also absent from
    // baseMenu and their still-legitimate overrides would be wrongly deleted, since
    // "disabled but installed" and "not present at all" both look identical here.
    public async Task<IResult> PruneOverridesAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != AdminMenuLayoutService.DefaultMenuId)
        {
            return NotFound();
        }

        var baseMenu = await BuildDefaultNavigationMenuAsync();
        await layoutService.PruneOrphanedOverridesAsync(baseMenu);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    public async Task<IResult> DeleteNodeAsync(string menuId, string nodeId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == AdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
            if (await layoutService.IsLockedNewBranchAsync(baseMenu, nodeId))
            {
                return BadRequest("The New menu branch is locked and cannot be edited.");
            }

            if (!await layoutService.IsCustomAsync(nodeId))
            {
                return BadRequest("Only custom primary navigation nodes can be deleted.");
            }

            await layoutService.DeleteCustomAsync(baseMenu, nodeId);
            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var node = menu.GetMenuItemById(nodeId);
        if (node is null)
        {
            return NotFound();
        }

        if (!menu.RemoveMenuItem(node))
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        // The deleted subtree's translations go with it through the admin menu events.
        await adminMenuService.SaveAsync(menu);

        return Ok(AdminMenuSummary.From(menu));
    }

    private async Task<AdminMenuSummary> GetDefaultMenuSummaryAsync()
    {
        // LoadAsync (GetOrCreateMutableAsync), not GetAsync (GetOrCreateImmutableAsync).
        // The immutable overload serves the CACHED document, which does not yet reflect
        // layout mutations made earlier in this same request — those are only visible via
        // the mutable instance until the deferred save commits after the response is
        // written. Every mutating endpoint returns this summary, so reading the immutable
        // copy made each PUT/POST respond with the PREVIOUS state: the write landed, but
        // the response body was always one edit behind.
        var layout = await layoutService.LoadAsync();
        var primaryNavMenuSettings = await primaryNavMenuSettingsStore.GetAsync(HttpContext.RequestAborted);
        var baseMenu = await BuildDefaultNavigationMenuAsync();
        // Captured BEFORE ApplyForManagement substitutes any DisplayText rename, so the
        // editor can show "originally called X" without the layout ever storing X.
        var originalTexts = AdminMenuNodeSummary.CollectOriginalTexts(baseMenu.Items);
        var managedMenu = await iconController.ResolveMenuIconsAsync(baseMenu with
        {
            Items = layoutService.ApplyForManagement(baseMenu.Items, layout),
        }, IconController.AdminMenuChromeIconKeys, HttpContext.RequestAborted);
        var items = managedMenu.Items;
        var separators = layout.Separators
            .Where(separator => !string.IsNullOrWhiteSpace(separator.Key))
            .Select(separator => new AdminMenuSeparatorSummary(
                separator.Key,
                string.IsNullOrWhiteSpace(separator.ParentKey) ? null : separator.ParentKey,
                0,
                separator.Order))
            .ToArray();
        return new AdminMenuSummary(
            AdminMenuLayoutService.DefaultMenuId,
            AdminMenuLayoutService.DefaultMenuName,
            true,
            true,
            separators,
            primaryNavMenuSettings,
            managedMenu.Icons,
            items.Select((item, index) => AdminMenuNodeSummary.From(item, layout, layoutService, null, 0, index, originalTexts)).ToArray());
    }

    // The owning admin menu's name and the node's own stored caption, for the node with this
    // UniqueId - or null when no admin menu owns it (a provider-contributed item). Matched on
    // UniqueId rather than caption, because that is exactly the identity a caption cannot supply
    // once it has been translated or edited.
    private async Task<(string MenuName, string? SourceText)?> FindNodeAsync(string nodeId)
    {
        var list = await adminMenuService.GetAdminMenuListAsync();
        foreach (var menu in list.AdminMenu)
        {
            var found = FindNode(menu.MenuItems, nodeId);
            if (found is not null)
            {
                return (menu.Name, AdminMenuNodeSummary.GetNodeText(found));
            }
        }

        return null;

        static AdminNode? FindNode(IEnumerable<MenuItem> items, string nodeId)
        {
            foreach (var item in items)
            {
                if (item is AdminNode node && string.Equals(node.UniqueId, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }

                var child = FindNode(item.Items, nodeId);
                if (child is not null)
                {
                    return child;
                }
            }

            return null;
        }
    }

    // The menu name and invariant caption of a declared Primary Navigation entry.
    private async Task<(string MenuName, string? SourceText)?> FindEntryAsync(string entryId)
    {
        var entry = (await pageRegistry.GetAsync(HttpContext.RequestAborted)).Find(Grove.Routing.ShellId.Admin, entryId);
        return entry?.Menu is { CaptionIsData: false } menu
            ? (AdminMenuBuilder.TranslationMenuName, menu.Caption)
            : null;
    }

    // The page registry's default tree for this caller, before the tenant's edits.
    private async Task<NavigationMenu> BuildDefaultNavigationMenuAsync()
    {
        await adminSettingsNormalizer.EnsureNewMenuEnabledAsync();
        return await adminMenuBuilder.BuildDefaultAsync(HttpContext);
    }

    private async Task<AdminMenu?> LoadMenuForUpdateAsync(string menuId)
    {
        var list = await adminMenuService.LoadAdminMenuListAsync();
        return adminMenuService.GetAdminMenuById(list, menuId);
    }

    private static AdminMenuSummary WithPlacement(AdminMenuSummary summary, MenuPlacementEntry entry) =>
        WithPlacement(summary, entry.Placement, entry.Enabled);

    private static AdminMenuSummary WithPlacement(AdminMenuSummary summary, IReadOnlyDictionary<string, MenuPlacementEntry> placements) =>
        placements.TryGetValue(summary.Id, out var entry) ? WithPlacement(summary, entry) : summary;

    private static AdminMenuSummary WithPlacement(AdminMenuSummary summary, MenuPlacement placement, bool enabled) =>
        placement == MenuPlacement.Admin ? summary : summary with { Placement = placement, Enabled = enabled };

    private static AdminNode CloneNode(AdminNode node, string menuName)
    {
        var clone = node switch
        {
            LinkAdminNode link => (AdminNode)new LinkAdminNode
            {
                LinkText = link.LinkText,
                LinkUrl = link.LinkUrl,
                IconClass = link.IconClass,
                PermissionNames = link.PermissionNames.ToArray(),
            },
            PlaceholderAdminNode placeholder => (AdminNode)new PlaceholderAdminNode
            {
                LinkText = placeholder.LinkText,
                IconClass = placeholder.IconClass,
                PermissionNames = placeholder.PermissionNames.ToArray(),
            },
            _ => new PlaceholderAdminNode { LinkText = node.Text.Value },
        };

        clone.UniqueId = Guid.NewGuid().ToString("n");
        clone.MenuName = menuName;
        clone.Enabled = node.Enabled;
        clone.Priority = node.Priority;
        clone.Position = node.Position;

        foreach (var child in node.Items.OfType<AdminNode>())
        {
            clone.Items.Add(CloneNode(child, menuName));
        }

        return clone;
    }

    // The request's required members (MVC's implicit [Required] on non-nullable members answered these).
    private static IResult? RequiredNodeMembers(AdminMenuNodeEditModel model)
    {
        var errors = new Dictionary<string, string[]>();
        if (model.Type is null)
        {
            errors["type"] = ["The Type field is required."];
        }

        if (model.Text is null)
        {
            errors["text"] = ["The Text field is required."];
        }

        return errors.Count == 0 ? null : ApiProblems.Validation(errors);
    }

    private static bool IsPlaceholderNodeType(string type) =>
        string.Equals(type, nameof(PlaceholderAdminNode), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "placeholder", StringComparison.OrdinalIgnoreCase);

    private static void SetMenuName(AdminNode node, string menuName)
    {
        node.MenuName = menuName;
        foreach (var child in node.Items.OfType<AdminNode>())
        {
            SetMenuName(child, menuName);
        }
    }

    private static AdminNode? CreateNode(string type) => type switch
    {
        nameof(LinkAdminNode) or "link" => new LinkAdminNode(),
        nameof(PlaceholderAdminNode) or "placeholder" => new PlaceholderAdminNode(),
        _ => null,
    };

    private static void ApplyNode(AdminNode node, AdminMenuNodeEditModel model)
    {
        node.Enabled = model.Enabled;
        node.Priority = model.Priority;
        node.Position = model.DisplayPosition;

        switch (node)
        {
            case LinkAdminNode link:
                link.LinkText = model.Text.Trim();
                link.LinkUrl = model.Url?.Trim() ?? string.Empty;
                link.IconClass = model.IconClass?.Trim() ?? string.Empty;
                link.PermissionNames = NormalizePermissionNames(model.PermissionNames);
                break;
            case PlaceholderAdminNode placeholder:
                placeholder.LinkText = model.Text.Trim();
                placeholder.IconClass = model.IconClass?.Trim() ?? string.Empty;
                placeholder.PermissionNames = NormalizePermissionNames(model.PermissionNames);
                break;
        }
    }

    private static bool MoveNode(AdminMenu menu, AdminNode node, string? parentNodeId, int? requestedPosition)
    {
        if (node.UniqueId == parentNodeId || IsDescendantOf(node, parentNodeId))
        {
            return false;
        }

        if (!TryGetParent(menu, parentNodeId, out var parent))
        {
            return false;
        }

        var oldParent = FindParent(menu, node);
        var oldIndex = GetNodeIndex(menu, oldParent, node);
        var position = requestedPosition ?? GetChildCount(menu, parent);

        if (ReferenceEquals(oldParent, parent) && oldIndex >= 0 && position > oldIndex)
        {
            position--;
        }

        if (!menu.RemoveMenuItem(node))
        {
            return false;
        }

        position = ClampPosition(position, GetChildCount(menu, parent));
        return menu.InsertMenuItemAt(node, parent, position);
    }

    private static bool IsDescendantOf(AdminNode node, string? candidateId)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            return false;
        }

        foreach (var child in node.Items.OfType<AdminNode>())
        {
            if (child.UniqueId == candidateId || IsDescendantOf(child, candidateId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetParent(AdminMenu menu, string? parentNodeId, out AdminNode? parent)
    {
        parent = null;
        if (string.IsNullOrWhiteSpace(parentNodeId))
        {
            return true;
        }

        parent = menu.GetMenuItemById(parentNodeId);
        return parent is not null;
    }

    private static AdminNode? FindParent(AdminMenu menu, AdminNode node)
    {
        if (menu.MenuItems.Contains(node))
        {
            return null;
        }

        foreach (var root in menu.MenuItems.OfType<AdminNode>())
        {
            var parent = FindParent(root, node);
            if (parent is not null)
            {
                return parent;
            }
        }

        return null;
    }

    private static AdminNode? FindParent(AdminNode parent, AdminNode node)
    {
        if (parent.Items.Contains(node))
        {
            return parent;
        }

        foreach (var child in parent.Items.OfType<AdminNode>())
        {
            var found = FindParent(child, node);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static int GetNodeIndex(AdminMenu menu, AdminNode? parent, AdminNode node)
        => parent is null ? menu.MenuItems.IndexOf(node) : parent.Items.IndexOf(node);

    private static int GetChildCount(AdminMenu menu, AdminNode? parent)
        => parent is null ? menu.MenuItems.Count : parent.Items.OfType<AdminNode>().Count();

    private static int ClampPosition(int position, int childCount)
        => Math.Clamp(position, 0, childCount);

    private static string[] NormalizePermissionNames(string[]? permissionNames)
        => permissionNames?.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
}
