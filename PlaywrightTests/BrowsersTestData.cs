// Copyright (c) Martin Costello, 2021. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.BrowserStack.Automate;
using Microsoft.Playwright;
using Xunit;

namespace PlaywrightTests;

public sealed class BrowsersTestData : TheoryData<string, string>
{
    public BrowsersTestData()
    {
        bool useBrowserStack = BrowserStackCredentials() != default;

        Add(BrowserType.Chromium, null);

        if (useBrowserStack || !OperatingSystem.IsWindows())
        {
            Add(BrowserType.Chromium, "chrome");
        }

        if (useBrowserStack || OperatingSystem.IsWindows())
        {
            Add(BrowserType.Chromium, "msedge");
        }

        Add(BrowserType.Firefox, null);

        /*
        if (useBrowserStack || OperatingSystem.IsMacOS())
        {
            Add(BrowserType.Webkit, null);
        }
        */
    }

    public static bool IsRunningInGitHubActions { get; } = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

    public static async Task<bool> UseBrowserStackAsync(CancellationToken cancellationToken)
    {
        var credentials = BrowserStackCredentials();

        if (credentials == default)
        {
            return false;
        }

        using var client = new BrowserStackAutomateClient(credentials.UserName, credentials.AccessToken);
        var status = await client.GetStatusAsync(cancellationToken);

        if (status.MaximumAllowedParallelSessions < 1 ||
            status.ParallelSessionsRunning == status.MaximumAllowedParallelSessions)
        {
            return false;
        }

        return true;
    }

    public static (string UserName, string AccessToken) BrowserStackCredentials()
    {
        string userName = Environment.GetEnvironmentVariable("BROWSERSTACK_USERNAME");
        string accessToken = Environment.GetEnvironmentVariable("BROWSERSTACK_TOKEN");

        return (userName, accessToken);
    }
}
