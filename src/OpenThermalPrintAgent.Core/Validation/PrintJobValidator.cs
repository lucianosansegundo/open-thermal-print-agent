using OpenThermalPrintAgent.Core.Errors;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Core.Validation;

public static class PrintJobValidator
{
    public const int MaxContentItems = 500;
    public const int MaxTextLength = 2048;
    public const int MaxCopies = 10;
    public const int MaxQrCodeLength = 1024;
    public const int MaxBarcodeLength = 128;
    public const int MaxRasterBytes = 64 * 1024;

    public static AgentError? Validate(PrintJobRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.PrinterName))
        {
            errors.Add("printerName is required.");
        }

        if (!string.Equals(request.Format, "escpos", StringComparison.OrdinalIgnoreCase))
        {
            return AgentError.UnsupportedFormat(request.Format);
        }

        ValidatePaperWidth(request.PaperWidth, errors);
        ValidateCutMode(request.Options.CutMode, "options.cutMode", errors);
        ValidateEncodingProfile(request.Options.EncodingProfile, "options.encodingProfile", errors);

        if (request.Options.Copies is < 1 or > MaxCopies)
        {
            errors.Add($"options.copies must be between 1 and {MaxCopies}.");
        }

        if (request.Content.Count == 0)
        {
            errors.Add("content must include at least one command.");
        }

        if (request.Content.Count > MaxContentItems)
        {
            errors.Add($"content must include at most {MaxContentItems} commands.");
        }

        for (var index = 0; index < request.Content.Count; index++)
        {
            ValidateCommand(request.Content[index], index, errors);
        }

        return errors.Count == 0 ? null : AgentError.InvalidPayload(errors.ToArray());
    }

    public static AgentError? Validate(TestPrintRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.PrinterName))
        {
            errors.Add("printerName is required.");
        }

        ValidatePaperWidth(request.PaperWidth, errors);
        ValidateCutMode(request.CutMode, "cutMode", errors);
        ValidateEncodingProfile(request.EncodingProfile, "encodingProfile", errors);

        return errors.Count == 0 ? null : AgentError.InvalidPayload(errors.ToArray());
    }

    private static void ValidateCutMode(CutMode? cutMode, string fieldName, List<string> errors)
    {
        if (cutMode is not null && !Enum.IsDefined(cutMode.Value))
        {
            errors.Add($"{fieldName} must be none, full, partial, feedAndFull, or feedAndPartial.");
        }
    }

    private static void ValidateEncodingProfile(EncodingProfile? encodingProfile, string fieldName, List<string> errors)
    {
        if (encodingProfile is not null && !Enum.IsDefined(encodingProfile.Value))
        {
            errors.Add($"{fieldName} must be latin1, cp850, or cp858.");
        }
    }

    private static void ValidatePaperWidth(PaperWidth paperWidth, List<string> errors)
    {
        if (!Enum.IsDefined(paperWidth))
        {
            errors.Add("paperWidth must be 58mm or 80mm.");
        }
    }

    private static void ValidateCommand(PrintContentCommand command, int index, List<string> errors)
    {
        if (!Enum.IsDefined(command.Type))
        {
            errors.Add($"content[{index}].type is invalid.");
            return;
        }

        if (command.Align is not null && !Enum.IsDefined(command.Align.Value))
        {
            errors.Add($"content[{index}].align is invalid.");
        }

        ValidateCutMode(command.Mode, $"content[{index}].mode", errors);

        if (command.BarcodeType is not null && !Enum.IsDefined(command.BarcodeType.Value))
        {
            errors.Add($"content[{index}].barcodeType is invalid.");
        }

        switch (command.Type)
        {
            case PrintCommandType.Text:
                if (string.IsNullOrEmpty(command.Value))
                {
                    errors.Add($"content[{index}].value is required for text commands.");
                }
                else if (command.Value.Length > MaxTextLength)
                {
                    errors.Add($"content[{index}].value must be at most {MaxTextLength} characters.");
                }

                break;

            case PrintCommandType.Feed:
                if (command.Lines is null or < 1 or > 20)
                {
                    errors.Add($"content[{index}].lines must be between 1 and 20 for feed commands.");
                }

                break;

            case PrintCommandType.QrCode:
                ValidateRequiredValue(command, index, "QR code", MaxQrCodeLength, errors);
                break;

            case PrintCommandType.Barcode:
                ValidateRequiredValue(command, index, "barcode", MaxBarcodeLength, errors);
                if (command.BarcodeType is null)
                {
                    errors.Add($"content[{index}].barcodeType is required for barcode commands.");
                }

                break;

            case PrintCommandType.Image:
                ValidateRasterImage(command, index, errors);
                break;
        }
    }

    private static void ValidateRequiredValue(PrintContentCommand command, int index, string commandName, int maxLength, List<string> errors)
    {
        if (string.IsNullOrEmpty(command.Value))
        {
            errors.Add($"content[{index}].value is required for {commandName} commands.");
        }
        else if (command.Value.Length > maxLength)
        {
            errors.Add($"content[{index}].value must be at most {maxLength} characters for {commandName} commands.");
        }
    }

    private static void ValidateRasterImage(PrintContentCommand command, int index, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(command.Data))
        {
            errors.Add($"content[{index}].data is required for image commands.");
            return;
        }

        if (command.WidthBytes is null or < 1 or > 255)
        {
            errors.Add($"content[{index}].widthBytes must be between 1 and 255 for image commands.");
        }

        if (command.HeightDots is null or < 1 or > 4095)
        {
            errors.Add($"content[{index}].heightDots must be between 1 and 4095 for image commands.");
        }

        try
        {
            var bytes = Convert.FromBase64String(command.Data);
            if (bytes.Length > MaxRasterBytes)
            {
                errors.Add($"content[{index}].data must decode to at most {MaxRasterBytes} bytes.");
            }

            if (command.WidthBytes is not null && command.HeightDots is not null &&
                bytes.Length != command.WidthBytes.Value * command.HeightDots.Value)
            {
                errors.Add($"content[{index}].data length must equal widthBytes * heightDots.");
            }
        }
        catch (FormatException)
        {
            errors.Add($"content[{index}].data must be valid base64 for image commands.");
        }
    }
}
