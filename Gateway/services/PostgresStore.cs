using System.Data;
using GatewayPluginContract;
using Npgsql;
using Npgsql.Schema;

namespace Gateway.services;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

public class PostgresStore : IStore
{
    private readonly NpgsqlDataSourceBuilder _dbFactory;
    private readonly NpgsqlDataSource _db;
    public PostgresStore(IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Postgres") ??
                               throw new InvalidOperationException("Connection string 'Postgres' not found in configuration.");
        
        // Connects to a database using Postgres
        _dbFactory = new NpgsqlDataSourceBuilder(connectionString);
        _dbFactory.MapEnum<ServiceFailurePolicies>("public.servicefailurepolicies");
        _db = _dbFactory.Build();

        if(!_db.OpenConnection().CanCreateBatch)
        {
            throw new InvalidOperationException("Failed to connect to the database.");
        }
        Console.WriteLine("Connected to database");
    }

    public async Task<T> GetAsync<T>(string key, string? scope = null) where T : notnull
    {
        using var command =
            _db.CreateCommand("SELECT value, type FROM plugindata WHERE key = @key AND namespace = @scope");
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("scope", scope ?? "global");
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var value = reader.GetString(0);
            var type = reader.GetString(1);

            return (T)(object)value;
        }
        throw new KeyNotFoundException($"Key '{key}' not found in scope '{scope ?? "global"}'.");
    }

    public async Task SetAsync<T>(string key, T value, string type, string? scope = null) where T : notnull
    {
        using var command = _db.CreateCommand(
            "INSERT INTO plugindata (key, value, type, namespace) VALUES (@key, @value, @type, @scope) " +
            "ON CONFLICT (key, namespace) DO UPDATE SET value = @value, type = @type"
        );
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("value", value.ToString()!);
        command.Parameters.AddWithValue("type", type);
        command.Parameters.AddWithValue("scope", scope ?? "global");
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveAsync(string key, string? scope = null)
    {
    }
    
    public async Task<PipeConfigurationRecipe> GetGlobalPipeConfigRecipeAsync()
    {
        Console.WriteLine("Getting global pipe config recipe");
        await using var command = _db.CreateCommand(
            """
            SELECT * FROM pipeservices 
            JOIN pipes ON pipeservices.pipeid = pipes.id 
            WHERE pipes.global = true
            """
        );
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return new PipeConfigurationRecipe
            {
                ServiceList = [],
            };
        var serviceList = new List<PipeRecipeServiceContainer>();
        do
        {
            var test = await reader.GetColumnSchemaAsync();
            var service = reader.GetString(2) + reader.GetString(1) + "/" + reader.GetString(3);
            ServiceFailurePolicies failurePolicy;
            try
            {
                failurePolicy = ParseFailurePolicy(reader.GetString("failure_policy"));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to parse failure policy for service {service}: {e.Message}");
                throw new Exceptions.MisconfiguredServiceException(
                    $"Service {service} is misconfigured. Failure policy is not valid. {e.Message}");
            }
            var container = new PipeRecipeServiceContainer()
            {
                Identifier = service,
                FailurePolicy = failurePolicy
            };
            serviceList.Add(container);
        } while (await reader.ReadAsync());
        
        return new PipeConfigurationRecipe
        {
            ServiceList = serviceList
        };
        

    }
    public async Task<PipeConfigurationRecipe> GetPipeConfigRecipeAsync(string? endpoint = null)
    {
        // In a real implementation, this would get the global config
        if (string.IsNullOrEmpty(endpoint))
        {
            return await GetGlobalPipeConfigRecipeAsync();
        }
        await using var command = _db.CreateCommand(
            "SELECT * FROM endpointservices WHERE $1 = path OR starts_with($1, path || '/')"
        );
        command.Parameters.AddWithValue(endpoint);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return await GetGlobalPipeConfigRecipeAsync();
        }
       
        var serviceList = new List<PipeRecipeServiceContainer>();
        do
        {
            var failurePolicy = ParseFailurePolicy(reader.GetString("failure_policy"));            
            var container = new PipeRecipeServiceContainer()
            {
                Identifier = reader.GetString(2) + reader.GetString(1) + "/" + reader.GetString(3),
                FailurePolicy = failurePolicy
            };
            
            serviceList.Add(container);
        } while (await reader.ReadAsync());
        
        return new PipeConfigurationRecipe
        {
            ServiceList = serviceList
        };

    }

    private static ServiceFailurePolicies ParseFailurePolicy(string failurePolicyString)
    {
        var failurePolicy = failurePolicyString switch
        {
            "Ignore" => ServiceFailurePolicies.Ignore,
            "RetryThenBlock" => ServiceFailurePolicies.RetryThenBlock,
            "RetryThenIgnore" => ServiceFailurePolicies.RetryThenIgnore,
            "Block" => ServiceFailurePolicies.Block,
            _ => throw new Exceptions.MisconfiguredServiceException(
                $"Service is misconfigured. Failure policy is not valid.")
        };

        return failurePolicy;
    }

    public async Task<Dictionary<string, Dictionary<string, string>>> GetPluginConfigsAsync(string? endpoint = null)
    {
        Guid endpointId = Guid.Empty;
        if (endpoint != null)
        {
            await using var command0 = _db.CreateCommand("SELECT id FROM endpoints WHERE path = $1");
            command0.Parameters.AddWithValue(endpoint);
            await using var reader0 = await command0.ExecuteReaderAsync();
            if (!await reader0.ReadAsync())
            {
                endpointId = Guid.Empty; // Use global configs if endpoint not found
            }

            while (await reader0.ReadAsync())
            {
                endpointId = reader0.GetGuid(0);
            }
        }
        else
        {
            endpointId = Guid.Empty; // Use a default value for global configs
        }
        
        await using var command = _db.CreateCommand("""
        SELECT key, value, type, namespace, endpointid, internal
        FROM pluginconfigs 
        WHERE endpointid = $1; 
        """);
        
        command.Parameters.AddWithValue(endpointId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var configs = new Dictionary<string, Dictionary<string, string>>();
        
        while (await reader.ReadAsync())
        {
            var pluginName = reader.GetString(3);
            var key = reader.GetString(0);
            var value = reader.GetString(1);
            
            if (!configs.ContainsKey(pluginName))
            {
                configs[pluginName] = new Dictionary<string, string>();
            }
            
            configs[pluginName][key] = value;
        }
        
        if (endpoint == null)
        {
            // If no endpoint is specified, return global configs
            return configs;
        }
        // If an endpoint is specified, also include global configs that are not overridden by the endpoint
        await using var command2 = _db.CreateCommand("""
        SELECT key, value, type, namespace, endpointid, internal
        FROM pluginconfigs 
        WHERE endpointid IS NULL
        AND (key, namespace) NOT IN (
            SELECT key, namespace 
        FROM pluginconfigs 
        WHERE endpointid = $1
            );
        """);
        
        command2.Parameters.AddWithValue(endpointId);
        
        await using var reader2 = await command2.ExecuteReaderAsync();
        while (await reader2.ReadAsync())
        {
            var pluginName = reader2.GetString(3);
            var key = reader2.GetString(0);
            var value = reader2.GetString(1);
            
            if (!configs.ContainsKey(pluginName))
            {
                configs[pluginName] = new Dictionary<string, string>();
            }
            
            // Only add the config if it doesn't already exist for the endpoint
            if (!configs[pluginName].ContainsKey(key))
            {
                configs[pluginName][key] = value;
            }
        }
        return configs;
    }
}