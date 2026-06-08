using OpenThermalPrintAgent.Core.Errors;

namespace OpenThermalPrintAgent.EscPos;

public sealed class EscPosRenderException : Exception
{
    public EscPosRenderException(AgentError error)
        : base(error.Message)
    {
        Error = error;
    }

    public AgentError Error { get; }
}
