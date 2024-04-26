// Copyright (c) Martin Costello, 2021. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;
using Xunit;

namespace PlaywrightTests;

public sealed class BrowsersTestData : TheoryData<string, string>
{
    public BrowsersTestData()
    {
        bool useBrowserStack = UseBrowserStack;

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

    public static bool UseBrowserStack => BrowserStackCredentials() != default;

    public static (string UserName, string AccessToken) BrowserStackCredentials()
    {
        string userName = Environment.GetEnvironmentVariable("BROWSERSTACK_USERNAME");
        string accessToken = Environment.GetEnvironmentVariable("BROWSERSTACK_TOKEN");

        return (userName, accessToken);
    }
}
