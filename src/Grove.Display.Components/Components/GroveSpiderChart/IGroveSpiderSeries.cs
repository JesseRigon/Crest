using Microsoft.AspNetCore.Components;
using System.Collections.Generic;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// Non-generic contract for spider series so <see cref="GroveSpiderChart"/> can be non-generic.
    /// </summary>
    internal interface IGroveSpiderSeries
    {
        int Index { get; set; }
        string Title { get; }
        bool IsVisible { get; set; }
        bool MarkersVisible { get; }
        double MarkerSize { get; }
        double StrokeWidth { get; }
        SpiderSeriesType SeriesType { get; }

        IEnumerable<string> GetCategories();
        IEnumerable<double> GetValues();
        double GetValue(string category);
        object? GetData(string category);
        string FormatValue(double value);

        double MeasureLegend();
        RenderFragment RenderLegendItem();
        void ForceUpdate();
    }
}

