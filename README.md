# .NET Playwright Tests

[![Build status](https://github.com/martincostello/dotnet-playwright-tests/workflows/build/badge.svg?branch=main&event=push)](https://github.com/martincostello/dotnet-playwright-tests/actions?query=workflow%3Abuild+branch%3Amain+event%3Apush)

A simple test implemented in .NET 6 that uses [Playwright] for tests with
multiple browsers, including support for [BrowserStack Automate].

## Introduction

This repository demonstrates the use of Playwright to write automated browser
tests using .NET and [xunit].

The [`SearchTests`] class contains a single xunit test, which use the types
provided by the [Microsoft.Playwright] NuGet package to open an browser and then
search for the text "_.net core_", wait for the search results to load, and
then click on the first link with the text "_.NET_" in it.

```csharp
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

    if (BrowsersTestData.UseBrowserStack)
    {
        options.BrowserStackCredentials = BrowsersTestData.BrowserStackCredentials();
        options.UseBrowserStack = true;
    }

    // Create fixture that will provide an IPage to use for the test
    var browser = new BrowserFixture(options, OutputHelper);
    await browser.WithPageAsync(async (page) =>
    {
        // Open the search engine
        await page.GotoAsync("https://www.google.com/");
        await page.WaitForLoadStateAsync();

        // Dismiss any cookies dialog
        IElementHandle element = await page.QuerySelectorAsync("text='I agree'");

        if (element is not null)
        {
            await element.ClickAsync();
        }

        // Search for the desired term
        await page.TypeAsync("[name='q']", ".net core");
        await page.Keyboard.PressAsync("Enter");

        // Wait for the results to load
        await page.WaitForSelectorAsync("id=main");

        // Click through to the desired result
        await page.ClickAsync("a:has-text(\".NET\")");
    });
}
```

The test is _[data driven]_ and can run against multiple browsers, which are
determined by the [`BrowsersTestData`] class based on the operating system being
used to run the test.

Creating an [`IPage`] instance for a specific browser to use in specific tests
is delegated to the [`BrowserFixture`] class. `BrowserFixture` contains the
implementation for configuring an [`IBrowser`] for the specified browser type
and channel, such as Google Chrome or Mozilla Firefox, as well as configuring
any timeouts. Tests are run headless by default, unless a debugger is attached.

The fixture also handles cross-cutting concerns, such as recording video, taking
screenshots on test failure, and logging console and page errors.

The behaviour of `BrowserFixture` can be customised further using the
[`BrowserFixtureOptions`] class.

### Integrating with BrowserStack Automate

The `BrowserFixture` class also supports using [BrowserStack Automate] to run
tests, which allows you to run tests for multiple browser, device and operating
system combinations independently of the operating system used to run the tests
themselves.

The fixture also handles cross-cutting concerns, such as setting the outcome of
a browser session in the test run in the BrowserStack Automate console, and
recording video.

The `BrowserFixtureOptions` class contains options to customise the behaviour,
such as specifying a build number or project name, amongst other options. The
fixture also contains default behaviours to set the build number and project
name automatically if running in [GitHub Actions].

To enable the use of BrowserStack Automate with the tests, instead of locally
running browser instances, configure the credentials for the tests to use with
the `BROWSERSTACK_USERNAME` and `BROWSERSTACK_TOKEN` environment variables.

## Running the sample

To clone and run the sample locally, run the following commands.

```sh
# Clone the repo
git clone https://github.com/martincostello/dotnet-playwright-tests.git
cd dotnet-playwright-tests

# Run the tests
dotnet test
```

[BrowserStack Automate]: https://www.browserstack.com/automate
[data driven]: https://andrewlock.net/creating-parameterised-tests-in-xunit-with-inlinedata-classdata-and-memberdata/
[GitHub Actions]: https://docs.github.com/en/actions
[Microsoft.Playwright]: https://www.nuget.org/packages/Microsoft.Playwright/
[Playwright]: https://playwright.dev/dotnet/
[xunit]: https://xunit.net/

[`BrowserFixture`]: https://github.com/martincostello/dotnet-playwright-tests/blob/main/PlaywrightTests/BrowserFixture.cs
[`BrowserFixtureOptions`]: https://github.com/martincostello/dotnet-playwright-tests/blob/main/PlaywrightTests/BrowserFixtureOptions.cs
[`BrowsersTestData`]: https://github.com/martincostello/dotnet-playwright-tests/blob/648d0f9ad0235a952fa0fc935ff038b1a833f30b/PlaywrightTests/BrowsersTestData.cs#L23-L45
[`IBrowser`]: https://playwright.dev/dotnet/docs/api/class-browser
[`IPage`]: https://playwright.dev/dotnet/docs/api/class-page
[`SearchTests`]: https://github.com/martincostello/dotnet-playwright-tests/blob/648d0f9ad0235a952fa0fc935ff038b1a833f30b/PlaywrightTests/SearchTests.cs#L19-L21
