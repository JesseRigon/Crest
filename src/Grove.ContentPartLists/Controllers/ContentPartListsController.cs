using Grove.Models;
using Grove.Permissions;
using Grove.Services;
using Grove.ContentTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Grove.Controllers;

// The management API behind Content Part Lists. The SCREEN lives in this module's
// blazor-wasm project; consuming modules only declare and consume sets, they never own
// these editors. The provider-generic picker endpoints (api/grove/option-sources/*)
// live in Grove.Server with the IOptionSourceProvider abstraction itself.
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/grove/content-part-lists")]
public sealed class ContentPartListsController(
    IContentPartListService contentPartLists,
    IMeasurementPreferenceService measurementPreferences,
    IAuthorizationService authorizationService,
    DefinitionLockGuard locks,
    IContentPartListImporter importer) : ControllerBase
{
    /// <summary>
    /// The culture-to-units dataset: which measurement system the culture uses and
    /// the preferred global.uom unit key per dimension. Defaults to the request's
    /// resolved culture; pass ?culture= to ask for another. The seam localization or
    /// tenant settings can later adapt.
    /// </summary>
    [HttpGet("uom/preferences")]
    public async Task<ActionResult<MeasurementPreferences>> GetUomPreferencesAsync([FromQuery] string? culture = null)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(measurementPreferences.Resolve(culture));
    }

    [HttpGet]
    public async Task<ActionResult<GroveContentPartListModel[]>> ListAsync()
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok((await contentPartLists.ListAsync(HttpContext.RequestAborted)).ToArray());
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<GroveContentPartListModel>> GetAsync(string key)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var list = await contentPartLists.GetAsync(key, HttpContext.RequestAborted);
        return list is null ? NotFound() : Ok(list);
    }

    /// <summary>
    /// Browses a list arranged in levels: the selectable options directly under
    /// <paramref name="parent"/> (the top level when omitted), each with whether it has
    /// options below it. Searching the whole hierarchy is the picker's search over the
    /// <c>Path</c> column.
    /// </summary>
    [HttpGet("{key}/children")]
    public async Task<ActionResult<OptionNode[]>> GetChildrenAsync(string key, [FromQuery] string? parent = null)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var options = await contentPartLists.GetSelectableAsync(key, HttpContext.RequestAborted);
        return Ok(ContentPartListRules.Children(options, parent).ToArray());
    }

    /// <summary>The options an editor should offer: ordered, hidden ones dropped.</summary>
    [HttpGet("{key}/selectable")]
    public async Task<ActionResult<GroveOptionModel[]>> GetSelectableAsync(string key)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok((await contentPartLists.GetSelectableAsync(key, HttpContext.RequestAborted)).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<GroveContentPartListModel>> CreateAsync([FromBody] CreateContentPartListRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        try
        {
            return Ok(await contentPartLists.CreateListAsync(request.Key, request.DisplayText, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{key}/options")]
    public async Task<ActionResult<GroveOptionModel>> AddOptionAsync(string key, [FromBody] AddOptionRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var list = await contentPartLists.GetAsync(key, HttpContext.RequestAborted);
        if (list is not null)
        {
            if (ContentPartListRules.LockActive(list.EditLock))
            {
                return LockedProblem("Options cannot be added while the list is locked for editing.");
            }

            // Adds stay allowed under a data lock, but the category vocabulary is
            // frozen: logic keyed on the category set must cover tenant additions.
            var categoryValidation = ContentPartListRules.ValidateAddedCategory(list, request.Category);
            if (!categoryValidation.IsValid)
            {
                return LockedProblem(categoryValidation.Error!);
            }
        }

        try
        {
            return Ok(await contentPartLists.AddOptionAsync(key, request.Key, request.DisplayText, request.Position, request.Category, request.DisplayTextPlural, request.Value, request.Fields, request.Parent, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Imports options by key (<see cref="IContentPartListImporter"/>): new keys are added
    /// with the row's label, existing ones take the row's data and keep the tenant's label.
    /// Refused while the list is locked for editing.</summary>
    [HttpPost("{key}/import")]
    public async Task<ActionResult<ContentPartListImportResult>> ImportAsync(string key, [FromBody] ImportOptionsRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var list = await contentPartLists.GetAsync(key, HttpContext.RequestAborted);
        if (list is null)
        {
            return NotFound();
        }

        if (ContentPartListRules.LockActive(list.EditLock))
        {
            return LockedProblem("Options cannot be imported while the list is locked for editing.");
        }

        var rows = (request.Rows ?? []).Select(row => new ContentPartListImportRow(row.Key, row.DisplayText, row.Category, row.Value, row.Fields, row.Parent));
        return Ok(await importer.ImportAsync(key, rows, cancellationToken: HttpContext.RequestAborted));
    }

    /// <summary>Rewrites the list's manual order: Position becomes the index in the
    /// posted key array, which must name every option exactly once. Reordering is
    /// display-level, so a data lock permits it; an edit lock does not.</summary>
    [HttpPut("{key}/order")]
    public async Task<ActionResult<GroveContentPartListModel>> ReorderAsync(string key, [FromBody] ReorderOptionsRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        if (await IsEditLockedAsync(key))
        {
            return LockedProblem("The list is locked for editing.");
        }

        try
        {
            return Ok(await contentPartLists.ReorderOptionsAsync(key, request.Keys ?? [], HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Places or lifts tenant-authority locks on a list. Module-placed
    /// locks are refused in both directions. Super admin only.</summary>
    [HttpPut("{key}/locks")]
    public async Task<ActionResult<GroveContentPartListModel>> UpdateLocksAsync(string key, [FromBody] UpdateContentPartListLocksRequest request)
    {
        if (!await IsAuthorizedAsync(ContentPartListPermissions.LockContentPartLists))
        {
            return Forbid();
        }

        try
        {
            return Ok(await contentPartLists.UpdateListLocksAsync(key, request.DataLock, request.EditLock, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Deletes a tenant-owned list. Module-seeded lists are refused - their
    /// keys are the owning module's contract, so they can only be hidden.</summary>
    [HttpDelete("{key}")]
    public async Task<IActionResult> DeleteAsync(string key)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        if (await IsEditLockedAsync(key))
        {
            return LockedProblem("The list is locked for editing and cannot be deleted.");
        }

        try
        {
            await contentPartLists.DeleteListAsync(key, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // The technical key is deliberately not updatable - it is the contract module
    // code matches on. Tenants relabel, reorder and hide instead.
    [HttpPut("{key}/options/{optionKey}")]
    public async Task<ActionResult<GroveOptionModel>> UpdateOptionAsync(string key, string optionKey, [FromBody] UpdateOptionRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var list = await contentPartLists.GetAsync(key, HttpContext.RequestAborted);
        if (list is not null)
        {
            if (ContentPartListRules.LockActive(list.EditLock))
            {
                return LockedProblem("The list is locked for editing.");
            }

            // The data lock freezes the MACHINE surface - category assignments and
            // machine values; relabel/hide/reposition are display-level and stay open.
            if (request.Category is not null && ContentPartListRules.LockActive(list.DataLock))
            {
                return LockedProblem("This list's categories are locked and cannot be reassigned.");
            }

            if (request.Value is not null && ContentPartListRules.LockActive(list.DataLock))
            {
                return LockedProblem("This list's machine values are locked and cannot be changed.");
            }

            // An option's level is machine surface too: code may roll up by ancestor.
            if (request.Parent is not null && ContentPartListRules.LockActive(list.DataLock))
            {
                return LockedProblem("This list's levels are locked; an option cannot be moved under another.");
            }

            // Custom fields freeze per the admin's designation: a DataLocked field is
            // machine surface like Category/Value, the rest stay display surface.
            if (request.Fields is { Count: > 0 } && ContentPartListRules.LockActive(list.DataLock))
            {
                var lockedField = (list.Fields ?? [])
                    .FirstOrDefault(field => field.DataLocked && request.Fields.Keys.Any(name =>
                        string.Equals(name, field.Name, StringComparison.OrdinalIgnoreCase)));
                if (lockedField is not null)
                {
                    return LockedProblem($"The field '{lockedField.DisplayName}' is machine data on a locked list and cannot be changed.");
                }
            }
        }

        try
        {
            return Ok(await contentPartLists.UpdateOptionAsync(key, optionKey, request.DisplayText, request.Position, request.Hidden, request.Category, request.DisplayTextPlural, request.Value, request.Fields, request.Parent, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Sets a custom field's lock designation - whether it freezes with the
    /// list's data lock (machine surface) or stays editable (display surface). The
    /// designation is itself machine surface: while either lock is active it cannot
    /// be changed, in either direction.</summary>
    [HttpPut("{key}/fields/{fieldName}")]
    public async Task<ActionResult<GroveContentPartListModel>> UpdateOptionFieldAsync(string key, string fieldName, [FromBody] UpdateOptionFieldRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var list = await contentPartLists.GetAsync(key, HttpContext.RequestAborted);
        if (list is not null)
        {
            if (ContentPartListRules.LockActive(list.EditLock))
            {
                return LockedProblem("The list is locked for editing.");
            }

            if (ContentPartListRules.LockActive(list.DataLock))
            {
                return LockedProblem("This list's data is locked; field lock designations cannot be changed.");
            }
        }

        try
        {
            return Ok(await contentPartLists.SetOptionFieldLockAsync(key, fieldName, request.DataLocked, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>The content types eligible as a list's option type (they carry
    /// GroveOptionPart). Drives the option-type dropdown on the management page.</summary>
    [HttpGet("option-types")]
    public async Task<ActionResult<IReadOnlyList<string>>> ListOptionContentTypesAsync()
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        return Ok(await contentPartLists.GetOptionContentTypesAsync(HttpContext.RequestAborted));
    }

    /// <summary>Points the list at a dedicated option content type. Machine surface:
    /// refused while either lock is active, and refused by the service when the list
    /// already holds options of another type.</summary>
    [HttpPut("{key}/option-content-type")]
    public async Task<ActionResult<GroveContentPartListModel>> UpdateOptionContentTypeAsync(string key, [FromBody] UpdateOptionContentTypeRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var list = await contentPartLists.GetAsync(key, HttpContext.RequestAborted);
        if (list is not null)
        {
            if (ContentPartListRules.LockActive(list.EditLock))
            {
                return LockedProblem("The list is locked for editing.");
            }

            if (ContentPartListRules.LockActive(list.DataLock))
            {
                return LockedProblem("This list's data is locked; the option type cannot be changed.");
            }
        }

        try
        {
            return Ok(await contentPartLists.SetOptionContentTypeAsync(key, request.ContentType, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{key}/attach")]
    public async Task<IActionResult> AttachAsync(string key, [FromBody] AttachContentPartListRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        // Attaching converts an existing field of another type: a locked one stays what it is
        // (409 via the definition-lock filter).
        await locks.EnsureFieldChangeAsync(request.ContentType, request.FieldName, DefinitionChanges.Retyped);

        try
        {
            await contentPartLists.AttachToContentTypeAsync(key, request.ContentType, request.FieldName, request.DisplayName, cancellationToken: HttpContext.RequestAborted);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private async Task<bool> IsEditLockedAsync(string key)
    {
        var list = await contentPartLists.GetAsync(key, HttpContext.RequestAborted);
        return list is not null && ContentPartListRules.LockActive(list.EditLock);
    }

    private ObjectResult LockedProblem(string message) =>
        Problem(message, statusCode: StatusCodes.Status409Conflict);

    private Task<bool> CanViewAsync() => IsAuthorizedAsync(ContentPartListPermissions.ViewContentPartLists);

    private Task<bool> CanManageAsync() => IsAuthorizedAsync(ContentPartListPermissions.ManageContentPartLists);

    private async Task<bool> IsAuthorizedAsync(Grove.Security.Permissions.Permission permission) =>
        await authorizationService.AuthorizeAsync(User, permission);
}

public sealed record CreateContentPartListRequest(string Key, string DisplayText);

/// <summary>Rows to import; see <see cref="IContentPartListImporter"/>.</summary>
public sealed record ImportOptionsRequest(IReadOnlyList<AddOptionRequest>? Rows);

public sealed record AddOptionRequest(string Key, string DisplayText, int Position = 0, string? Category = null, string? DisplayTextPlural = null, string? Value = null, Dictionary<string, string?>? Fields = null, string? Parent = null);

/// <summary>Null leaves a value unchanged; a BLANK DisplayTextPlural or Value clears
/// it (plural falls back to the singular; a cleared Value means the key is the
/// option's only machine datum). Fields carries custom data field edits by field
/// name, with the same per-entry contract: absent = untouched, blank = cleared.</summary>
public sealed record UpdateOptionRequest(string? DisplayText, int? Position, bool? Hidden, string? Category = null, string? DisplayTextPlural = null, string? Value = null, Dictionary<string, string?>? Fields = null, string? Parent = null);

/// <summary>The per-field lock designation: true freezes the field's values under
/// the list's data lock, false keeps them display-editable.</summary>
public sealed record UpdateOptionFieldRequest(bool DataLocked);

/// <summary>Option-content-type change payload.</summary>
public sealed record UpdateOptionContentTypeRequest(string ContentType);

/// <summary>Every option key in the desired manual order.</summary>
public sealed record ReorderOptionsRequest(IReadOnlyList<string>? Keys);

/// <summary>Tenant-authority lock changes; null leaves a lock as it is.</summary>
public sealed record UpdateContentPartListLocksRequest(bool? DataLock, bool? EditLock);

public sealed record AttachContentPartListRequest(string ContentType, string FieldName, string? DisplayName);
