// Copyright (c) Martin Costello, 2021. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections;
using Microsoft.Playwright;

namespace PlaywrightTests;

public sealed class BrowsersTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return new[] { BrowserType.Chromium };

        if (!OperatingSystem.IsWindows())
        {
            yield return new[] { BrowserType.Chromium + ":chrome" };
        }

        if (!OperatingSystem.IsLinux())
        {
            yield return new[] { BrowserType.Chromium + ":msedge" };
        }

        yield return new object[] { BrowserType.Firefox };

        if (OperatingSystem.IsMacOS())
        {
            yield return new object[] { BrowserType.Webkit };
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
