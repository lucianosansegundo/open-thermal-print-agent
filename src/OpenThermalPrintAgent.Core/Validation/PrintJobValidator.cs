using System.Text.Json;
using OpenThermalPrintAgent.Core.Errors;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Core.Validation;

public static class PrintJobValidator
{
    public const int MaxContentItems = 500;
    public const int MaxTextLength = 2048;
    public const int MaxCopies = 10;
    public const int MaxQrCodeLength = 1024;
    public const int MinQrCodeSize = 1;
    public const int MaxQrCodeSize = 16;
    public const int MaxBarcodeLength = 128;
    public const int MaxRasterBytes = 64 * 1024;
    public const int MaxReceiptBlocks = 200;
    public const int MaxReceiptRows = 100;
    public const int MaxReceiptItems = 100;

    public static AgentError? Validate(PrintJobRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.PrinterName))
        {
            errors.Add("printerName is required.");
        }

        if (!string.Equals(request.Format, "escpos", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Format, "receipt", StringComparison.OrdinalIgnoreCase))
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

        if (string.Equals(request.Format, "receipt", StringComparison.OrdinalIgnoreCase))
        {
            ValidateReceipt(request.Receipt, errors);
        }
        else
        {
            ValidateEscPosContent(request.Content, errors);
        }

        return errors.Count == 0 ? null : AgentError.InvalidPayload(errors.ToArray());
    }

    private static void ValidateEscPosContent(IReadOnlyList<PrintContentCommand> content, List<string> errors)
    {
        if (content.Count == 0)
        {
            errors.Add("content must include at least one command.");
        }

        if (content.Count > MaxContentItems)
        {
            errors.Add($"content must include at most {MaxContentItems} commands.");
        }

        for (var index = 0; index < content.Count; index++)
        {
            ValidateCommand(content[index], index, errors);
        }
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
                ValidateQrSize(command.Size, $"content[{index}].size", errors);
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

    private static void ValidateReceipt(ReceiptDocument? receipt, List<string> errors)
    {
        if (receipt is null)
        {
            errors.Add("receipt is required when format is receipt.");
            return;
        }

        ValidateOptionalText(receipt.Title, "receipt.title", MaxTextLength, errors);
        ValidateOptionalText(receipt.Subtitle, "receipt.subtitle", MaxTextLength, errors);

        if (string.IsNullOrWhiteSpace(receipt.Title) &&
            string.IsNullOrWhiteSpace(receipt.Subtitle) &&
            receipt.Blocks.Count == 0)
        {
            errors.Add("receipt must include title, subtitle, or at least one block.");
        }

        if (receipt.Blocks.Count > MaxReceiptBlocks)
        {
            errors.Add($"receipt.blocks must include at most {MaxReceiptBlocks} blocks.");
        }

        for (var index = 0; index < receipt.Blocks.Count; index++)
        {
            ValidateReceiptBlock(receipt.Blocks[index], index, errors);
        }
    }

    private static void ValidateReceiptBlock(ReceiptBlock block, int index, List<string> errors)
    {
        if (block.Align is not null && !Enum.IsDefined(block.Align.Value))
        {
            errors.Add($"receipt.blocks[{index}].align is invalid.");
        }

        switch (block.Type.Trim().ToLowerInvariant())
        {
            case "text":
                ValidateOptionalText(block.Label, $"receipt.blocks[{index}].label", MaxTextLength, errors);
                ValidateStringArrayLines(block.Lines, $"receipt.blocks[{index}].lines", errors);
                break;

            case "keyvalue":
            case "totals":
                ValidateRows(block.Rows, $"receipt.blocks[{index}].rows", errors);
                break;

            case "separator":
                if (block.SeparatorChar is not null && block.SeparatorChar.Length != 1)
                {
                    errors.Add($"receipt.blocks[{index}].char must be exactly one character.");
                }

                break;

            case "items":
                ValidateItems(block.Items, $"receipt.blocks[{index}].items", errors);
                break;

            case "blank":
                ValidateBlankLines(block.Lines, $"receipt.blocks[{index}].lines", errors);
                break;

            case "qr":
                ValidateRequiredText(block.Value, $"receipt.blocks[{index}].value", MaxQrCodeLength, errors);
                ValidateQrSize(block.Size, $"receipt.blocks[{index}].size", errors);
                break;

            default:
                errors.Add($"receipt.blocks[{index}].type is invalid.");
                break;
        }
    }

    private static void ValidateRows(IReadOnlyList<ReceiptKeyValueRow> rows, string fieldName, List<string> errors)
    {
        if (rows.Count == 0)
        {
            errors.Add($"{fieldName} must include at least one row.");
            return;
        }

        if (rows.Count > MaxReceiptRows)
        {
            errors.Add($"{fieldName} must include at most {MaxReceiptRows} rows.");
        }

        for (var index = 0; index < rows.Count; index++)
        {
            ValidateRequiredText(rows[index].Label, $"{fieldName}[{index}].label", MaxTextLength, errors);
            ValidateRequiredText(rows[index].Value, $"{fieldName}[{index}].value", MaxTextLength, errors);
        }
    }

    private static void ValidateItems(IReadOnlyList<ReceiptItem> items, string fieldName, List<string> errors)
    {
        if (items.Count == 0)
        {
            errors.Add($"{fieldName} must include at least one item.");
            return;
        }

        if (items.Count > MaxReceiptItems)
        {
            errors.Add($"{fieldName} must include at most {MaxReceiptItems} items.");
        }

        for (var index = 0; index < items.Count; index++)
        {
            ValidateRequiredText(items[index].Name, $"{fieldName}[{index}].name", MaxTextLength, errors);
            ValidateOptionalText(items[index].Quantity, $"{fieldName}[{index}].quantity", MaxTextLength, errors);
            ValidateOptionalText(items[index].UnitPrice, $"{fieldName}[{index}].unitPrice", MaxTextLength, errors);
            ValidateOptionalText(items[index].Total, $"{fieldName}[{index}].total", MaxTextLength, errors);
            ValidateOptionalText(items[index].Comment, $"{fieldName}[{index}].comment", MaxTextLength, errors);
        }
    }

    private static void ValidateStringArrayLines(JsonElement? lines, string fieldName, List<string> errors)
    {
        if (lines is null || lines.Value.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{fieldName} must be an array of strings.");
            return;
        }

        var count = 0;
        foreach (var line in lines.Value.EnumerateArray())
        {
            if (line.ValueKind != JsonValueKind.String)
            {
                errors.Add($"{fieldName}[{count}] must be a string.");
            }
            else
            {
                ValidateRequiredText(line.GetString(), $"{fieldName}[{count}]", MaxTextLength, errors);
            }

            count++;
        }

        if (count == 0)
        {
            errors.Add($"{fieldName} must include at least one line.");
        }
    }

    private static void ValidateBlankLines(JsonElement? lines, string fieldName, List<string> errors)
    {
        if (lines is null)
        {
            return;
        }

        if (lines.Value.ValueKind != JsonValueKind.Number || !lines.Value.TryGetInt32(out var value) || value is < 1 or > 20)
        {
            errors.Add($"{fieldName} must be between 1 and 20.");
        }
    }

    private static void ValidateQrSize(int? size, string fieldName, List<string> errors)
    {
        if (size is not null and (< MinQrCodeSize or > MaxQrCodeSize))
        {
            errors.Add($"{fieldName} must be between {MinQrCodeSize} and {MaxQrCodeSize}.");
        }
    }

    private static void ValidateRequiredText(string? value, string fieldName, int maxLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
        else
        {
            ValidateOptionalText(value, fieldName, maxLength, errors);
        }
    }

    private static void ValidateOptionalText(string? value, string fieldName, int maxLength, List<string> errors)
    {
        if (value is not null && value.Length > maxLength)
        {
            errors.Add($"{fieldName} must be at most {maxLength} characters.");
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
