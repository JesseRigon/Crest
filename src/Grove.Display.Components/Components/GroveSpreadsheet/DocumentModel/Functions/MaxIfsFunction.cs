#nullable enable

using System.Collections.Generic;

namespace Grove.Display.Components.Primitives.Documents.Spreadsheet;

class MaxIfsFunction : ConditionalAggregateFunctionBase
{
    public override string Name => "MAXIFS";

    protected override CellData Aggregate(List<double> values) => AggregationMethods.Max(values);
}
