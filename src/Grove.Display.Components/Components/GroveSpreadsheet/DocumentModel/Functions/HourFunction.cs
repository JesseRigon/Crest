#nullable enable

using System;

namespace Grove.Display.Components.Primitives.Documents.Spreadsheet;

class HourFunction : DatePartFunctionBase
{
    public override string Name => "HOUR";

    protected override int GetPart(DateTime dateTime) => dateTime.Hour;
}