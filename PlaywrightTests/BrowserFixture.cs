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
    private const string VideosDirectory = "videos";

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
        string activeTestName = Options.TestName ?? testName;

        // Create a new browser of the specified type
        using IPlaywright playwright = await Playwright.CreateAsync();

        string videoUrl = null;

        await using (IBrowser browser = await CreateBrowserAsync(playwright, activeTestName))
        {
            // Create a new context for the test
            BrowserNewContextOptions options = CreateContextOptions();

            await using IBrowserContext context = await browser.NewContextAsync(options);

            // Enable generating a trace, if enabled, to use with https://trace.playwright.dev
            // Tracing does not currently work if using BrowserStack Automate.
            // See https://github.com/microsoft/playwright-dotnet/issues/1972.
            if (Options.CaptureTrace && !Options.UseBrowserStack)
            {
                await context.Tracing.StartAsync(new TracingStartOptions()
                {
                    Screenshots = true,
                    Snapshots = true,
                    Sources = true,
                    Title = activeTestName
                });
            }

            // Create a new page to use for the test
            IPage page = await context.NewPageAsync();

            // Capture output from the browser to the test logs
            page.Console += (_, e) => OutputHelper.WriteLine(e.Text);
            page.PageError += (_, e) => OutputHelper.WriteLine(e);

            try
            {
                // Run the test, passing the page to it
                await action(page);

                // Set the BrowserStack test status, if in use
                await TrySetSessionStatusAsync(page, "passed");
            }
            catch (Exception ex)
            {
                // Try and capture a screenshot at the point the test failed
                await TryCaptureScreenshotAsync(page, activeTestName);

                // Set the BrowserStack test status, if in use
                await TrySetSessionStatusAsync(page, "failed", ex.Message);
                throw;
            }
            finally
            {
                if (Options.CaptureTrace && !Options.UseBrowserStack)
                {
                    string traceName = GenerateFileName(activeTestName, ".zip");
                    string path = Path.Combine("traces", traceName);

                    await context.Tracing.StopAsync(new TracingStopOptions()
                    {
                        Path = path
                    });
                }

                videoUrl = await TryCaptureVideoAsync(page, activeTestName);
            }
        }

        if (videoUrl is not null)
        {
            // For BrowserStack Automate we need to fetch and save the video after the browser
            // is disposed of as we can't get the video while the session is still running.
            await CaptureBrowserStackVideoAsync(videoUrl, activeTestName);
        }
    }

    protected virtual BrowserNewContextOptions CreateContextOptions()
    {
        var options = new BrowserNewContextOptions()
        {
            Locale = "en-GB",
            TimezoneId = "Europe/London",
        };

        if (Options.CaptureVideo)
        {
            options.RecordVideoDir = Path.GetTempPath();
        }

        return options;
    }

    private async Task<IBrowser> CreateBrowserAsync(IPlaywright playwright, string testName)
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
                ["name"] = testName,
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

    private async Task TrySetSessionStatusAsync(IPage page, string status, string reason = "")
    {
        if (!Options.UseBrowserStack)
        {
            return;
        }

        // See https://www.browserstack.com/docs/automate/playwright/mark-test-status#mark-the-status-of-your-playwright-test-using-rest-api-duringafter-the-test-script-run
        string json = JsonSerializer.Serialize(new
        {
            action = "setSessionStatus",
            arguments = new
            {
                status,
                reason
            }
        });

        await page.EvaluateAsync("_ => {}", $"browserstack_executor: {json}");
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

    private async Task<string> TryCaptureVideoAsync(
        IPage page,
        string testName)
    {
        if (!Options.CaptureVideo || page.Video is null)
        {
            return null;
        }

        try
        {
            string fileName = GenerateFileName(testName, ".webm");
            string path = Path.Combine(VideosDirectory, fileName);

            if (Options.UseBrowserStack)
            {
                // BrowserStack Automate does not stop the video until the session has ended, so there
                // is no way to get the video to save it to a file, other than after the browser session
                // has ended. Instead, we get the URL to the video and then download it afterwards.
                // See https://www.browserstack.com/docs/automate/playwright/debug-failed-tests#video-recording.
                string session = await page.EvaluateAsync<string>("_ => {}", "browserstack_executor: {\"action\":\"getSessionDetails\"}");

                using var document = JsonDocument.Parse(session);
                return document.RootElement.GetProperty("video_url").GetString();
            }
            else
            {
                await page.CloseAsync();
                await page.Video.SaveAsAsync(path);
            }

            OutputHelper.WriteLine($"Video saved to {path}.");

            return null;
        }
        catch (Exception ex)
        {
            OutputHelper.WriteLine("Failed to capture video: " + ex);
            return null;
        }
    }

    private async Task CaptureBrowserStackVideoAsync(string videoUrl, string testName)
    {
        using var client = new HttpClient();

        for (int i = 0; i < 10; i++)
        {
            using var response = await client.GetAsync(videoUrl);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // The video may take a few seconds to be available
                await Task.Delay(TimeSpan.FromSeconds(2));
                continue;
            }

            response.EnsureSuccessStatusCode();

            string extension = Path.GetExtension(response.Content.Headers.ContentDisposition?.FileName) ?? ".mp4";
            string fileName = GenerateFileName(testName, extension);
            string path = Path.Combine(VideosDirectory, fileName);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(VideosDirectory);
            }

            using var file = File.OpenWrite(path);

            using var stream = await response.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(file);

            OutputHelper.WriteLine($"Video saved to {path}.");
            break;
        }
    }
}
