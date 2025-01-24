// Copyright (c) Martin Costello, 2021. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;
using Xunit;

namespace PlaywrightTests;

public class SearchTests(ITestOutputHelper outputHelper) : IAsyncLifetime
{
    public ValueTask InitializeAsync()
    {
        int exitCode = Program.Main(["install"]);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright exited with code {exitCode}.");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    [Theory]
    [ClassData(typeof(BrowsersTestData))]
    public async Task Search_For_DotNet_Core(string browserType, string browserChannel)
    {
        // Configure the options to use with the fixture for this test
        var options = new BrowserFixtureOptions()
        {
            BrowserType = browserType,
            BrowserChannel = browserChannel,
        };

        if (await BrowsersTestData.UseBrowserStackAsync(TestContext.Current.CancellationToken))
        {
            options.BrowserStackCredentials = BrowsersTestData.BrowserStackCredentials();
            options.UseBrowserStack = true;
        }

        // Create fixture that will provide an IPage to use for the test
        var browser = new BrowserFixture(options, outputHelper);
        await browser.WithPageAsync(async (page) =>
        {
            // Open the search engine
            await page.GotoAsync("https://www.bing.com/");
            await page.WaitForLoadStateAsync();

            try
            {
                // Dismiss any cookies banner
                IElementHandle element = await page.WaitForSelectorAsync("text='Accept'", new() { Timeout = 15_000 });

                if (element is not null)
                {
                    await element.ClickAsync();
                    await element.WaitForElementStateAsync(ElementState.Hidden);
                    await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
                }
            }
            catch (TimeoutException)
            {
                // No banner
            }

            // Search for the desired term
            await page.FillAsync("[name='q']", ".net core");
            await page.Keyboard.PressAsync("Enter");

            // Wait for the results to load
            await page.WaitForSelectorAsync("[aria-label='Search Results']");

            // Click through to the desired result
            await page.ClickAsync("a:has-text(\".NET\")");
        });
    }
}
