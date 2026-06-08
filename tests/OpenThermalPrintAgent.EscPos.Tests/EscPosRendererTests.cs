using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.EscPos;

namespace OpenThermalPrintAgent.EscPos.Tests;

public sealed class EscPosRendererTests
{
    private readonly EscPosRenderer _renderer = new();

    [Fact]
    public void RenderWritesInitializeAndSimpleText()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.Text, Value = "Hello" }));

        Assert.Equal(Bytes(
            0x1B, 0x40,
            0x1B, 0x61, 0x00,
            'H', 'e', 'l', 'l', 'o',
            0x0A), bytes);
    }

    [Fact]
    public void RenderWritesBoldCommand()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.Text, Value = "Total", Bold = true }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x45, 0x01));
    }

    [Fact]
    public void RenderWritesAlignmentCommands()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.Text, Value = "Center", Align = TextAlignment.Center },
            new PrintContentCommand { Type = PrintCommandType.Text, Value = "Right", Align = TextAlignment.Right }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x61, 0x01));
        AssertContainsSequence(bytes, Bytes(0x1B, 0x61, 0x02));
    }

    [Fact]
    public void RenderWritesFeedCommand()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.Feed, Lines = 3 }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x64, 0x03));
    }

    [Fact]
    public void RenderWritesCutCommand()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.Cut }));

        AssertContainsSequence(bytes, Bytes(0x1D, 0x56, 0x01));
    }

    [Fact]
    public void RenderWritesExplicitFullCutCommand()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.Cut, Mode = CutMode.Full }));

        AssertContainsSequence(bytes, Bytes(0x1D, 0x56, 0x00));
    }

    [Fact]
    public void RenderWritesExplicitPartialCutCommand()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.Cut, Mode = CutMode.Partial }));

        AssertContainsSequence(bytes, Bytes(0x1D, 0x56, 0x01));
    }

    [Fact]
    public void RenderWritesExplicitFeedAndFullCutCommand()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.Cut, Mode = CutMode.FeedAndFull }));

        AssertContainsSequence(bytes, Bytes(0x1D, 0x56, 0x41, 0x03));
    }

    [Fact]
    public void RenderWritesExplicitFeedAndPartialCutCommand()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.Cut, Mode = CutMode.FeedAndPartial }));

        AssertContainsSequence(bytes, Bytes(0x1D, 0x56, 0x42, 0x03));
    }

    [Fact]
    public void RenderDoesNotWriteCutBytesWhenOptionsCutModeIsNone()
    {
        var bytes = _renderer.Render(JobWithOptions(
            new PrintJobOptions { Cut = true, CutMode = CutMode.None },
            new PrintContentCommand { Type = PrintCommandType.Text, Value = "Hello" }));

        AssertDoesNotContainAnyCutSequence(bytes);
    }

    [Fact]
    public void RenderWritesFullCutWhenLegacyCutIsTrueWithoutCutMode()
    {
        var bytes = _renderer.Render(JobWithOptions(
            new PrintJobOptions { Cut = true },
            new PrintContentCommand { Type = PrintCommandType.Text, Value = "Hello" }));

        AssertContainsSequence(bytes, Bytes(0x1D, 0x56, 0x00));
    }

    [Fact]
    public void RenderDoesNotWriteCutBytesWhenLegacyCutIsFalseWithoutCutMode()
    {
        var bytes = _renderer.Render(JobWithOptions(
            new PrintJobOptions { Cut = false },
            new PrintContentCommand { Type = PrintCommandType.Text, Value = "Hello" }));

        AssertDoesNotContainAnyCutSequence(bytes);
    }

    [Fact]
    public void RenderWritesDrawerKickCommand()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.OpenDrawer }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x70, 0x00, 0x32, 0xFA));
    }

    [Fact]
    public void RenderRejectsInvalidPaperWidth()
    {
        var request = JobWith(new PrintContentCommand { Type = PrintCommandType.Text, Value = "Hello" }) with
        {
            PaperWidth = (PaperWidth)(-1)
        };

        Assert.Throws<EscPosRenderException>(() => _renderer.Render(request));
    }

    [Fact]
    public void RenderRejectsInvalidPayload()
    {
        var request = JobWith(new PrintContentCommand { Type = PrintCommandType.Text });

        Assert.Throws<EscPosRenderException>(() => _renderer.Render(request));
    }

    [Fact]
    public void RenderTestReceiptIncludesAccentedCharacters()
    {
        var bytes = _renderer.RenderTestReceipt(new TestPrintRequest
        {
            PrinterName = "POS-80",
            PaperWidth = PaperWidth.Mm80,
            Cut = false
        }, DateTimeOffset.Parse("2026-06-08T00:00:00Z"));

        Assert.Contains((byte)0xE1, bytes);
        Assert.Contains((byte)0xF1, bytes);
    }

    private static PrintJobRequest JobWith(params PrintContentCommand[] commands) => new()
    {
        PrinterName = "POS-80",
        Format = "escpos",
        PaperWidth = PaperWidth.Mm80,
        Content = commands
    };

    private static PrintJobRequest JobWithOptions(PrintJobOptions options, params PrintContentCommand[] commands) => new()
    {
        PrinterName = "POS-80",
        Format = "escpos",
        PaperWidth = PaperWidth.Mm80,
        Options = options,
        Content = commands
    };

    private static byte[] Bytes(params int[] values) => values.Select(value => (byte)value).ToArray();

    private static void AssertContainsSequence(byte[] actual, byte[] expected)
    {
        for (var offset = 0; offset <= actual.Length - expected.Length; offset++)
        {
            if (actual.AsSpan(offset, expected.Length).SequenceEqual(expected))
            {
                return;
            }
        }

        Assert.Fail($"Expected byte sequence was not found: {Convert.ToHexString(expected)}");
    }

    private static void AssertDoesNotContainAnyCutSequence(byte[] actual)
    {
        AssertDoesNotContainSequence(actual, Bytes(0x1D, 0x56, 0x00));
        AssertDoesNotContainSequence(actual, Bytes(0x1D, 0x56, 0x01));
        AssertDoesNotContainSequence(actual, Bytes(0x1D, 0x56, 0x41, 0x03));
        AssertDoesNotContainSequence(actual, Bytes(0x1D, 0x56, 0x42, 0x03));
    }

    private static void AssertDoesNotContainSequence(byte[] actual, byte[] expected)
    {
        for (var offset = 0; offset <= actual.Length - expected.Length; offset++)
        {
            if (actual.AsSpan(offset, expected.Length).SequenceEqual(expected))
            {
                Assert.Fail($"Unexpected byte sequence was found: {Convert.ToHexString(expected)}");
            }
        }
    }
}
