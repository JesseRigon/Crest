using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Data.Connectors;
using Grove.Data.Queries;
using Grove.Data.Queries.Connections;
using Grove.Data.Queries.Sql;
using Grove.Data.Queries.Sql.Models;
using Grove.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Grove.Connectors.Consumers.Tests;

public sealed class QueryConnectionSourceTests
{
    private readonly FakeConnectorEngine _engine = new();
    private readonly FakeCallerAccessor _callers = new() { Current = Callers.Staff };
    private readonly FakeCallerFactory _factory = new();

    [Fact]
    public async Task ConnectionSourceRunsTheOperationAsTheStaffCallerAndTypesTheRows()
    {
        _engine.Answer = _ => FakeConnectorEngine.Rows(JsonNode.Parse("""[{"id":1,"name":"Ada","active":true,"since":"2024-01-02T03:04:05Z"},{"id":2,"name":null,"score":1.5}]""")!, nextPage: "cursor-2");
        var source = new ConnectionQuerySource(Services());
        var query = ConnectionQuery("crm", "listContacts", new JsonObject { ["status"] = "open", ["limit"] = 5 });

        var results = await source.ExecuteQueryAsync(query, new QueryRequest
        {
            Parameters = new Dictionary<string, object> { ["status"] = "closed" },
            PageToken = "cursor-1",
            CancellationToken = TestContext.Current.CancellationToken,
        });

        var call = Assert.Single(_engine.Calls);
        Assert.Equal("crm", call.ConnectionKey);
        Assert.Equal("listContacts", call.OperationId);
        Assert.Same(Callers.Staff, call.Caller);
        Assert.Null(call.Scope);
        Assert.Equal("cursor-1", call.Page);
        Assert.Equal("closed", call.Arguments!["status"]!.GetValue<string>());
        Assert.Equal(5, call.Arguments!["limit"]!.GetValue<int>());

        Assert.Equal(2, results.Items.Count());
        Assert.Equal("cursor-2", results.NextPageToken);
        Assert.Null(results.Total);
        Assert.Equal(
            [("id", typeof(long)), ("name", typeof(string)), ("active", typeof(bool)), ("since", typeof(DateTime)), ("score", typeof(double))],
            results.Columns.Select(column => (column.Name, column.Type)).ToArray());
    }

    [Fact]
    public async Task ConnectionSourceReadsRowsFromAnEnvelopeOrAPath()
    {
        _engine.Answer = _ => FakeConnectorEngine.Rows(JsonNode.Parse("""{"data":[{"a":1}],"meta":{"page":{"items":[{"b":2},{"b":3}]}}}""")!);
        var source = new ConnectionQuerySource(Services());

        var enveloped = await source.ExecuteQueryAsync(ConnectionQuery("api", "list"), new QueryRequest());
        Assert.Equal(1, enveloped.Total);
        Assert.Equal("a", Assert.Single(enveloped.Columns).Name);

        var pathed = ConnectionQuery("api", "list");
        pathed.Alter<ConnectionQueryMetadata>(metadata => metadata.RowsPath = "meta.page.items");
        var results = await source.ExecuteQueryAsync(pathed, new QueryRequest());
        Assert.Equal(2, results.Total);
        Assert.Equal("b", Assert.Single(results.Columns).Name);
    }

    [Fact]
    public async Task ConnectionSourceSendsThePageSizeInTheOperationsSizeParameter()
    {
        _engine.Answer = _ => FakeConnectorEngine.Rows(new JsonArray());
        var registry = Substitute.For<IConnectionRegistry>();
        registry.FindOperationAsync("gh", "repos", Arg.Any<CancellationToken>()).Returns(new ConnectionOperation
        {
            Id = "repos",
            Paging = new OperationPaging(PagingStyle.Link, "page", "per_page"),
        });
        var source = new ConnectionQuerySource(Services(registry));

        await source.ExecuteQueryAsync(ConnectionQuery("gh", "repos"), new QueryRequest { PageSize = 25 });

        Assert.Equal(25, Assert.Single(_engine.Calls).Arguments!["per_page"]!.GetValue<int>());
    }

