// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.IO;
using System.Net.Http;
using System.Text;
using DeskAuth.Core;

namespace DeskAuth.Services;

/// <summary>
/// Gets an authenticator icon from an image address, or the icon a website shows in browsers
/// </summary>
internal static class IconDownloader
{
    private const int MaxImageBytes = 2 * 1024 * 1024;
    private const int MaxPageBytes = 2 * 1024 * 1024;

    private static readonly HttpClient Http = CreateClient();

    public sealed class DownloadException(string message, Exception? inner = null) : Exception(message, inner);

    /// <summary>
    /// Get an icon from an address, which can be an image or a web page
    /// </summary>
    /// <param name="address">URL; "https://" is added if there is no scheme</param>
    /// <returns>skin value for the icon</returns>
    /// <exception cref="DownloadException">no usable icon was found</exception>
    public static async Task<string> GetSkinAsync(string address, Func<byte[], string> createSkin, CancellationToken cancel)
    {
        Uri uri = ParseAddress(address);

        byte[] data;
        string? contentType;
        Uri finalUri;
        try
        {
            (data, contentType, finalUri) = await DownloadAsync(uri, MaxPageBytes, cancel);
        }
        catch (DownloadException)
        {
            // some sites block pages for non-browsers but still serve their favicon
            if (await TryGetFaviconIcoAsync(uri, createSkin, cancel) is string favicon)
            {
                return favicon;
            }
            throw;
        }
        if (!IsHtml(contentType, data) && TryCreateSkin(data, createSkin) is string direct)
        {
            return direct;
        }
        if (!IsHtml(contentType, data))
        {
            throw new DownloadException("The address is not an image or web page that Windows can read.");
        }

        // a web page: try its declared icons, best first
        string html = Encoding.UTF8.GetString(data);
        foreach (var icon in FaviconFinder.FindIcons(html, finalUri))
        {
            cancel.ThrowIfCancellationRequested();
            byte[]? iconData = null;
            try
            {
                iconData = icon.Url.Scheme == "data" ? DecodeDataUri(icon.Url.OriginalString) : (await DownloadAsync(icon.Url, MaxImageBytes, cancel)).Data;
            }
            catch (DownloadException)
            {
                // try the next one
            }
            if (iconData != null && TryCreateSkin(iconData, createSkin) is string skin)
            {
                return skin;
            }
        }
        throw new DownloadException($"No icon was found for {finalUri.Host}.");
    }

    private static async Task<string?> TryGetFaviconIcoAsync(Uri site, Func<byte[], string> createSkin, CancellationToken cancel)
    {
        var favicon = new Uri(site, "/favicon.ico");
        if (favicon == site)
        {
            return null;
        }
        try
        {
            return TryCreateSkin((await DownloadAsync(favicon, MaxImageBytes, cancel)).Data, createSkin);
        }
        catch (DownloadException)
        {
            return null;
        }
    }

    private static Uri ParseAddress(string address)
    {
        address = address.Trim();
        if (!address.Contains("://", StringComparison.Ordinal))
        {
            address = "https://" + address;
        }
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https") || uri.Host.Length == 0)
        {
            throw new DownloadException("Enter a website or image address, such as github.com.");
        }
        return uri;
    }

    private static async Task<(byte[] Data, string? ContentType, Uri FinalUri)> DownloadAsync(Uri uri, int maxBytes, CancellationToken cancel)
    {
        try
        {
            using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancel);
            if (!response.IsSuccessStatusCode)
            {
                throw new DownloadException($"{uri.Host} returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }
            if (response.Content.Headers.ContentLength > maxBytes)
            {
                throw new DownloadException("The file is too large.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancel);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancel)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                {
                    throw new DownloadException("The file is too large.");
                }
                buffer.Write(chunk, 0, read);
            }
            return (buffer.ToArray(), response.Content.Headers.ContentType?.MediaType, response.RequestMessage?.RequestUri ?? uri);
        }
        catch (HttpRequestException ex)
        {
            throw new DownloadException($"Cannot connect to {uri.Host}. {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancel.IsCancellationRequested)
        {
            throw new DownloadException($"{uri.Host} took too long to respond.", ex);
        }
    }

    private static bool IsHtml(string? contentType, byte[] data)
    {
        if (contentType != null && contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        // some servers don't send a content type
        string start = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 512)).TrimStart('﻿', ' ', '\r', '\n', '\t');
        return start.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase) || start.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryCreateSkin(byte[] data, Func<byte[], string> createSkin)
    {
        try
        {
            return createSkin(data);
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or IOException
            or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static byte[]? DecodeDataUri(string uri)
    {
        int comma = uri.IndexOf(',');
        if (comma < 0 || !uri[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        try
        {
            return Convert.FromBase64String(Uri.UnescapeDataString(uri[(comma + 1)..]));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
        })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        // some sites refuse requests that don't look like a browser
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DeskAuth/4.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,image/png,image/x-icon,image/*;q=0.8,*/*;q=0.5");
        return client;
    }
}
