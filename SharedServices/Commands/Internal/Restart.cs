using System.Diagnostics;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace SharedServices.Commands.Internal;

public class Restart : InternalContracts.CommandDefinition
{
    public Restart()
    {
        PluginIdentifier = "internal";
        Identifier = "instance.restart";
        Handler = async (gateway, param) => await Handle(gateway, param);
    }


    public async Task Handle(GatewayBase gateway, string? param)
    {
        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        var newProcess = new System.Diagnostics.ProcessStartInfo
        {
            FileName = currentProcess.MainModule?.FileName ??
                       throw new InvalidOperationException("Cannot determine current process file name"),
            Arguments = string.Join(' ', Environment.GetCommandLineArgs().Skip(1)),
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        };
        System.Diagnostics.Process.Start(newProcess);
        await Task.Delay(1000); // Give the new process a moment to start
        Environment.Exit(0);
    }
}