    [Fact]
    public async Task AMemberRunCallsOnTheTenantCredentialAsTheSystemActor()
    {
        _callers.Current = Callers.Member;
        _engine.Answer = _ => FakeConnectorEngine.Rows(new JsonArray());
        var source = new ConnectionQuerySource(Services());

        await source.ExecuteQueryAsync(ConnectionQuery("crm", "list"), new QueryRequest());

        var call = Assert.Single(_engine.Calls);
        Assert.Same(FakeCallerFactory.System, call.Caller);
        Assert.Equal(CredentialScope.Tenant, call.Scope);
    }

    [Fact]
    public async Task AFailedCallIsAnErrorNotAnEmptyResult()
    {
        _engine.Answer = _ => new ConnectorResult { Outcome = ConnectorOutcome.NotAuthorized, Error = "Connect your account." };
        var source = new ConnectionQuerySource(Services());

        var exception = await Assert.ThrowsAsync<ConnectionQueryException>(() => source.ExecuteQueryAsync(ConnectionQuery("crm", "list"), new QueryRequest()));

        Assert.Equal(ConnectorOutcome.NotAuthorized, exception.Result!.Outcome);
    }

    [Fact]
    public async Task SqlSourceRunsAnExternalDatabaseThroughItsConnectionAndPages()
    {
        _engine.Answer = _ => FakeConnectorEngine.Rows(new JsonArray([.. Enumerable.Range(1, 5).Select(i => (JsonNode)new JsonObject { ["Id"] = i })]));
        var source = SqlSource();
        var query = SqlQuery("SELECT Id FROM Orders WHERE Status = @status", "warehouse");

        var first = await source.ExecuteQueryAsync(query, new QueryRequest { Parameters = new Dictionary<string, object> { ["status"] = "open" }, PageSize = 2 });

        var call = Assert.Single(_engine.Calls);
        Assert.Equal("warehouse", call.ConnectionKey);
        Assert.Null(call.OperationId);
        Assert.Equal(SqlQuerySource.ExternalQueryMethod, call.Request!.Method);
        Assert.Equal("SELECT Id FROM Orders WHERE Status = @status", call.Request.Body);
        Assert.Equal("open", call.Arguments!["status"]!.GetValue<string>());
        Assert.Equal(5, first.Total);
        Assert.Equal(2, first.Items.Count());
        Assert.NotNull(first.NextPageToken);

        var last = await source.ExecuteQueryAsync(query, new QueryRequest { PageSize = 2, PageToken = PageTokens(4) });
        Assert.Single(last.Items);
        Assert.Null(last.NextPageToken);
    }

    [Fact]
    public async Task SqlSourceRefusesAnythingButASelectBeforeCallingTheConnection()
    {
        var source = SqlSource();

        await Assert.ThrowsAsync<SqlQueryException>(() => source.ExecuteQueryAsync(SqlQuery("DELETE FROM Orders", "warehouse"), new QueryRequest()));
        await Assert.ThrowsAsync<SqlQueryException>(() => source.ExecuteQueryAsync(SqlQuery("SELECT * FROM T WHERE a = '{{ x }}'", "warehouse"), new QueryRequest()));

        var items = SqlQuery("SELECT DocumentId FROM Orders", "warehouse");
        items.ReturnContentItems = true;
        await Assert.ThrowsAsync<SqlQueryException>(() => source.ExecuteQueryAsync(items, new QueryRequest()));

        Assert.Empty(_engine.Calls);
    }

    private static string PageTokens(int offset) => Grove.Data.Queries.Structured.PageTokens.Encode(offset);

    private SqlQuerySource SqlSource() => new(null!, null!, Services(), NullLogger<SqlQuerySource>.Instance);

    private static Query SqlQuery(string template, string connection)
    {
        var query = new Query { Name = "q", Source = SqlQuerySource.SourceName };
        query.Put(new SqlQueryMetadata { Template = template, Connection = connection });
        return query;
    }

    private static Query ConnectionQuery(string connection, string operation, JsonObject? arguments = null)
    {
        var query = new Query { Name = "q", Source = ConnectionQuerySource.SourceName };
        query.Put(new ConnectionQueryMetadata { Connection = connection, Operation = operation, Arguments = arguments });
        return query;
    }

    private ServiceProvider Services(IConnectionRegistry? registry = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<IConnectorEngine>(_engine)
            .AddSingleton<ICallerContextAccessor>(_callers)
            .AddSingleton<ICallerContextFactory>(_factory);

        if (registry is not null)
        {
            services.AddSingleton(registry);
        }

        return services.BuildServiceProvider();
    }
}
