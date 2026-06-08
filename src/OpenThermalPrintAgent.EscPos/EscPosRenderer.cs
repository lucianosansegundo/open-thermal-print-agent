using System.Text;
using OpenThermalPrintAgent.Core.Models;
using OpenThermalPrintAgent.Core.Validation;

namespace OpenThermalPrintAgent.EscPos;

public sealed class EscPosRenderer
{
    private static readonly byte[] Initialize = [0x1B, 0x40];
    private static readonly byte[] BoldOn = [0x1B, 0x45, 0x01];
    private static readonly byte[] BoldOff = [0x1B, 0x45, 0x00];
    private static readonly byte[] FullCut = [0x1D, 0x56, 0x00];
    private static readonly byte[] PartialCut = [0x1D, 0x56, 0x01];
    private static readonly byte[] FeedAndFullCut = [0x1D, 0x56, 0x41, 0x03];
    private static readonly byte[] FeedAndPartialCut = [0x1D, 0x56, 0x42, 0x03];
    private static readonly byte[] DrawerKick = [0x1B, 0x70, 0x00, 0x32, 0xFA];

    public byte[] Render(PrintJobRequest request)
    {
        var error = PrintJobValidator.Validate(request);
        if (error is not null)
        {
            throw new EscPosRenderException(error);
        }

        using var output = new MemoryStream();
        Write(output, Initialize);

        var copies = Math.Max(1, request.Options.Copies);
        for (var copy = 0; copy < copies; copy++)
        {
            if (request.Options.OpenDrawer)
            {
                Write(output, DrawerKick);
            }

            foreach (var command in request.Content)
            {
                WriteCommand(output, command);
            }

            WriteCut(output, ResolveOptionsCutMode(request.Options));
        }

        return output.ToArray();
    }

    public byte[] RenderTestReceipt(TestPrintRequest request, DateTimeOffset now)
    {
        var error = PrintJobValidator.Validate(request);
        if (error is not null)
        {
            throw new EscPosRenderException(error);
        }

        var job = new PrintJobRequest
        {
            PrinterName = request.PrinterName,
            PaperWidth = request.PaperWidth,
            Options = new PrintJobOptions
            {
                Cut = request.Cut,
                CutMode = request.CutMode,
                OpenDrawer = request.OpenDrawer,
                Copies = 1
            },
            Content =
            [
                new PrintContentCommand { Type = PrintCommandType.Text, Value = "Open Thermal Print Agent", Align = TextAlignment.Center, Bold = true },
                new PrintContentCommand { Type = PrintCommandType.Text, Value = "Test receipt", Align = TextAlignment.Center, Bold = false },
                new PrintContentCommand { Type = PrintCommandType.Feed, Lines = 1 },
                new PrintContentCommand { Type = PrintCommandType.Text, Value = $"Printed at: {now:O}", Align = TextAlignment.Left },
                new PrintContentCommand { Type = PrintCommandType.Text, Value = "Accents: \u00e1 \u00e9 \u00ed \u00f3 \u00fa \u00f1", Align = TextAlignment.Left },
                new PrintContentCommand { Type = PrintCommandType.Text, Value = "------------------------", Align = TextAlignment.Left },
                new PrintContentCommand { Type = PrintCommandType.Text, Value = "Sample item      $ 1.000", Align = TextAlignment.Left },
                new PrintContentCommand { Type = PrintCommandType.Text, Value = "TOTAL            $ 1.000", Align = TextAlignment.Left, Bold = true },
                new PrintContentCommand { Type = PrintCommandType.Feed, Lines = 3 }
            ]
        };

        return Render(job);
    }

    private static void WriteCommand(Stream output, PrintContentCommand command)
    {
        switch (command.Type)
        {
            case PrintCommandType.Text:
                WriteAlignment(output, command.Align ?? TextAlignment.Left);

                if (command.Bold.HasValue)
                {
                    Write(output, command.Bold.Value ? BoldOn : BoldOff);
                }

                WriteText(output, command.Value ?? string.Empty);
                output.WriteByte(0x0A);
                break;

            case PrintCommandType.Feed:
                WriteFeed(output, command.Lines ?? 1);
                break;

            case PrintCommandType.Cut:
                WriteCut(output, command.Mode ?? CutMode.Partial);
                break;

            case PrintCommandType.OpenDrawer:
                Write(output, DrawerKick);
                break;
        }
    }

    private static CutMode ResolveOptionsCutMode(PrintJobOptions options)
    {
        if (options.CutMode is not null)
        {
            return options.CutMode.Value;
        }

        return options.Cut ? CutMode.Full : CutMode.None;
    }

    private static void WriteCut(Stream output, CutMode mode)
    {
        switch (mode)
        {
            case CutMode.None:
                break;
            case CutMode.Full:
                Write(output, FullCut);
                break;
            case CutMode.Partial:
                Write(output, PartialCut);
                break;
            case CutMode.FeedAndFull:
                Write(output, FeedAndFullCut);
                break;
            case CutMode.FeedAndPartial:
                Write(output, FeedAndPartialCut);
                break;
        }
    }

    private static void WriteAlignment(Stream output, TextAlignment alignment)
    {
        var value = alignment switch
        {
            TextAlignment.Left => 0x00,
            TextAlignment.Center => 0x01,
            TextAlignment.Right => 0x02,
            _ => 0x00
        };

        Write(output, [0x1B, 0x61, (byte)value]);
    }

    private static void WriteFeed(Stream output, int lines)
    {
        Write(output, [0x1B, 0x64, (byte)lines]);
    }

    private static void WriteText(Stream output, string text)
    {
        var bytes = Encoding.Latin1.GetBytes(text);
        output.Write(bytes);
    }

    private static void Write(Stream output, ReadOnlySpan<byte> bytes)
    {
        output.Write(bytes);
    }
}
