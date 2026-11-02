// DeskAuth, based on WinAuth.
// Copyright (C) 2010-2017 Colin Mackie (WinAuth).
// Copyright (C) 2026 Nick Morphis and contributors (DeskAuth, modified from WinAuth).
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using DeskAuth.Core;

namespace DeskAuth.Services;

/// <summary>
/// Optional anonymous usage statistics, sent only if the user agrees. See privacy.html on deskauth.com.
///
/// At most once a day, when DeskAuth starts, it sends a random install ID, the app version, the Windows
/// version and the processor type to Google Analytics (the "DeskAuth app" property, using the
/// Measurement Protocol). Nothing about the user's authenticators is ever sent.
/// </summary>
internal static class UsageStatistics
{
    // Google Analytics > Admin > Data streams > the app's web stream: its measurement ID, and an API
    // secret from "Measurement Protocol API secrets". Both empty turns usage statistics off entirely,
    // and users are not asked.
    private const string MeasurementId = "G-D9PKP4NWP1";
    private const string ApiSecret = "-ZrHZfL1R2eWntj2NdsplQ";

    private static readonly TimeSpan ReportInterval = TimeSpan.FromHours(20);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// Usage statistics are set up in this build
    /// </summary>
    public static bool IsAvailable => MeasurementId.Length != 0 && ApiSecret.Length != 0;

    /// <summary>
    /// Record the user's choice. Agreeing creates an install ID; declining discards it.
    /// </summary>
    public static void SetChoice(DeskAuthConfig config, bool send)
    {
        config.SendUsageStatistics = send;
        if (send)
        {
            config.InstallId ??= Guid.NewGuid();
        }
        else
        {
            config.InstallId = null;
            config.LastUsageReport = null;
        }
    }

    /// <summary>
    /// Send today's report if the user agreed and one hasn't been sent recently
    /// </summary>
    /// <returns>true if a report was sent, so the config should be saved</returns>
    public static async Task<bool> ReportIfDueAsync(DeskAuthConfig config)
    {
        if (!IsAvailable || config.SendUsageStatistics != true || config.InstallId is not Guid installId)
        {
            return false;
        }
        var now = DateTimeOffset.UtcNow;
        if (config.LastUsageReport is DateTimeOffset last && now - last < ReportInterval)
        {
            return false;
        }

        // GA4 counts a user as active when an event has a session and engagement time
        var payload = new
        {
            client_id = installId.ToString(),
            non_personalized_ads = true,
            events = new[]
            {
                new
                {
                    name = "app_start",
                    @params = new Dictionary<string, object>
                    {
                        ["app_version"] = AppInfo.VersionText,
                        ["os_version"] = Environment.OSVersion.Version.ToString(),
                        ["architecture"] = RuntimeInformation.OSArchitecture.ToString(),
                        ["session_id"] = now.ToUnixTimeSeconds().ToString(),
                        ["engagement_time_msec"] = 1,
                    },
                },
            },
        };

        try
        {
            string url = $"https://www.google-analytics.com/mp/collect?measurement_id={Uri.EscapeDataString(MeasurementId)}&api_secret={Uri.EscapeDataString(ApiSecret)}";
            using var response = await Http.PostAsJsonAsync(url, payload);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // offline or blocked: try again next time
            return false;
        }

        config.LastUsageReport = now;
        return true;
    }
}
