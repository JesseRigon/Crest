using GraphQL.Types;
using Crest.Demo.Models;

namespace Crest.Demo.GraphQL;

public class TestQueryObjectType : ObjectGraphType<TestContentPartA>
{
    public TestQueryObjectType()
    {
        Name = "TestContentPartA";

        Field("line", x => x.Line, true);
        Field("lineIgnored", x => x.Line, true);
        Field("lineOtherIgnored", x => x.Line, true);
    }
}
