using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace  Lecti;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GatewayPluginContract;

/// <summary>
/// A/B testing plugin for Gateway
/// </summary>

public class Lecti : IPlugin
{
    private PluginManifest _manifest = new PluginManifest
    {
        Name = "Lecti",
        Version = 0.1,
        Description = "A/B testing plugin for Gateway",
        Author = "FT111",
    };

    public PluginManifest GetManifest()
    {
        return _manifest;
    }

    public void InitialiseServiceConfiguration(DbContext context,
        Func<Func<PluginConfigDefinition, PluginConfigDefinition>, Task> addConfig)
    {
        addConfig(definition =>
        {
            definition.Key = "downstream_variants";
            definition.DefaultValue = "[]";
            definition.ValueType = "json";
            definition.Internal = false;
            definition.ConstraintDescription = "Must be a target ID";
            
            definition.ValueConstraint = s =>
            {
                try
                {
                    var targetList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(s);
                    var parsedTargets = new HashSet<Guid>();
                    if (targetList == null)
                    {
                        return true;
                    }
                    foreach (var targetId in targetList)
                    {
                        if (!Guid.TryParse(targetId, out var targetGuid))
                        {
                            return false;
                        }

                        try
                        {
                            parsedTargets.Add(targetGuid);
                        }
                        catch (Exception e)
                        {
                            return false;
                        }
                    }

                    var matchingTargets = context.Set<Target>().Where(target => parsedTargets.Contains(target.Id)).ToArray().Length;
                    Console.WriteLine("matching targets:::: " + matchingTargets);
                    return matchingTargets == parsedTargets.Count;
                }
                catch (Exception e)
                {
                    return false;
                }
            };
            return definition;
        });
    }

    public void ConfigurePluginRegistrar(IPluginServiceRegistrar registrar)
    {
        registrar.RegisterService<Selector>(this, new Selector(), ServiceTypes.PreProcessor);
        registrar.RegisterService<Checker>(this, new Checker(), ServiceTypes.PostProcessor);
    }

    public void ConfigureDataRegistries(PluginCache cache)
    {
        cache.Register(
            "assignedTargets",
            new CachedData<IQueryable<PluginData>>
            {
                InvalidationFrequency = TimeSpan.FromSeconds(5),
                Fetch = (ctx) =>
                    Task.FromResult(ctx.Set<PluginData>().Where(dt => dt.Namespace == "Lecti").AsQueryable())
            });
        
        cache.Register(
            "storedTargets",
            new CachedData<IQueryable<Target>>
            {
                InvalidationFrequency = TimeSpan.FromMinutes(1),
                Fetch = (ctx) => Task.FromResult(ctx.Set<Target>().AsQueryable())
            });
        // tel.RegisterDataCard(new DataCard<Visualisation.PieChartModel>
        // {
        //     Name = "Lecti A/B Test Results",
        //     Description = "Shows the current distribution of A/B test targets.",
        //     GetData = (repoFactory) =>
        //     {
        //         var assignments = repoFactory.GetRepo<PluginData>()
        //             .QueryAsync((d) => d.Namespace == _manifest.Name && d.Category == "ipTargets").Result;
        //         
        //         var targetTotals = new Dictionary<string, double>();
        //         foreach (var assignment in assignments)
        //         {
        //             if (assignment.Value == null) continue;
        //             if (!double.TryParse(assignment.Value, out var value)) continue;
        //
        //             if (!targetTotals.TryAdd(assignment.Key, 0))
        //             {
        //                 targetTotals[assignment.Key] += 1;
        //             }
        //         }
        //         // Normalize the totals to be between 0 and 1
        //         var total = targetTotals.Values.Sum();
        //         if (total == 0) return new Visualisation.PieChartModel { Segments = new Dictionary<string, double>() };
        //         
        //         var segments = new Dictionary<string, double>();
        //         foreach (var kvp in targetTotals)
        //         {
        //             segments[kvp.Key] = kvp.Value / total;
        //         }
        //         
        //         return new Visualisation.PieChartModel
        //         {
        //             Segments = segments
        //         };
        //     }
        // });
    }

    public Dictionary<ServiceTypes, IService[]> GetServices()
    {
        return new Dictionary<ServiceTypes, IService[]>
        {
            { ServiceTypes.PreProcessor, [new Selector()] },
            { ServiceTypes.PostProcessor, [new Checker()] },
            { ServiceTypes.Forwarder, [] }
        };
    }
}