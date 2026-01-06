namespace SharedServices;

public class CommandManager
{
    private readonly Dictionary<string, InternalContracts.InternalCommandDefinition> _commands = new();
    
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