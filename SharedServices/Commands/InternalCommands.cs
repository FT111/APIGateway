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
            .SelectMany(assembly => assembly.GetTypes());
        commandDefinitions = commandDefinitions
            .Where(type => !type.IsAbstract && type.IsAssignableTo(internalCommandType))
            .ToList();


        foreach (var commandDefinition in commandDefinitions)
        {
            if (commandDefinition.Name == "CommandDefinition")
            {
                continue; // Skip the base CommandDefinition class
            }
            if (Activator.CreateInstance(commandDefinition) is InternalContracts.CommandDefinition commandInstance)
            {
                commandManager.RegisterInternalCommand(commandInstance);
            }
            else
            {
                throw new InvalidOperationException($"Failed to create instance of internal command definition: {commandDefinition.FullName}");
            }
        }
    }
}