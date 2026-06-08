using OpenThermalPrintAgent.Core.Errors;
using OpenThermalPrintAgent.Core.Models;

namespace OpenThermalPrintAgent.Core.Validation;

public static class PrintJobValidator
{
    public const int MaxContentItems = 500;
    public const int MaxTextLength = 2048;
    public const int MaxCopies = 10;

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

        return errors.Count == 0 ? null : AgentError.InvalidPayload(errors.ToArray());
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
        }
    }
}
