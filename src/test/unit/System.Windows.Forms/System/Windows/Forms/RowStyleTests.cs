﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace System.Windows.Forms.Tests;

public class RowStyleTests
{
    [WinFormsFact]
    public void RowStyle_Ctor_Default()
    {
        RowStyle style = new();
        Assert.Equal(SizeType.AutoSize, style.SizeType);
        Assert.Equal(0, style.Height);
    }

    [WinFormsTheory]
    [EnumData<SizeType>]
    [InvalidEnumData<SizeType>]
    public void RowStyle_Ctor_SizeType(SizeType sizeType)
    {
        RowStyle style = new(sizeType);
        Assert.Equal(sizeType, style.SizeType);
        Assert.Equal(0, style.Height);
    }

    [WinFormsTheory]
    [InlineData(SizeType.AutoSize, 0)]
    [InlineData(SizeType.Absolute, 1)]
    [InlineData(SizeType.Percent, 2)]
    [InlineData((SizeType.AutoSize - 1), 3)]
    [InlineData((SizeType.Percent + 1), 4)]
    public void RowStyle_Ctor_SizeType_Float(SizeType sizeType, float width)
    {
        RowStyle style = new(sizeType, width);
        Assert.Equal(sizeType, style.SizeType);
        Assert.Equal(width, style.Height);
    }

    [WinFormsFact]
    public void RowStyle_Ctor_NegativeHeight_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>("value", () => new RowStyle(SizeType.AutoSize, -1));
    }

    [WinFormsTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(float.MaxValue)]
    public void RowStyle_Height_Set_GetReturnsExpected(float value)
    {
        RowStyle style = new()
        {
            Height = value
        };
        Assert.Equal(value, style.Height);

        // Set same.
        style.Height = value;
        Assert.Equal(value, style.Height);
    }

    [WinFormsTheory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(float.MaxValue, 1)]
    public void RowStyle_Height_SetWithOwner_GetReturnsExpected(float value, int expectedLayoutCallCount)
    {
        using TableLayoutPanel control = new();
        RowStyle style = new();
        control.LayoutSettings.RowStyles.Add(style);
        int layoutCallCount = 0;
        control.Layout += (sender, e) =>
        {
            Assert.Same(control, sender);
            Assert.Same(control, e.AffectedControl);
            Assert.Equal("Style", e.AffectedProperty);
            layoutCallCount++;
        };

        style.Height = value;
        Assert.Equal(value, style.Height);
        Assert.Equal(expectedLayoutCallCount, layoutCallCount);
        Assert.False(control.IsHandleCreated);

        // Set same.
        style.Height = value;
        Assert.Equal(value, style.Height);
        Assert.Equal(expectedLayoutCallCount, layoutCallCount);
        Assert.False(control.IsHandleCreated);
    }

    [WinFormsTheory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(float.MaxValue, 1)]
    public void RowStyle_Height_SetWithOwnerWithHandle_GetReturnsExpected(float value, int expectedLayoutCallCount)
    {
        using TableLayoutPanel control = new();
        RowStyle style = new();
        control.LayoutSettings.RowStyles.Add(style);
        Assert.NotEqual(IntPtr.Zero, control.Handle);
        int invalidatedCallCount = 0;
        control.Invalidated += (sender, e) => invalidatedCallCount++;
        int styleChangedCallCount = 0;
        control.StyleChanged += (sender, e) => styleChangedCallCount++;
        int createdCallCount = 0;
        control.HandleCreated += (sender, e) => createdCallCount++;
        int layoutCallCount = 0;
        control.Layout += (sender, e) =>
        {
            Assert.Same(control, sender);
            Assert.Same(control, e.AffectedControl);
            Assert.Equal("Style", e.AffectedProperty);
            layoutCallCount++;
        };

        style.Height = value;
        Assert.Equal(value, style.Height);
        Assert.Equal(expectedLayoutCallCount, layoutCallCount);
        Assert.True(control.IsHandleCreated);
        Assert.Equal(expectedLayoutCallCount * 2, invalidatedCallCount);
        Assert.Equal(0, styleChangedCallCount);
        Assert.Equal(0, createdCallCount);

        // Set same.
        style.Height = value;
        Assert.Equal(value, style.Height);
        Assert.Equal(expectedLayoutCallCount, layoutCallCount);
        Assert.True(control.IsHandleCreated);
        Assert.Equal(expectedLayoutCallCount * 2, invalidatedCallCount);
        Assert.Equal(0, styleChangedCallCount);
        Assert.Equal(0, createdCallCount);
    }

    [WinFormsFact]
    public void RowStyle_Height_SetNegative_ThrowsArgumentOutOfRangeException()
    {
        RowStyle style = new();
        Assert.Throws<ArgumentOutOfRangeException>("value", () => style.Height = -1);
    }
}
