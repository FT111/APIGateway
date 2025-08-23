using Microsoft.EntityFrameworkCore;
using GatewayPluginContract.Entities;

namespace Supervisor.Context;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<GatewayPluginContract.Entities.Endpoint> Endpoints { get; set; }
    public DbSet<Target> Targets { get; set; }
    public DbSet<Pipe> Pipes { get; set; }
    public DbSet<PipeService> PipeServices { get; set; }
    public DbSet<PluginConfig> PluginConfigs { get; set; }
    public DbSet<PluginData> PluginData { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<Request> Requests { get; set; }
}