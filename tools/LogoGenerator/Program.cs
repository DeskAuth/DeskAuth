// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Draws the DeskAuth logo as vector shapes and renders it at every icon size.
//
// usage: dotnet run --project tools/LogoGenerator -- <output folder>
// Copy deskauth.ico to src/DeskAuth/Resources/deskauth.ico and logo256.png to src/DeskAuth/Resources/DeskAuthLogo.png.
string outDir = args[0];
Directory.CreateDirectory(outDir);
var t = new Thread(() =>
{
    int[] sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256, 512];
    var frames = new List<(int Size, byte[] Png)>();
    foreach (int size in sizes)
    {
        var image = Render(size);
        Save(image, Path.Combine(outDir, $"logo{size}.png"));
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(image));
        using var ms = new MemoryStream();
        enc.Save(ms);
        frames.Add((size, ms.ToArray()));
    }
    WriteIco(Path.Combine(outDir, "deskauth.ico"), frames.Where(f => f.Size <= 256).ToList());

});
t.SetApartmentState(ApartmentState.STA);
t.Start();
t.Join();

// ICO with PNG-compressed images, supported since Windows Vista
static void WriteIco(string path, List<(int Size, byte[] Png)> frames)
{
    using var fs = File.Create(path);
    using var w = new BinaryWriter(fs);
    w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)frames.Count);
    int offset = 6 + 16 * frames.Count;
    foreach (var (size, png) in frames)
    {
        w.Write((byte)(size >= 256 ? 0 : size)); w.Write((byte)(size >= 256 ? 0 : size));
        w.Write((byte)0); w.Write((byte)0);
        w.Write((ushort)1); w.Write((ushort)32);
        w.Write(png.Length); w.Write(offset);
        offset += png.Length;
    }
    foreach (var (_, png) in frames) w.Write(png);
}

static BitmapSource Render(int size)
{
    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        dc.PushTransform(new ScaleTransform(size / 512.0, size / 512.0));
        Logo.Draw(dc);
    }
    var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bmp.Render(visual);
    return bmp;
}

static void Save(BitmapSource image, string path)
{
    var enc = new PngBitmapEncoder();
    enc.Frames.Add(BitmapFrame.Create(image));
    using var fs = File.Create(path);
    enc.Save(fs);
}

/// <summary>
/// The "countdown" logo: a blue rounded square with a ring three quarters full, like the countdown
/// shown next to each code, around a cyan dot. Drawn on a 100 unit grid scaled to 512 px.
/// </summary>
static class Logo
{
    static readonly Color Blue = Color.FromRgb(0x0B, 0x6F, 0xB8);
    static readonly Color Cyan = Color.FromRgb(0x5C, 0xCF, 0xF7);

    public static void Draw(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(5.12, 5.12));

        dc.DrawRoundedRectangle(new SolidColorBrush(Blue), null, new Rect(4, 4, 92, 92), 24, 24);

        var center = new Point(50, 50);
        const double radius = 26;
        var track = new Pen(new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)), 11);
        dc.DrawEllipse(null, track, center, radius, radius);

        // three quarters of the ring, clockwise from 12 o'clock
        var arc = new PathFigure { StartPoint = new Point(50, 24) };
        arc.Segments.Add(new ArcSegment(new Point(24, 50), new Size(radius, radius), 0, true, SweepDirection.Clockwise, true));
        var ring = new Pen(Brushes.White, 11) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, ring, new PathGeometry([arc]));

        dc.DrawEllipse(new SolidColorBrush(Cyan), null, center, 7, 7);
        dc.Pop();
    }
}
