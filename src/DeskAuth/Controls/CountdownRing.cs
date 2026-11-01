// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Windows;
using System.Windows.Media;

namespace DeskAuth.Controls;

/// <summary>
/// Pie showing the time left for a code
/// </summary>
internal sealed class CountdownRing : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(CountdownRing),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(CountdownRing),
        new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Fraction remaining, 0 to 1
    /// </summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double radius = Math.Min(ActualWidth, ActualHeight) / 2 - 1;
        if (radius <= 0)
        {
            return;
        }
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var pen = new Pen(Fill, 1.5);
        dc.DrawEllipse(null, pen, center, radius, radius);

        double value = Math.Clamp(Value, 0, 1);
        if (value >= 0.999)
        {
            dc.DrawEllipse(Fill, null, center, radius, radius);
            return;
        }
        if (value <= 0)
        {
            return;
        }

        // pie from 12 o'clock, clockwise
        double angle = value * 2 * Math.PI;
        var start = new Point(center.X, center.Y - radius);
        var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = center, IsClosed = true };
        figure.Segments.Add(new LineSegment(start, false));
        figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, angle > Math.PI, SweepDirection.Clockwise, false));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        dc.DrawGeometry(Fill, null, geometry);
    }
}
