// Copyright (c) Martin Costello, 2021. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace PlaywrightTests;

public class SearchTests
{
    public SearchTests(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
    }

    private ITestOutputHelper OutputHelper { get; }

    [Theory]
    [ClassData(typeof(BrowsersTestData))]
    public async Task Search_For_DotNet_Core(string browserType)
    {
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
