using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace PlaywrightTests
{
    public class SearchTests
    {
        public SearchTests(ITestOutputHelper outputHelper)
        {
            OutputHelper = outputHelper;
        }

        private ITestOutputHelper OutputHelper { get; }

        [SkippableTheory]
        [InlineData("chromium")]
        [InlineData("firefox")]
        [InlineData("webkit")]
        public async Task Search_For_DotNet_Core(string browserType)
        {
            // Arrange
            Skip.If(
                !OperatingSystem.IsMacOS() && browserType == "webkit",
                $"{browserType} is only supported on macOS.");

            // Create fixture that will provide an IPage to use for the test
            var browser = new BrowserFixture(OutputHelper);
            await browser.WithPageAsync(browserType, async (page) =>
            {
                // Open the search engine
                await page.GotoAsync("https://www.bing.com/");
                await page.WaitForLoadStateAsync();

                // Dismiss any cookies dialog
                IElementHandle element = await page.QuerySelectorAsync("id=bnp_btn_accept");

                if (element is not null)
                {
                    await element.ClickAsync();
                }

                // Search for the desired term
                await page.TypeAsync("[name='q']", ".net core");
                await page.Keyboard.PressAsync("Enter");

                // Wait for the results to load
                await page.WaitForSelectorAsync("id=b_content");

                // Click through to the desired result
                await page.ClickAsync("a:has-text(\".NET Core\")");
            });
        }
    }
}
