using System.Globalization;
using System.Linq;

namespace NordicBeesERP.Helpers;

public static class ChartMathHelper
{
    // Exact port of the mockup's curve(): smooth cubic-bezier path through N points.
    public static string BuildSmoothPath(decimal[] values, double w, double h, decimal max)
    {
        var n = values.Length;
        if (max <= 0m) max = 1m;
        var pts = new (double x, double y)[n];
        for (var i = 0; i < n; i++)
        {
            pts[i] = (i * (w / (n - 1)), h - ((double)(values[i] / max)) * (h - 12));
        }
        var inv = CultureInfo.InvariantCulture;
        var d = $"M{pts[0].x.ToString("0.0", inv)} {pts[0].y.ToString("0.0", inv)}";
        for (var i = 1; i < n; i++)
        {
            var p = pts[i - 1];
            var c = pts[i];
            var mx = (p.x + c.x) / 2;
            d += $" C{mx.ToString("0.0", inv)} {p.y.ToString("0.0", inv)} {mx.ToString("0.0", inv)} {c.y.ToString("0.0", inv)} {c.x.ToString("0.0", inv)} {c.y.ToString("0.0", inv)}";
        }
        return d;
    }

    // Straight-line sparkline path — exact port of the mockup's spark() function.
    // Per-series min/max normalized. Used by the 4 KPI cards.
    public static (string line, string area) BuildSparkPath(decimal[] values, double w, double h)
    {
        var n = values.Length;
        var inv = CultureInfo.InvariantCulture;
        var min = values.Min();
        var max = values.Max();
        var sp = (double)(max - min);
        if (sp == 0) sp = 1;
        var pts = new (double x, double y)[n];
        for (var i = 0; i < n; i++)
        {
            pts[i] = (i * (w / (n - 1)), h - 4 - ((double)(values[i] - min) / sp) * (h - 12));
        }
        var line = "M" + pts[0].x.ToString("0.0", inv) + " " + pts[0].y.ToString("0.0", inv);
        for (var i = 1; i < n; i++)
        {
            line += " L" + pts[i].x.ToString("0.0", inv) + " " + pts[i].y.ToString("0.0", inv);
        }
        var area = line + " L" + w.ToString("0.0", inv) + " " + h.ToString("0.0", inv) + " L0 " + h.ToString("0.0", inv) + " Z";
        return (line, area);
    }
}
