// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskAuth.Core;

namespace DeskAuth.Services;

/// <summary>
/// Authenticator icons: custom images stored in the config, or the DeskAuth logo by default.
///
/// Files from WinAuth 3 can also name one of its built-in icons (e.g. "GitHubIcon.png"), which are
/// kept in Resources\Icons so those authenticators keep their icons.
/// </summary>
internal static class IconLibrary
{
    private const string ResourcePrefix = "Icons.";
    private const string LogoResource = "DeskAuthLogo.png";
    private const string Base64Prefix = "base64:";

    /// <summary>
    /// WinAuth 3 icons for the DeskAuth logo, which use the current logo
    /// </summary>
    private static readonly HashSet<string> LogoSkins = new(StringComparer.OrdinalIgnoreCase) { "WinAuthIcon.png", "WinAuth2Icon.png" };

    /// <summary>
    /// Size custom icons are stored at
    /// </summary>
    public const int IconSize = 48;

    private static readonly Assembly Assembly = typeof(IconLibrary).Assembly;
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The DeskAuth logo
    /// </summary>
    public static ImageSource Logo { get; } = LoadStream(Assembly.GetManifestResourceStream(LogoResource)!)!;

    /// <summary>
    /// Icon for an authenticator: its chosen icon, or the DeskAuth logo
    /// </summary>
    public static ImageSource For(AuthenticatorEntry entry) => ForSkin(entry.Skin);

    /// <summary>
    /// Icon for a skin value, or the DeskAuth logo if it is empty or can't be loaded
    /// </summary>
    public static ImageSource ForSkin(string? skin)
    {
        if (string.IsNullOrEmpty(skin) || LogoSkins.Contains(skin))
        {
            return Logo;
        }
        return Load(skin) ?? Logo;
    }

    /// <summary>
    /// Create a custom icon value from an image file, scaled to <see cref="IconSize"/>
    /// </summary>
    public static string CreateCustomSkin(string imageFile) => CreateCustomSkin(File.ReadAllBytes(imageFile));

    /// <summary>
    /// Create a custom icon value from image data (PNG, JPEG, GIF, BMP, ICO...), scaled to fit <see cref="IconSize"/>
    /// </summary>
    /// <exception cref="NotSupportedException">the data is not an image Windows can decode</exception>
    public static string CreateCustomSkin(byte[] imageData)
    {
        using var input = new MemoryStream(imageData);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        // .ico files hold several sizes
        BitmapSource source = decoder.Frames.MaxBy(f => f.PixelWidth * f.PixelHeight)
            ?? throw new NotSupportedException("The image is empty");

        // fit inside the icon square, keeping the aspect ratio
        double scale = Math.Min((double)IconSize / source.PixelWidth, (double)IconSize / source.PixelHeight);
        double width = source.PixelWidth * scale;
        double height = source.PixelHeight * scale;
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(source, new System.Windows.Rect((IconSize - width) / 2, (IconSize - height) / 2, width, height));
        }
        var bitmap = new RenderTargetBitmap(IconSize, IconSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();
        encoder.Save(output);
        return Base64Prefix + Convert.ToBase64String(output.ToArray());
    }

    private static ImageSource? Load(string skin)
    {
        if (Cache.TryGetValue(skin, out var cached))
        {
            return cached;
        }

        ImageSource? image = null;
        try
        {
            using Stream? stream = skin.StartsWith(Base64Prefix, StringComparison.Ordinal)
                ? new MemoryStream(Convert.FromBase64String(skin[Base64Prefix.Length..]))
                : Assembly.GetManifestResourceStream(ResourcePrefix + skin);
            if (stream != null)
            {
                image = LoadStream(stream);
            }
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or IOException)
        {
            // invalid image data; use the default
        }

        // don't cache custom images, which are unique and may be large
        if (!skin.StartsWith(Base64Prefix, StringComparison.Ordinal))
        {
            Cache[skin] = image;
        }
        return image;
    }

    private static ImageSource LoadStream(Stream stream)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = stream;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
