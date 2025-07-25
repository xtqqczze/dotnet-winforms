﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (C) 2004-2005 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// Author:
//
//  Jordi Mas i Hernandez (jordi@ximian.com)
//

namespace System.Drawing.Printing.Tests;

public class PageSettingsTests
{
    [Fact(Skip = "Condition not met", SkipType = typeof(Helpers), SkipUnless = nameof(Helpers.AnyInstalledPrinters))]
    public void Clone_Success()
    {
        PageSettings ps = new()
        {
            Color = false,
            Landscape = true,
            Margins = new Margins(120, 130, 140, 150),
            PaperSize = new PaperSize("My Custom Size", 222, 333)
        };
        PageSettings clone = (PageSettings)ps.Clone();

        Assert.Equal(ps.Color, clone.Color);
        Assert.Equal(ps.Landscape, clone.Landscape);
        Assert.Equal(ps.Margins, clone.Margins);
        Assert.Same(ps.PrinterSettings, clone.PrinterSettings);

        // PaperSize
        Assert.Equal(ps.PaperSize.PaperName, clone.PaperSize.PaperName);
        Assert.Equal(ps.PaperSize.Width, clone.PaperSize.Width);
        Assert.Equal(ps.PaperSize.Height, clone.PaperSize.Height);
        Assert.Equal(ps.PaperSize.Kind, clone.PaperSize.Kind);

        // PrinterResolution
        Assert.Equal(ps.PrinterResolution.X, clone.PrinterResolution.X);
        Assert.Equal(ps.PrinterResolution.Y, clone.PrinterResolution.Y);
        Assert.Equal(ps.PrinterResolution.Kind, clone.PrinterResolution.Kind);

        // PaperSource
        Assert.Equal(ps.PaperSource.Kind, clone.PaperSource.Kind);
        Assert.Equal(ps.PaperSource.SourceName, clone.PaperSource.SourceName);
    }

    [Fact(Skip = "Condition not met", SkipType = typeof(Helpers), SkipUnless = nameof(Helpers.AnyInstalledPrinters))]
    public void PrintToPDF_DefaultPageSettings_IsColor()
    {
        // Regression test for https://github.com/dotnet/winforms/issues/13367
        if (!Helpers.TryGetPdfPrinterName(out string? printerName))
        {
            return;
        }

        PrinterSettings printerSettings = new()
        {
            PrinterName = printerName
        };

        printerSettings.DefaultPageSettings.Color.Should().BeTrue("PDF printer should support color printing.");
    }
}
