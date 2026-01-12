namespace SharedServices.Commands;

public static class InternalCommands
{
    /// <summary>
    /// Uses reflection to populate the CommandManager with internal command definitions.
    /// Internal commands are packaged in SharedServices.Commands.Internal
    /// </summary>
    public static void ConfigureCommandManager(CommandManager commandManager)
    {
        var internalCommandType = typeof(InternalContracts.CommandDefinition);
        var commandDefinitions = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.GetCustomAttributes(internalCommandType, true).Length > 0)
            .ToList();

        foreach (var commandDefinition in commandDefinitions)
        {
            if (Activator.CreateInstance(commandDefinition) is InternalContracts.CommandDefinition commandInstance)
            {
                commandManager.RegisterCommand(commandInstance);
            }
            else
            {
                throw new InvalidOperationException($"Failed to create instance of internal command definition: {commandDefinition.FullName}");
            }
        }
    }
}