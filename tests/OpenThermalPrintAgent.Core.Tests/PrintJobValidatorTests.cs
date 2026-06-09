using OpenThermalPrintAgent.Core.Errors;
using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.Core.Validation;
using System.Text.Json;

namespace OpenThermalPrintAgent.Core.Tests;

public sealed class PrintJobValidatorTests
{
    [Fact]
    public void ValidateReturnsNullForValidEscPosJob()
    {
        var request = ValidRequest();

        var error = PrintJobValidator.Validate(request);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateRejectsUnsupportedFormat()
    {
        var request = ValidRequest() with { Format = "pdf" };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.UnsupportedFormat, error.Code);
    }

    [Fact]
    public void ValidateRejectsMissingPrinterName()
    {
        var request = ValidRequest() with { PrinterName = "" };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("printerName", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsInvalidPaperWidth()
    {
        var request = ValidRequest() with { PaperWidth = (PaperWidth)(-1) };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("paperWidth", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsInvalidTextCommand()
    {
        var request = ValidRequest() with
        {
            Content = [new PrintContentCommand { Type = PrintCommandType.Text }]
        };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("value", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsInvalidOptionsCutMode()
    {
        var request = ValidRequest() with
        {
            Options = new PrintJobOptions { CutMode = (CutMode)(-1) }
        };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("options.cutMode", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsInvalidCommandCutMode()
    {
        var request = ValidRequest() with
        {
            Content = [new PrintContentCommand { Type = PrintCommandType.Cut, Mode = (CutMode)(-1) }]
        };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("content[0].mode", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsInvalidTestPrintCutMode()
    {
        var request = new TestPrintRequest
        {
            PrinterName = "POS-80",
            PaperWidth = PaperWidth.Mm80,
            CutMode = (CutMode)(-1)
        };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("cutMode", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsInvalidEncodingProfile()
    {
        var request = ValidRequest() with
        {
            Options = new PrintJobOptions { EncodingProfile = (EncodingProfile)(-1) }
        };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("options.encodingProfile", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsBarcodeWithoutBarcodeType()
    {
        var request = ValidRequest() with
        {
            Content = [new PrintContentCommand { Type = PrintCommandType.Barcode, Value = "ABC123" }]
        };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("barcodeType", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsRasterImageWithInvalidBase64()
    {
        var request = ValidRequest() with
        {
            Content =
            [
                new PrintContentCommand
                {
                    Type = PrintCommandType.Image,
                    Data = "not-base64",
                    WidthBytes = 1,
                    HeightDots = 1
                }
            ]
        };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("valid base64", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsRasterImageWithWrongDataLength()
    {
        var request = ValidRequest() with
        {
            Content =
            [
                new PrintContentCommand
                {
                    Type = PrintCommandType.Image,
                    Data = Convert.ToBase64String([0xFF]),
                    WidthBytes = 2,
                    HeightDots = 2
                }
            ]
        };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("widthBytes * heightDots", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateReturnsNullForValidReceiptJob()
    {
        var request = ValidReceiptRequest();

        var error = PrintJobValidator.Validate(request);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateRejectsMissingReceiptForReceiptFormat()
    {
        var request = ValidRequest() with { Format = "receipt", Content = [] };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("receipt is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsEmptyReceipt()
    {
        var request = ValidReceiptRequest() with
        {
            Receipt = new ReceiptDocument()
        };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("title, subtitle, or at least one block", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsUnknownReceiptBlockType()
    {
        var request = ValidReceiptRequest() with
        {
            Receipt = new ReceiptDocument
            {
                Blocks =
                [
                    new ReceiptBlock { Type = "unknown" }
                ]
            }
        };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("receipt.blocks[0].type", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsReceiptTextBlockWithoutLines()
    {
        var request = ValidReceiptRequest() with
        {
            Receipt = new ReceiptDocument
            {
                Blocks =
                [
                    new ReceiptBlock { Type = "text" }
                ]
            }
        };

        var error = PrintJobValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidPayload, error.Code);
        Assert.Contains(error.Details, detail => detail.Contains("receipt.blocks[0].lines", StringComparison.Ordinal));
    }

    private static PrintJobRequest ValidRequest() => new()
    {
        PrinterName = "POS-80",
        Format = "escpos",
        PaperWidth = PaperWidth.Mm80,
        Options = new PrintJobOptions { Copies = 1 },
        Content =
        [
            new PrintContentCommand
            {
                Type = PrintCommandType.Text,
                Value = "Hello",
                Align = TextAlignment.Left
            }
        ]
    };

    private static PrintJobRequest ValidReceiptRequest() => new()
    {
        PrinterName = "POS-80",
        Format = "receipt",
        PaperWidth = PaperWidth.Mm80,
        Options = new PrintJobOptions { Copies = 1 },
        Receipt = new ReceiptDocument
        {
            Title = "My Store",
            Blocks =
            [
                new ReceiptBlock
                {
                    Type = "text",
                    Lines = JsonSerializer.SerializeToElement(new[] { "Hello" })
                }
            ]
        }
    };
}
