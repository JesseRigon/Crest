#nullable enable

using System;

namespace Grove.Components.Primitives.Documents.Spreadsheet;

class SecondFunction : DatePartFunctionBase
{
    public override string Name => "SECOND";

    protected override int GetPart(DateTime dateTime) => dateTime.Second;
}