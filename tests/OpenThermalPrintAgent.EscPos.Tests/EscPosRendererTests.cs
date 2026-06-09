using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.EscPos;
using System.Text;
using System.Text.Json;

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
    public void RenderWritesQrCodeCommands()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.QrCode, Value = "https://example.test" }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x61, 0x00));
        AssertContainsSequence(bytes, Bytes(0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00));
        AssertContainsSequence(bytes, Bytes(0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30));
    }

    [Fact]
    public void RenderWritesCode128BarcodeCommand()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.Barcode, BarcodeType = BarcodeType.Code128, Value = "ABC123" }));

        AssertContainsSequence(bytes, Bytes(0x1D, 0x6B, 0x49, 0x08, '{', 'B', 'A', 'B', 'C', '1', '2', '3'));
    }

    [Fact]
    public void RenderWritesRasterImageCommand()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand
            {
                Type = PrintCommandType.Image,
                Data = Convert.ToBase64String([0xFF, 0x00]),
                WidthBytes = 1,
                HeightDots = 2
            }));

        AssertContainsSequence(bytes, Bytes(0x1D, 0x76, 0x30, 0x00, 0x01, 0x00, 0x02, 0x00, 0xFF, 0x00));
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

    [Fact]
    public void RenderUsesLatin1EncodingByDefault()
    {
        var bytes = _renderer.Render(JobWith(
            new PrintContentCommand { Type = PrintCommandType.Text, Value = "á é í ó ú ñ" }));

        AssertContainsSequence(bytes, Encoding.Latin1.GetBytes("á é í ó ú ñ"));
    }

    [Fact]
    public void RenderUsesCp850EncodingWhenConfigured()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = _renderer.Render(JobWithOptions(
            new PrintJobOptions { EncodingProfile = EncodingProfile.Cp850 },
            new PrintContentCommand { Type = PrintCommandType.Text, Value = "á é í ó ú ñ" }));

        AssertContainsSequence(bytes, Encoding.GetEncoding(850).GetBytes("á é í ó ú ñ"));
    }

    [Fact]
    public void RenderUsesCp858EncodingWhenConfigured()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = _renderer.Render(JobWithOptions(
            new PrintJobOptions { EncodingProfile = EncodingProfile.Cp858 },
            new PrintContentCommand { Type = PrintCommandType.Text, Value = "Total 10€" }));

        AssertContainsSequence(bytes, Encoding.GetEncoding(858).GetBytes("Total 10€"));
    }

    [Fact]
    public void RenderReceiptWritesCenteredBoldTitle()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument { Title = "My Store" }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x61, 0x01));
        AssertContainsSequence(bytes, Bytes(0x1B, 0x45, 0x01));
        AssertContainsText(bytes, "My Store");
    }

    [Fact]
    public void RenderReceiptWritesCenteredSubtitle()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument { Subtitle = "Receipt" }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x61, 0x01));
        AssertContainsText(bytes, "Receipt");
    }

    [Fact]
    public void RenderReceiptWritesTextBlockLines()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock { Type = "text", Lines = Lines("Line one", "Line two") }
            ]
        }));

        AssertContainsText(bytes, "Line one");
        AssertContainsText(bytes, "Line two");
    }

    [Fact]
    public void RenderReceiptWritesTextBlockLabel()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock { Type = "text", Label = "Legal notice", Lines = Lines("Text") }
            ]
        }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x45, 0x01));
        AssertContainsText(bytes, "Legal notice");
        AssertContainsText(bytes, "Text");
    }

    [Fact]
    public void RenderReceiptAlignsKeyValueRows()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock
                {
                    Type = "keyValue",
                    Rows = [new ReceiptKeyValueRow { Label = "Date", Value = "2026-06-08" }]
                }
            ]
        }));

        AssertContainsText(bytes, KeyValueText("Date", "2026-06-08", EscPosRenderer.Mm80CharactersPerLine));
    }

    [Fact]
    public void RenderReceiptSeparatorFillsPaperWidth()
    {
        var eightyBytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks = [new ReceiptBlock { Type = "separator" }]
        }, PaperWidth.Mm80));
        var fiftyEightBytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks = [new ReceiptBlock { Type = "separator" }]
        }, PaperWidth.Mm58));

        AssertContainsText(eightyBytes, new string('-', EscPosRenderer.Mm80CharactersPerLine));
        AssertContainsText(fiftyEightBytes, new string('-', EscPosRenderer.Mm58CharactersPerLine));
    }

    [Fact]
    public void RenderReceiptItemsBlockWritesItemColumns()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock
                {
                    Type = "items",
                    Items = [new ReceiptItem { Name = "Coffee", Quantity = "2", UnitPrice = "$ 1.000", Total = "$ 2.000" }]
                }
            ]
        }));

        AssertContainsText(bytes, "Coffee");
        AssertContainsText(bytes, "2 $ 1.000");
        AssertContainsText(bytes, "$ 2.000");
    }

    [Fact]
    public void RenderReceiptWrapsLongItemNames()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock
                {
                    Type = "items",
                    Items = [new ReceiptItem { Name = "Croissant with a very long name that wraps", Total = "$ 1.500" }]
                }
            ]
        }, PaperWidth.Mm58));

        AssertContainsText(bytes, "Croissant with a very long name");
        AssertContainsText(bytes, "that wraps");
    }

    [Fact]
    public void RenderReceiptWritesItemComment()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock
                {
                    Type = "items",
                    Items = [new ReceiptItem { Name = "Coffee", Comment = "No sugar" }]
                }
            ]
        }));

        AssertContainsText(bytes, "  No sugar");
    }

    [Fact]
    public void RenderReceiptWritesBoldTotal()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock
                {
                    Type = "totals",
                    Rows = [new ReceiptKeyValueRow { Label = "TOTAL", Value = "$ 3.500", Bold = true }]
                }
            ]
        }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x45, 0x01));
        AssertContainsText(bytes, KeyValueText("TOTAL", "$ 3.500", EscPosRenderer.Mm80CharactersPerLine));
    }

    [Fact]
    public void RenderReceiptBlankBlockFeedsLines()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks = [new ReceiptBlock { Type = "blank", Lines = Number(3) }]
        }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x64, 0x03));
    }

    [Fact]
    public void RenderReceiptUsesDifferentPaperWidths()
    {
        var eightyBytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock
                {
                    Type = "keyValue",
                    Rows = [new ReceiptKeyValueRow { Label = "TOTAL", Value = "$ 1.000" }]
                }
            ]
        }, PaperWidth.Mm80));
        var fiftyEightBytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock
                {
                    Type = "keyValue",
                    Rows = [new ReceiptKeyValueRow { Label = "TOTAL", Value = "$ 1.000" }]
                }
            ]
        }, PaperWidth.Mm58));

        AssertContainsText(eightyBytes, KeyValueText("TOTAL", "$ 1.000", EscPosRenderer.Mm80CharactersPerLine));
        AssertContainsText(fiftyEightBytes, KeyValueText("TOTAL", "$ 1.000", EscPosRenderer.Mm58CharactersPerLine));
    }

    [Fact]
    public void RenderReceiptAppliesCutOptions()
    {
        var bytes = _renderer.Render(ReceiptJob(
            new ReceiptDocument { Title = "My Store" },
            PaperWidth.Mm80,
            new PrintJobOptions { Cut = true, CutMode = CutMode.Full }));

        AssertContainsSequence(bytes, Bytes(0x1D, 0x56, 0x00));
    }

    [Fact]
    public void RenderReceiptQrBlockWritesQrCodeCommands()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock { Type = "qr", Value = "https://example.test/receipt/123" }
            ]
        }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x61, 0x01));
        AssertContainsSequence(bytes, Bytes(0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00));
        AssertContainsSequence(bytes, Bytes(0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30));
    }

    [Theory]
    [InlineData(TextAlignment.Left, 0x00)]
    [InlineData(TextAlignment.Center, 0x01)]
    [InlineData(TextAlignment.Right, 0x02)]
    public void RenderReceiptQrBlockAppliesAlignment(TextAlignment alignment, int expectedValue)
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock { Type = "qr", Value = "https://example.test", Align = alignment }
            ]
        }));

        AssertContainsSequence(bytes, Bytes(0x1B, 0x61, expectedValue));
    }

    [Fact]
    public void RenderReceiptQrBlockAppliesSize()
    {
        var bytes = _renderer.Render(ReceiptJob(new ReceiptDocument
        {
            Blocks =
            [
                new ReceiptBlock { Type = "qr", Value = "https://example.test", Size = 8 }
            ]
        }));

        AssertContainsSequence(bytes, Bytes(0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x08));
    }

    [Fact]
    public void RenderReceiptMixedTextQrBlankAndCut()
    {
        var bytes = _renderer.Render(ReceiptJob(
            new ReceiptDocument
            {
                Blocks =
                [
                    new ReceiptBlock { Type = "text", Lines = Lines("Scan receipt"), Align = TextAlignment.Center },
                    new ReceiptBlock { Type = "qr", Value = "https://example.test/receipt/123", Size = 4 },
                    new ReceiptBlock { Type = "blank", Lines = Number(2) }
                ]
            },
            PaperWidth.Mm80,
            new PrintJobOptions { Cut = true, CutMode = CutMode.Full }));

        AssertContainsText(bytes, "Scan receipt");
        AssertContainsSequence(bytes, Bytes(0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x04));
        AssertContainsSequence(bytes, Bytes(0x1B, 0x64, 0x02));
        AssertContainsSequence(bytes, Bytes(0x1D, 0x56, 0x00));
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

    private static PrintJobRequest ReceiptJob(ReceiptDocument receipt, PaperWidth paperWidth = PaperWidth.Mm80, PrintJobOptions? options = null) => new()
    {
        PrinterName = "POS-80",
        Format = "receipt",
        PaperWidth = paperWidth,
        Options = options ?? new PrintJobOptions(),
        Receipt = receipt
    };

    private static JsonElement Lines(params string[] lines) => JsonSerializer.SerializeToElement(lines);

    private static JsonElement Number(int value) => JsonSerializer.SerializeToElement(value);

    private static string KeyValueText(string label, string value, int width)
    {
        return label + new string(' ', width - label.Length - value.Length) + value;
    }

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

    private static void AssertContainsText(byte[] actual, string expected)
    {
        AssertContainsSequence(actual, Encoding.Latin1.GetBytes(expected));
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
