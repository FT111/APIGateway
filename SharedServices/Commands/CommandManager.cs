using GatewayPluginContract;

namespace SharedServices.Commands;

public class CommandManager
{
    private readonly Dictionary<string, InternalContracts.CommandDefinition> _commands = new();
    
    public void ConfigurePluginManager(IPluginManager pluginManager)
    {
        pluginManager.AddPluginLoadStep(async plugin =>
        {
            var manifest = plugin.GetManifest();
            var pluginKey = manifest.Name + "/" + manifest.Version;
            foreach (var command in manifest.Commands)
            {
                // Transform MQSubmission to InternalCommandDefinition
                var internalCommand = new InternalContracts.CommandDefinition()
                {
                    PluginIdentifier = pluginKey,
                    Identifier = command.Identifier,
                    Handler =  command.Handler,
                };
                RegisterCommand(internalCommand);
            }
            await Task.CompletedTask;
        });
    }
    
    public void RegisterCommand(InternalContracts.CommandDefinition command)
    {
        var commandKey = command.PluginIdentifier + "/" + command.Identifier;
        if (_commands.ContainsKey(commandKey))
        {
            throw new InvalidOperationException($"{command.PluginIdentifier} is attempting to register command '{commandKey}', which is already registered by the plugin.");
        }
        _commands[commandKey] = command;
    }
}