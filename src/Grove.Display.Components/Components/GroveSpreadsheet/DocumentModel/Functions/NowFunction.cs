#nullable enable

using System;

namespace Grove.Display.Components.Primitives.Documents.Spreadsheet;

class NowFunction : FormulaFunction
{
    public override string Name => "NOW";

    public override FunctionParameter[] Parameters => [];

    public override CellData Evaluate(FunctionArguments arguments)
    {
        return CellData.FromDate(DateTime.Now);
    }
}