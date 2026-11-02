// DeskAuth, based on WinAuth.
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using DeskAuth.Core;

namespace DeskAuth.Services;

/// <summary>
/// Asks GitHub for the latest published release when DeskAuth starts. Only the request itself is sent:
/// GitHub sees the IP address and the DeskAuth version in the User-Agent, and nothing else.
/// </summary>
internal static class UpdateChecker
{
    private static readonly HttpClient Http = CreateClient();

    /// <summary>
    /// Look for a newer release
    /// </summary>
    /// <returns>the newer release, or null if there isn't one or GitHub can't be reached</returns>
    public static async Task<UpdateCheck.Release?> FindNewerReleaseAsync()
    {
        try
        {
            using var response = await Http.GetAsync(UpdateCheck.LatestReleaseApi);
            if (!response.IsSuccessStatusCode)
            {
                // 404 when there are no releases yet, 403 when rate limited
                return null;
            }
            string json = await response.Content.ReadAsStringAsync();
            return UpdateCheck.FindNewerRelease(json, AppInfo.Version);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // offline, blocked or an unexpected response: try again next time DeskAuth starts
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // GitHub requires a User-Agent
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DeskAuth", AppInfo.VersionText));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }
}
