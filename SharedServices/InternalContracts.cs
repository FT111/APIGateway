using GatewayPluginContract;

namespace SharedServices;

public static class InternalContracts
{
    public class CommandDefinition : MqCommandSubmission
    {
        public string? PluginIdentifier { get; set; }
    }
}