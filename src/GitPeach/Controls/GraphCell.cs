using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GitPeach.Models;

namespace GitPeach.Controls;

public sealed class GraphCell : Control
{
    public const double LaneWidth = 14;
    public const double RowHeight = 24;

    public static readonly StyledProperty<GraphRow?> RowProperty =
        AvaloniaProperty.Register<GraphCell, GraphRow?>(nameof(Row));

    public static readonly StyledProperty<bool> IsWipProperty =
        AvaloniaProperty.Register<GraphCell, bool>(nameof(IsWip));

    static GraphCell()
    {
        AffectsRender<GraphCell>(RowProperty, IsWipProperty);
    }

    public GraphRow? Row { get => GetValue(RowProperty); set => SetValue(RowProperty, value); }
    public bool IsWip { get => GetValue(IsWipProperty); set => SetValue(IsWipProperty, value); }

    private static double X(int lane) => lane * LaneWidth + LaneWidth / 2;

    public override void Render(DrawingContext ctx)
    {
        double h = Bounds.Height;
        double mid = h / 2;

        if (IsWip)
        {
            var pen = new Pen(Palette.Dim, 1.5, DashStyle.Dash);
            ctx.DrawEllipse(Palette.Panel, pen, new Point(X(0), mid), 4.5, 4.5);
            return;
        }
        var row = Row;
        if (row is null) return;

        foreach (var s in row.Segments)
        {
            var pen = new Pen(new SolidColorBrush(Palette.LaneColors[s.Color % Palette.LaneColors.Length]), 2);
            double x1 = X(s.From), x2 = X(s.To);
            if (s.Top)
            {
                // top half: from row top (lane From) to the node level (lane To)
                DrawLink(ctx, pen, new Point(x1, 0), new Point(x2, mid));
            }
            else
            {
                DrawLink(ctx, pen, new Point(x1, mid), new Point(x2, h));
            }
        }

        var nodeBrush = new SolidColorBrush(Palette.LaneColors[row.NodeColor % Palette.LaneColors.Length]);
        var c = new Point(X(row.NodeLane), mid);
        if (row.IsMerge)
        {
            ctx.DrawEllipse(Palette.Bg, new Pen(nodeBrush, 2), c, 4, 4);
        }
        else
        {
            ctx.DrawEllipse(nodeBrush, new Pen(Palette.Bg, 1.5), c, 4.5, 4.5);
        }
    }

    private static void DrawLink(DrawingContext ctx, Pen pen, Point a, Point b)
    {
        if (Math.Abs(a.X - b.X) < 0.1)
        {
            ctx.DrawLine(pen, a, b);
            return;
        }
        var g = new StreamGeometry();
        using (var sg = g.Open())
        {
            sg.BeginFigure(a, false);
            double my = (a.Y + b.Y) / 2;
            sg.CubicBezierTo(new Point(a.X, my), new Point(b.X, my), b);
            sg.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, g);
    }
}
