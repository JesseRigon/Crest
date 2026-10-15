#nullable enable

using System.Collections.Generic;

namespace Grove.Components.Primitives.Documents.Spreadsheet;

class AverageIfsFunction : ConditionalAggregateFunctionBase
{
    public override string Name => "AVERAGEIFS";

    protected override CellData Aggregate(List<double> values) => AggregationMethods.Average(values);
}
