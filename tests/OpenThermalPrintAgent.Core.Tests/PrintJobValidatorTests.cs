using OpenThermalPrintAgent.Core.Errors;
using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.Core.Validation;

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
}
