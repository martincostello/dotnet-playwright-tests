// Copyright (c) Martin Costello, 2021. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace PlaywrightTests;

public class BrowserFixture
{
    public BrowserFixture(
        BrowserFixtureOptions options,
        ITestOutputHelper outputHelper)
    {
        Options = options;
        OutputHelper = outputHelper;
    }

    private BrowserFixtureOptions Options { get; }

    private ITestOutputHelper OutputHelper { get; }

    public async Task WithPageAsync(
        Func<IPage, Task> action,
        [CallerMemberName] string testName = null)
    {
        // Create a new browser of the specified type
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await CreateBrowserAsync(playwright, testName);

        // Create a new page to use for the test
        BrowserNewPageOptions options = CreatePageOptions();
        IPage page = await browser.NewPageAsync(options);

        // Capture output from the browser to the test logs
        page.Console += (_, e) => OutputHelper.WriteLine(e.Text);
        page.PageError += (_, e) => OutputHelper.WriteLine(e);

        try
        {
            // Run the test, passing the page to it
            await action(page);
        }
        catch (Exception)
        {
            await TryCaptureScreenshotAsync(page, Options.TestName ?? testName);
            throw;
        }
        finally
        {
            await TryCaptureVideoAsync(page, Options.TestName ?? testName);
        }
    }

    protected virtual BrowserNewPageOptions CreatePageOptions()
    {
        var options = new BrowserNewPageOptions()
        {
            Locale = "en-GB",
            TimezoneId = "Europe/London",
        };

        if (BrowsersTestData.IsRunningInGitHubActions)
        {
            options.RecordVideoDir = Path.GetTempPath();
        }

        return options;
    }

    private async Task<IBrowser> CreateBrowserAsync(
        IPlaywright playwright,
        [CallerMemberName] string testName = null)
    {
        var options = new BrowserTypeLaunchOptions()
        {
            Channel = Options.BrowserChannel,
        };

        if (System.Diagnostics.Debugger.IsAttached)
        {
            options.Devtools = true;
            options.Headless = false;
            options.SlowMo = 100;
        }

        var browserType = playwright[Options.BrowserType];

        if (Options.UseBrowserStack && Options.BrowserStackCredentials != default)
        {
            // Allowed browsers are "chrome", "edge", "playwright-chromium", "playwright-firefox" and "playwright-webkit".
            // See https://www.browserstack.com/docs/automate/playwright and
            // https://github.com/browserstack/playwright-browserstack/blob/761b35bf79d79ddbfdf518fa6969b409bc42a941/google_search.js
            string browser;

            if (!string.IsNullOrEmpty(options.Channel))
            {
                browser = options.Channel switch
                {
                    "msedge" => "edge",
                    _ => options.Channel,
                };
            }
            else
            {
                browser = "playwright-" + Options.BrowserType;
            }

            // Use the version of the Microsoft.Playwright assembly unless
            // explicitly overridden by the options specified by the test.
            string playwrightVersion =
                Options.PlaywrightVersion ??
                typeof(IBrowser).Assembly.GetName()!.Version.ToString(3);

            // Supported capabilities and operating systems are documented at the following URLs:
            // https://www.browserstack.com/automate/capabilities
            // https://www.browserstack.com/list-of-browsers-and-platforms/playwright
            var capabilities = new Dictionary<string, string>()
            {
                ["browser"] = browser,
                ["browserstack.accessKey"] = Options.BrowserStackCredentials.AccessKey,
                ["browserstack.username"] = Options.BrowserStackCredentials.UserName,
                ["build"] = Options.Build ?? GetDefaultBuildNumber(),
                ["client.playwrightVersion"] = playwrightVersion,
                ["name"] = Options.TestName ?? testName,
                ["os"] = Options.OperatingSystem,
                ["os_version"] = Options.OperatingSystemVersion,
                ["project"] = Options.ProjectName ?? GetDefaultProject(),
            };

            // Serialize the capabilities as a JSON blob and pass to the
            // BrowserStack endpoint in the "caps" query string parameter.
            string json = JsonSerializer.Serialize(capabilities);
            string wsEndpoint = QueryHelpers.AddQueryString(Options.BrowserStackEndpoint.ToString(), "caps", json);

            var connectOptions = new BrowserTypeConnectOptions()
            {
                SlowMo = options.SlowMo,
                Timeout = options.Timeout
            };

            return await browserType.ConnectAsync(wsEndpoint, connectOptions);
        }

        return await browserType.LaunchAsync(options);
    }

    private static string GetDefaultBuildNumber()
    {
        string build = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");

        if (!string.IsNullOrEmpty(build))
        {
            return build;
        }

        return typeof(BrowserFixture).Assembly.GetName().Version.ToString(3);
    }

    private static string GetDefaultProject()
    {
        string project = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");

        if (!string.IsNullOrEmpty(project))
        {
            return project.Split('/')[1];
        }

        return "dotnet-playwright-tests";
    }

    private string GenerateFileName(string testName, string extension)
    {
        string browserType = Options.BrowserType;

        if (!string.IsNullOrEmpty(Options.BrowserChannel))
        {
            browserType += "_" + Options.BrowserChannel;
        }

        string os =
            OperatingSystem.IsLinux() ? "linux" :
            OperatingSystem.IsMacOS() ? "macos" :
            OperatingSystem.IsWindows() ? "windows" :
            "other";

        string utcNow = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture);
        return $"{testName}_{browserType}_{os}_{utcNow}{extension}";
    }

    private async Task TryCaptureScreenshotAsync(
        IPage page,
        string testName)
    {
        try
        {
            // Generate a unique name for the screenshot
            string fileName = GenerateFileName(testName, ".png");
            string path = Path.Combine("screenshots", fileName);

            await page.ScreenshotAsync(new PageScreenshotOptions()
            {
                Path = path,
            });

            OutputHelper.WriteLine($"Screenshot saved to {path}.");
        }
        catch (Exception ex)
        {
            OutputHelper.WriteLine("Failed to capture screenshot: " + ex);
        }
    }

    private async Task TryCaptureVideoAsync(
        IPage page,
        string testName)
    {
        // HACK The call to SaveAsAsync() hangs when used with BrowserStack.
        // https://github.com/martincostello/dotnet-playwright-tests/pull/34#issuecomment-1018689977
        if (!BrowsersTestData.IsRunningInGitHubActions || BrowsersTestData.UseBrowserStack)
        {
            return;
        }

        try
        {
            await page.CloseAsync();

            string fileName = GenerateFileName(testName, ".webm");
            string path = Path.Combine("videos", fileName);

            await page.Video.SaveAsAsync(path);

            OutputHelper.WriteLine($"Video saved to {path}.");
        }
        catch (Exception ex)
        {
            OutputHelper.WriteLine("Failed to capture video: " + ex);
        }
    }
}
