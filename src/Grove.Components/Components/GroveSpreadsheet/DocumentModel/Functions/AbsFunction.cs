#nullable enable

using System;

namespace Grove.Components.Primitives.Documents.Spreadsheet;

class AbsFunction : UnaryMathFunctionBase
{
    public override string Name => "ABS";

    protected override CellData Compute(double number) => CellData.FromNumber(Math.Abs(number));
}
