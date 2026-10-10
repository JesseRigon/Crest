using Crest.Access;
using Crest.Data.Scoping;

namespace Crest.Tests.Access;

public class ScopeTests
{
    private sealed class Row
    {
        public string ContentType { get; set; }

        public string Owner { get; set; }

        public string ContentItemId { get; set; }

        public bool Latest { get; set; }
    }

    private static Func<string, object> Columns(string type, string owner, string id = "i1") => column => column switch
    {
        "ContentType" => type,
        "Owner" => owner,
        "ContentItemId" => id,
        _ => null,
    };

    [Fact]
    public void AllAdmitsAndNoneRefuses()
    {
        Assert.True(ScopeRule.All.Admits(Columns("Page", "u1")));
        Assert.False(ScopeRule.None.Admits(Columns("Page", "u1")));
    }

    [Fact]
    public void AndNarrows()
    {
        var any = ScopeRule.Where(ScopeFilter.Of(ScopeCondition.In("ContentType", ["Page"])));
        var own = ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal("Owner", "u1")));

        var rule = any.And(own);

        Assert.True(rule.Admits(Columns("Page", "u1")));
        Assert.False(rule.Admits(Columns("Page", "u2")));
        Assert.False(rule.Admits(Columns("Post", "u1")));
        Assert.Same(ScopeRule.None, any.And(ScopeRule.None));
        Assert.Same(any, any.And(ScopeRule.All));
    }

    [Fact]
    public void NotInIsThePassThrough()
    {
        var rule = ScopeRule.Where(ScopeFilter.Of(ScopeCondition.NotIn("ContentType", ["Invoice"])));

        Assert.True(rule.Admits(Columns("Page", "u1")));
        Assert.False(rule.Admits(Columns("Invoice", "u1")));
    }

    [Fact]
    public void CompilesToAnIndexPredicate()
    {
        // YesSql's IsIn/IsNotIn are markers its SQL translator rewrites, so the expression is
        // checked structurally; the in-memory semantics are covered by ScopeRule.Admits.
        var rule = ScopeRule.Where(ScopeFilter.AnyOf(
            ScopeFilter.Of(ScopeCondition.In("ContentType", ["Page"])),
            ScopeFilter.AllOf(
                ScopeFilter.Of(ScopeCondition.In("ContentType", ["Post"])),
                ScopeFilter.Of(ScopeCondition.Equal("Owner", "u1")))));

        var text = ScopeExpressions.ToPredicate<Row>(rule).ToString();

        Assert.Contains("Convert(index.ContentType, Object).IsIn(", text);
        Assert.Contains("OrElse", text);
        Assert.Contains("AndAlso", text);
        Assert.Contains("index.Owner == \"u1\"", text);
    }

    [Fact]
    public void CompilesBooleanAndNegatedConditions()
    {
        var rule = ScopeRule.Where(ScopeFilter.AllOf(
            ScopeFilter.Of(ScopeCondition.Equal("Latest", true)),
            ScopeFilter.Of(ScopeCondition.NotIn("ContentType", ["Invoice"]))));

        var text = ScopeExpressions.ToPredicate<Row>(rule).ToString();

        Assert.Contains("index.Latest == True", text);
        Assert.Contains("Convert(index.ContentType, Object).IsNotIn(", text);
    }

    [Fact]
    public void AllAndNoneCompileToConstants()
    {
        Assert.True(ScopeExpressions.ToPredicate<Row>(ScopeRule.All).Compile()(new Row()));
        Assert.False(ScopeExpressions.ToPredicate<Row>(ScopeRule.None).Compile()(new Row()));
    }

    [Fact]
    public void MissingTargetIsRefused()
    {
        var set = new ScopeSet("sig", new Dictionary<string, ScopeRule>(StringComparer.OrdinalIgnoreCase));

        Assert.Throws<ScopeRefusedException>(() => set.Require("Document"));
    }

    [Fact]
    public void ScopeSignatureIgnoresRoleOrderAndChangesWithVersion()
    {
        var a = new CallerContext { Tenant = "t", Side = CallerSide.Admin, Roles = new HashSet<string> { "A", "B" }, PermissionVersion = 1 };
        var b = new CallerContext { Tenant = "t", Side = CallerSide.Admin, Roles = new HashSet<string> { "B", "A" }, PermissionVersion = 1 };
        var c = new CallerContext { Tenant = "t", Side = CallerSide.Admin, Roles = new HashSet<string> { "A", "B" }, PermissionVersion = 2 };

        Assert.Equal(a.ScopeSignature, b.ScopeSignature);
        Assert.NotEqual(a.ScopeSignature, c.ScopeSignature);
    }
}
