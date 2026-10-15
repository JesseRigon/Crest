#nullable enable

using System;

namespace Grove.Components.Primitives.Documents.Spreadsheet;

class MonthFunction : DatePartFunctionBase
{
    public override string Name => "MONTH";

    protected override int GetPart(DateTime dateTime) => dateTime.Month;
}