// Copyright (c) KnifeLemon. Licensed under the MIT License.

using Filee.Core.Presets;
using Xunit;

namespace Filee.Core.Tests;

public sealed class SizeTextTests
{
    [Theory]
    [InlineData("500", ByteUnit.KB, 500 * 1024L)]
    [InlineData("500kb", ByteUnit.KB, 500 * 1024L)]
    [InlineData("500 KB", ByteUnit.KB, 500 * 1024L)]
    [InlineData("500k", ByteUnit.KB, 500 * 1024L)]
    [InlineData("1.5 MB", ByteUnit.KB, (long)(1.5 * 1024 * 1024))]
    [InlineData("1,5mb", ByteUnit.KB, (long)(1.5 * 1024 * 1024))]
    [InlineData("2 GB", ByteUnit.KB, 2L * 1024 * 1024 * 1024)]
    [InlineData("2048 bytes", ByteUnit.KB, 2048L)]
    [InlineData("1024b", ByteUnit.KB, 1024L)]
    [InlineData("1", ByteUnit.MB, 1024 * 1024L)]
    public void Parses_valid_sizes(string input, ByteUnit defaultUnit, long expected)
    {
        Assert.True(SizeText.TryParse(input, defaultUnit, out var bytes));
        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("-500kb")]
    [InlineData("NaN")]
    [InlineData("abc")]
    [InlineData("500 bytes")] // Less than 1 KB (1024 bytes) minimum
    [InlineData("1023")] // Less than 1 KB if default is Bytes
    public void Rejects_invalid_sizes(string? input)
    {
        Assert.False(SizeText.TryParse(input, ByteUnit.Bytes, out _));
    }

    [Fact]
    public void Format_returns_readable_string()
    {
        Assert.Equal("500 KB", SizeText.Format(500 * 1024));
        Assert.Equal("1.5 MB", SizeText.Format((long)(1.5 * 1024 * 1024)));
        Assert.Equal("2 GB", SizeText.Format(2L * 1024 * 1024 * 1024));
    }
}
