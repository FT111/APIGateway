using GatewayPluginContract;
using GatewayPluginContract.MQ;

namespace SharedServices.Commands;

public class CommandManager
{
    private const string InternalProtectedString = "internal.";
    public readonly Dictionary<Contracts.MqCommandKey, InternalContracts.CommandDefinition> _commands = new();
    
    public void ConfigurePluginManager(IPluginManager pluginManager)
    {
        pluginManager.AddPluginLoadStep(async plugin =>
        {
            var manifest = plugin.GetManifest();
            var pluginKey = manifest.Name + manifest.Version;
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

    public InternalContracts.CommandDefinition GetCommand(Contracts.MqCommandKey commandKey)
    {
        return _commands[commandKey] ?? throw new KeyNotFoundException($"Command '{commandKey}' not found.");
    }
    
    public void RegisterCommand(InternalContracts.CommandDefinition command)
    {
        if (command.PluginIdentifier == null) throw new ArgumentNullException(nameof(command.PluginIdentifier));
        
        var commandKey = Contracts.MqCommandKey.New(command.PluginIdentifier, command.Identifier);
        if (commandKey.ToString().StartsWith(InternalProtectedString))
        {
            throw new InvalidOperationException($"{command.PluginIdentifier} is attempting to register command '{commandKey}', which is reserved for internal commands.");
        }
        if (_commands.ContainsKey(commandKey))
        {
            throw new InvalidOperationException($"{command.PluginIdentifier} is attempting to register command '{commandKey}', which is already registered by the plugin.");
        }
        _commands[commandKey] = command;
    }
    
    internal void RegisterInternalCommand(InternalContracts.CommandDefinition command)
    {
        var commandKey = Contracts.MqCommandKey.Internal(command.Identifier);
        if (!_commands.TryAdd(commandKey, command))
        {
            throw new InvalidOperationException($"Attempting to register internal command '{commandKey}', which is already registered.");
        }
    }
}