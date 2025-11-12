using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Proxies;
using EFCore.NamingConventions;
using Gateway.Context;
using GatewayPluginContract;
using GatewayPluginContract.Entities;

namespace Gateway.services;



public class EfCorePostgresStore : GatewayPluginContract.Store
{

//     // private class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DbContext(options)
//     // {
//     //     public DbSet<PluginData> PluginData { get; set; } = null!;
//     //     public DbSet<Endpoint> Endpoints { get; set; } = null!;
//     //     public DbSet<PipeService> PipeServices { get; set; } = null!;
//     //     public DbSet<PluginConfig> PluginConfigs { get; set; } = null!;
//     //     public DbSet<Pipe> Pipes { get; set; } = null!;
//     //     public DbSet<Target> Targets { get; set; } = null!;
//     //     
//     //     protected override void OnModelCreating(ModelBuilder modelBuilder)
//     //     {
//     //         base.OnModelCreating(modelBuilder);
//     //         modelBuilder.Entity<PluginData>().ToTable("plugindata");
//     //         modelBuilder.Entity<Endpoint>()
//     //             .ToTable("endpoints");
//     //         modelBuilder.Entity<Endpoint>()
//     //             .HasOne(ep => ep.Target)
//     //             .WithMany(t => t.Endpoints)
//     //             .HasForeignKey("target_id");
//     //         modelBuilder.Entity<Endpoint>()
//     //             .HasOne(e => e.Pipe)
//     //             .WithMany(p => p.Endpoints)
//     //             .HasForeignKey("pipe_id")
//     //             .OnDelete(DeleteBehavior.Cascade);
//     //         // modelBuilder.Entity<PipeService>().ToTable("pipeservices").HasOne<Pipe>().WithMany(p => p.Services).HasForeignKey("pipe_id").OnDelete(DeleteBehavior.Cascade);
//     //         modelBuilder.Entity<PluginConfig>().ToTable("PluginConfigs")
//     //             .HasOne<Pipe>().WithMany(p => p.Configs)
//     //             .HasForeignKey("pipe_id")  // Add this line to specify the foreign key column name
//     //             .OnDelete(DeleteBehavior.Cascade);
//     //         modelBuilder.Entity<Target>().ToTable("targets");
//     //         modelBuilder.Entity<Pipe>().ToTable("pipes");
//     //         
//     //         modelBuilder.Entity<PipeService>()
//     //             .HasKey(p => new { p.PluginTitle, p.ServiceTitle, p.PipeId, p.Order });
//     //         
//     //         modelBuilder.Entity<PluginData>()
//     //             .HasKey(p => new { p.Key, p.Namespace });
//     //         
//     //         modelBuilder.Entity<PluginConfig>()
//     //             .HasKey(p => new { p.Key, p.Namespace });
//     //
//     //         modelBuilder.HasPostgresEnum<ServiceFailurePolicies>();
//     //     }
//     // }
//
    public override required DbContext Context { get; init; }


    private class DataRepo<T> : IDataRepository<T> where T : class
    {
        private readonly DbSet<T> _dbSet;
        private readonly DbContext Context;

        public DataRepo(DbContext dbContext)
        {
            _dbSet = dbContext.Set<T>();
            Context = dbContext ?? throw new ArgumentNullException(nameof(dbContext), "DbContext cannot be null.");
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
            await Context.SaveChangesAsync();
        }

        public async Task RemoveAsync(string key)
        {
            var entity = await GetAsync(key);
            if (entity != null)
            {
                _dbSet.Remove(entity);
            }

            await Context.SaveChangesAsync();
        }

        public async Task UpdateAsync(T model)
        {
            _dbSet.Update(model);
            await Context.SaveChangesAsync();
        }

        public async Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate)
        {
            return await Task.FromResult(_dbSet.AsNoTracking().Where(predicate));
        }

    }

    private class EfCorePostgresRepositories : Repositories
    {
        public override required DbContext Context { get; init; }

        public override IDataRepository<T> GetRepo<T>() where T : class
        {
            return new DataRepo<T>(Context);
        }
    }

    public override Repositories GetRepoFactory()
    {
        if (Context == null)
        {
            throw new InvalidOperationException("DbContext is not initialized.");
        }

        return new EfCorePostgresRepositories
        {
            Context = Context
        };
    }
}

public class EfStoreFactory(IConfiguration configuration) : StoreFactory(configuration)
{
    private readonly IConfiguration _configuration = configuration;
    
    public override GatewayPluginContract.Store CreateStore()
    {
        var connectionString = _configuration.GetConnectionString("default") ??
                               throw new InvalidOperationException("Connection string not found in configuration.");
        var optionsBuilder = new DbContextOptionsBuilder<EfDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure();
                npgsqlOptions.MigrationsAssembly("Gateway");
            })
            .UseSnakeCaseNamingConvention()
            // .UseLazyLoadingProxies()
            .EnableSensitiveDataLogging();
        var context = new EfDbContext(optionsBuilder.Options);
        if (context == null)
        {
            throw new InvalidOperationException("Failed to create DbContext.");
        }
        
        if (context.Database.CanConnect())
        {
            
        }
        else
        {
            throw new InvalidOperationException("Failed to connect to the database.");
        }

        return new EfCorePostgresStore()
        {
            Context = context
        };
    }
}