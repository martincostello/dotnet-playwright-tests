using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace PlaywrightTests
{
    public class BrowserFixture
    {
        public BrowserFixture(ITestOutputHelper outputHelper)
        {
            OutputHelper = outputHelper;
        }

        private static bool IsRunningInGitHubActions { get; } = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        private ITestOutputHelper OutputHelper { get; }

        public async Task WithPageAsync(
            string browserType,
            Func<IPage, Task> action,
            [CallerMemberName] string testName = null)
        {
            // Create a new browser of the specified type
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await CreateBrowserAsync(playwright, browserType);

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
                await TryCaptureScreenshotAsync(page, testName!, browserType);
                throw;
            }
            finally
            {
                await TryCaptureVideoAsync(page);
            }
        }

        protected virtual BrowserNewPageOptions CreatePageOptions()
        {
            var options = new BrowserNewPageOptions()
            {
                Locale = "en-GB",
                TimezoneId = "Europe/London",
            };

            if (IsRunningInGitHubActions)
            {
                options.RecordVideoDir = "videos";
                options.RecordVideoSize = new RecordVideoSize() { Width = 800, Height = 600 };
            }

            return options;
        }

        private static async Task<IBrowser> CreateBrowserAsync(IPlaywright playwright, string browserType)
        {
            var options = new BrowserTypeLaunchOptions();

            if (System.Diagnostics.Debugger.IsAttached)
            {
                options.Devtools = true;
                options.Headless = false;
                options.SlowMo = 100;
            }

            return await playwright[browserType].LaunchAsync(options);
        }

        private async Task TryCaptureScreenshotAsync(
            IPage page,
            string testName,
            string browserType)
        {
            try
            {
                // Generate a unique name for the screenshot
                string os =
                    OperatingSystem.IsLinux() ? "linux" :
                    OperatingSystem.IsMacOS() ? "macos" :
                    OperatingSystem.IsWindows() ? "windows" :
                    "other";

                string utcNow = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture);
                string path = Path.Combine("screenshots", $"{testName}_{browserType}_{os}_{utcNow}.png");

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

        private async Task TryCaptureVideoAsync(IPage page)
        {
            if (!IsRunningInGitHubActions)
            {
                return;
            }

            try
            {
                await page.CloseAsync();
                OutputHelper.WriteLine($"Video saved to {await page.Video.PathAsync()}.");
            }
            catch (Exception ex)
            {
                OutputHelper.WriteLine("Failed to capture video: " + ex);
            }
        }
    }
}
