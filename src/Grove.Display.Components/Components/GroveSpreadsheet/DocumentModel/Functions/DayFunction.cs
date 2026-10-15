#nullable enable

using System;

namespace Grove.Display.Components.Primitives.Documents.Spreadsheet;

class DayFunction : DatePartFunctionBase
{
    public override string Name => "DAY";

    protected override int GetPart(DateTime dateTime) => dateTime.Day;
}