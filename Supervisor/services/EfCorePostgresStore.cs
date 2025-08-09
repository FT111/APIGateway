using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Proxies;
using EFCore.NamingConventions;
using Supervisor.Context;
using GatewayPluginContract;
using GatewayPluginContract.Entities;

namespace Supervisor.services;



public class EfCorePostgresStore : GatewayPluginContract.Store
{
    private readonly DbContext? _dbContext;
    
            // private class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DbContext(options)
            // {
            //     public DbSet<PluginData> PluginData { get; set; } = null!;
            //     public DbSet<Endpoint> Endpoints { get; set; } = null!;
            //     public DbSet<PipeService> PipeServices { get; set; } = null!;
            //     public DbSet<PluginConfig> PluginConfigs { get; set; } = null!;
            //     public DbSet<Pipe> Pipes { get; set; } = null!;
            //     public DbSet<Target> Targets { get; set; } = null!;
            //     
            //     protected override void OnModelCreating(ModelBuilder modelBuilder)
            //     {
            //         base.OnModelCreating(modelBuilder);
            //         modelBuilder.Entity<PluginData>().ToTable("plugindata");
            //         modelBuilder.Entity<Endpoint>()
            //             .ToTable("endpoints");
            //         modelBuilder.Entity<Endpoint>()
            //             .HasOne(ep => ep.Target)
            //             .WithMany(t => t.Endpoints)
            //             .HasForeignKey("target_id");
            //         modelBuilder.Entity<Endpoint>()
            //             .HasOne(e => e.Pipe)
            //             .WithMany(p => p.Endpoints)
            //             .HasForeignKey("pipe_id")
            //             .OnDelete(DeleteBehavior.Cascade);
            //         // modelBuilder.Entity<PipeService>().ToTable("pipeservices").HasOne<Pipe>().WithMany(p => p.Services).HasForeignKey("pipe_id").OnDelete(DeleteBehavior.Cascade);
            //         modelBuilder.Entity<PluginConfig>().ToTable("pluginconfigs")
            //             .HasOne<Pipe>().WithMany(p => p.Configs)
            //             .HasForeignKey("pipe_id")  // Add this line to specify the foreign key column name
            //             .OnDelete(DeleteBehavior.Cascade);
            //         modelBuilder.Entity<Target>().ToTable("targets");
            //         modelBuilder.Entity<Pipe>().ToTable("pipes");
            //         
            //         modelBuilder.Entity<PipeService>()
            //             .HasKey(p => new { p.PluginTitle, p.ServiceTitle, p.PipeId, p.Order });
            //         
            //         modelBuilder.Entity<PluginData>()
            //             .HasKey(p => new { p.Key, p.Namespace });
            //         
            //         modelBuilder.Entity<PluginConfig>()
            //             .HasKey(p => new { p.Key, p.Namespace });
            //
            //         modelBuilder.HasPostgresEnum<ServiceFailurePolicies>();
            //     }
    // }
    
    public EfCorePostgresStore(IConfiguration configuration) : base(configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres") ??
                               throw new InvalidOperationException("Connection string 'EfCore' not found in configuration.");
        var optionsBuilder = new DbContextOptionsBuilder<EfDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure();
                npgsqlOptions.MigrationsAssembly("Gateway");
            })
            .UseSnakeCaseNamingConvention()
            .UseLazyLoadingProxies()
            .EnableSensitiveDataLogging();
        _dbContext = new EfDbContext(optionsBuilder.Options);
        if (_dbContext == null)
        {
            throw new InvalidOperationException("Failed to create DbContext.");
        }
        
        if (_dbContext.Database.CanConnect())
        {
            Console.WriteLine("Connected to database");
        }
        else
        {
            throw new InvalidOperationException("Failed to connect to the database.");
        }

    }

    private class DataRepo<T> : IDataRepository<T> where T : Entity
    {
        private readonly DbSet<T> _dbSet;
        private readonly DbContext _dbContext;

        public DataRepo(DbContext dbContext)
        {
            _dbSet = dbContext.Set<T>();
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext), "DbContext cannot be null.");
        }

        public async Task<T?> GetAsync(params object[] key)
        {
            return await _dbSet.FindAsync(key);
        }
        
        public async Task<List<T>> GetAllAsync()
        {
            return await _dbSet.AsNoTracking().ToListAsync();
        }

        public async Task AddAsync(T model)
        {
            await _dbSet.AddAsync(model);
            await _dbContext.SaveChangesAsync();
        }

        public async Task RemoveAsync(string key)
        {
            var entity = await GetAsync(key);
            if (entity != null)
            {
                _dbSet.Remove(entity);
            }
            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateAsync(T model)
        {
            _dbSet.Update(model);
            await _dbContext.SaveChangesAsync();
        }
        
        public async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate)
        {
            return await Task.FromResult(_dbSet.AsNoTracking().Where(predicate));
        }

    }
    
    private class EfCorePostgresGatewayRepositories : IGatewayRepositories
    {
        private readonly DbContext _dbContext;

        public EfCorePostgresGatewayRepositories(DbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext), "DbContext cannot be null.");
        }

        public IDataRepository<T> GetRepo<T>() where T : Entity
        {
            return new DataRepo<T>(_dbContext);
        }
    }
    
    public override IGatewayRepositories GetRepoFactory()
    {
        if (_dbContext == null)
        {
            throw new InvalidOperationException("DbContext is not initialized.");
        }
        return new EfCorePostgresGatewayRepositories(_dbContext);
    }
}

public class EfStoreFactory(IConfiguration configuration) : StoreFactory(configuration)
{
    private readonly IConfiguration _configuration = configuration;
    
    public override GatewayPluginContract.Store CreateStore()
    {
        return new EfCorePostgresStore(_configuration);
    }
}