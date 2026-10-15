#nullable enable

using System;

namespace Grove.Display.Components.Primitives.Documents.Spreadsheet;

class MinuteFunction : DatePartFunctionBase
{
    public override string Name => "MINUTE";

    protected override int GetPart(DateTime dateTime) => dateTime.Minute;
}