using GatewayPluginContract;

namespace SharedServices;

public static class InternalContracts
{
    public class InternalCommandDefinition : MqCommandSubmission
    {
        public required string? PluginIdentifier { get; set; }
    }
}