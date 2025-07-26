using Microsoft.EntityFrameworkCore;
using EFCore.NamingConventions;
using GatewayPluginContract;
using Endpoint = GatewayPluginContract.Endpoint;

namespace Gateway.services;



public class EfCorePostgresStore : GatewayPluginContract.Store
{
    private readonly DbContext? _dbContext;
    
    private class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DbContext(options)
    {
        public DbSet<PluginData> PluginData { get; set; } = null!;
        public DbSet<Endpoint> Endpoints { get; set; } = null!;
        public DbSet<PipeService> PipeServices { get; set; } = null!;
        public DbSet<PluginConfig> PluginConfigs { get; set; } = null!;
        public DbSet<Pipe> Pipes { get; set; } = null!;
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<PluginData>().ToTable("plugindata");
            modelBuilder.Entity<Endpoint>().ToTable("endpoints");
            modelBuilder.Entity<PipeService>().ToTable("pipeservices").HasOne<Pipe>().WithMany(p => p.Services).OnDelete(DeleteBehavior.Cascade).HasForeignKey(p => p.PipeId);
            modelBuilder.Entity<PluginConfig>().ToTable("pluginconfigs").HasOne<Pipe>().WithMany(p => p.Configs).OnDelete(DeleteBehavior.Cascade).HasForeignKey(p => p.PipeId);

            modelBuilder.Entity<Pipe>().ToTable("pipes");
            
            modelBuilder.Entity<PipeService>()
                .HasKey(p => new { p.PluginTitle, p.ServiceTitle, p.PipeId, p.Order });
            
            modelBuilder.Entity<PluginConfig>()
                .HasKey(p => new { p.Key, p.Namespace });

            modelBuilder.HasPostgresEnum<ServiceFailurePolicies>();
        }
        
    }
    
    public EfCorePostgresStore(IConfiguration configuration) : base(configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres") ??
                               throw new InvalidOperationException("Connection string 'EfCore' not found in configuration.");
        var optionsBuilder = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure();
            npgsqlOptions.MigrationsAssembly("Gateway");
        })
            .UseSnakeCaseNamingConvention();
        
        _dbContext = new PostgresDbContext(optionsBuilder.Options);
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

    private class DataRepo<T> : IDataRepository<T> where T : GatewayModel
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
        
        public async Task<IEnumerable<T>> QueryAsync(Func<T, bool> predicate)
        {
            return await Task.FromResult(_dbSet.AsNoTracking().Where(predicate).ToList());
        }

    }
    
    private class EfCorePostgresRepoFactory : IRepoFactory
    {
        private readonly DbContext _dbContext;

        public EfCorePostgresRepoFactory(DbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext), "DbContext cannot be null.");
        }

        public IDataRepository<T> GetRepo<T>() where T : GatewayModel
        {
            return new DataRepo<T>(_dbContext);
        }
    }
    
    public override IRepoFactory GetRepoFactory()
    {
        if (_dbContext == null)
        {
            throw new InvalidOperationException("DbContext is not initialized.");
        }
        return new EfCorePostgresRepoFactory(_dbContext);
    }
}