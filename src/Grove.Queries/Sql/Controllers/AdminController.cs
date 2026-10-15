using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Admin;
using Grove.Modules;
using Grove.Queries.Sql.ViewModels;
using YesSql;

namespace Grove.Queries.Sql.Controllers;

[Feature("Grove.Queries.Sql")]
public sealed class AdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IStore _store;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    public AdminController(
        IAuthorizationService authorizationService,
        IStore store,
        IServiceProvider serviceProvider,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _store = store;
        _services = serviceProvider;
        S = stringLocalizer;
    }

    [Admin("Queries/Sql/Query", "QueriesRunSql")]
    public Task<IActionResult> Query(string query)
    {
        query = string.IsNullOrWhiteSpace(query)
            ? ""
            : Base64.FromUTF8Base64String(query);

        return Query(new AdminQueryViewModel
        {
            DecodedQuery = query,
            FactoryName = _store.Configuration.ConnectionFactory.GetType().FullName,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Query(AdminQueryViewModel model)
    {
        model.FactoryName = _store.Configuration.ConnectionFactory.GetType().FullName;

        if (!await _authorizationService.AuthorizeAsync(User, QueriesPermissions.ManageSqlQueries))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(model.DecodedQuery))
        {
            return View(model);
        }

        if (string.IsNullOrEmpty(model.Parameters))
        {
            model.Parameters = "{ }";
        }

        var parameters = new Dictionary<string, object>(JConvert.DeserializeObject<Dictionary<string, object>>(model.Parameters) ?? [], StringComparer.OrdinalIgnoreCase);
        var run = await _services.GetRequiredService<SqlQueryRunner>().RunAsync(model.DecodedQuery, parameters, HttpContext.RequestAborted);

        model.RawSql = run.RawSql;
        model.Documents = run.Rows.ToList();
        if (run.RawSql is not null)
        {
            model.Parameters = JConvert.SerializeObject(run.Parameters, JOptions.Indented);
        }

        foreach (var error in run.Errors)
        {
            ModelState.AddModelError(nameof(model.DecodedQuery), error);
        }

        model.Elapsed = run.Elapsed;

        return View(model);
    }
}
