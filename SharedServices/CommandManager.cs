namespace SharedServices;

public class CommandManager
{
    private readonly Dictionary<string, InternalContracts.InternalCommandDefinition> _commands = new();
    
    public void ConfigurePluginManager(InternalContracts.IPluginManager pluginManager)
    {
        pluginManager.AddPluginLoadStep(async plugin =>
        {
            var manifest = plugin.GetManifest();
            var pluginKey = manifest.Name + "/" + manifest.Version;
            foreach (var command in manifest.Commands)
            {
                // Transform MQSubmission to InternalCommandDefinition
                var internalCommand = new InternalContracts.InternalCommandDefinition()
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
    
    public void RegisterCommand(InternalContracts.InternalCommandDefinition command)
    {
        var commandKey = command.PluginIdentifier + "/" + command.Identifier;
        if (_commands.ContainsKey(commandKey))
        {
            throw new InvalidOperationException($"{command.PluginIdentifier} is attempting to register command '{commandKey}', which is already registered by the plugin.");
        }
        _commands[commandKey] = command;
    }
}