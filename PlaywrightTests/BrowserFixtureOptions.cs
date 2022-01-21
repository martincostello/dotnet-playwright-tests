// Copyright (c) Martin Costello, 2021. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace PlaywrightTests;

/// <summary>
/// A class representing the options to use with <see cref="BrowserFixture"/>.
/// </summary>
public class BrowserFixtureOptions
{
    /// <summary>
    /// Gets or sets the browser type.
    /// </summary>
    public string BrowserType { get; set; }

    /// <summary>
    /// Gets or sets the optional browser channel.
    /// </summary>
    public string BrowserChannel { get; set; }

    /// <summary>
    /// Gets or sets the optional build number.
    /// </summary>
    public string Build { get; set; }

    /// <summary>
    /// Gets or sets the optional operating system name.
    /// </summary>
    public string OperatingSystem { get; set; }

    /// <summary>
    /// Gets or sets the optional operating system version.
    /// </summary>
    public string OperatingSystemVersion { get; set; }

    /// <summary>
    /// Gets or sets the optional Playwright version number.
    /// </summary>
    public string PlaywrightVersion { get; set; }

    /// <summary>
    /// Gets or sets the optional project name.
    /// </summary>
    public string ProjectName { get; set; }

    /// <summary>
    /// Gets or sets the optional test name.
    /// </summary>
    public string TestName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use BrowserStack.
    /// </summary>
    public bool UseBrowserStack { get; set; }

    /// <summary>
    /// Gets or sets the credentials to use to connect to BrowserStack.
    /// </summary>
    public (string UserName, string AccessKey) BrowserStackCredentials { get; set; }

    /// <summary>
    /// Gets or sets the URI of the WS endpoint to use to connect to BrowserStack.
    /// </summary>
    public Uri BrowserStackEndpoint { get; set; } = new("wss://cdp.browserstack.com/playwright", UriKind.Absolute);
}
