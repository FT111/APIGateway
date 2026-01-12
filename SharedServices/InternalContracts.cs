using GatewayPluginContract;

namespace SharedServices;

public static class InternalContracts
{
    public class CommandDefinition : MqCommandSubmission
    {
        public required string? PluginIdentifier { get; set; }
    }
}