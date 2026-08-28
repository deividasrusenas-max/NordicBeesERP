using System.Globalization;

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
}
