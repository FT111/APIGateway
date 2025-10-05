using System;
using System.Collections.Generic;
using GatewayPluginContract;
// using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;
using GatewayPluginContract.Entities;
using Endpoint = GatewayPluginContract.Entities.Endpoint;

namespace Supervisor.Context;

public partial class EfDbContext : DbContext
{
    public EfDbContext()
    {
    }

    public EfDbContext(DbContextOptions<EfDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Endpoint> Endpoints { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<Pipe> Pipes { get; set; }

    public virtual DbSet<PipeService> PipeServices { get; set; }

    public virtual DbSet<PluginConfig> PluginConfigs { get; set; }

    public virtual DbSet<PluginData> PluginData { get; set; }

    public virtual DbSet<Request> Requests { get; set; }

    public virtual DbSet<Target> Targets { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    {
        optionsBuilder.UseNpgsql(
            o =>
                o.MapEnum<ServiceFailurePolicies>("servicefailurepolicies"));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("conftypes", new[] { "text", "json", "boolean", "integer", "float" })
            .HasPostgresEnum("plugindatatype", new[] { "json", "string", "integer" })
            .HasPostgresEnum("servicefailurepolicies", new[] { "Ignore", "RetryThenBlock", "RetryThenIgnore", "Block" });

        modelBuilder.Entity<Endpoint>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("endpoints_pk");

            entity.ToTable("endpoints");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Path).HasColumnName("path");
            entity.Property(e => e.PipeId).HasColumnName("pipe_id");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetPathPrefix).HasColumnName("target_path_prefix");

            entity.HasOne(d => d.Pipe).WithMany(p => p.Endpoints)
                .HasForeignKey(d => d.PipeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("endpoints_pipes_id_fk");

            entity.HasOne(d => d.Target).WithMany(p => p.Endpoints)
                .HasForeignKey(d => d.TargetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("endpoints_targets_id_fk");
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("events_pk");

            entity.ToTable("events");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Endpointid).HasColumnName("endpoint_id");
            entity.Property(e => e.IsDismissed)
                .HasDefaultValue(false)
                .HasColumnName("is_dismissed");
            entity.Property(e => e.IsWarning)
                .HasDefaultValue(false)
                .HasColumnName("is_warning");
            entity.Property(e => e.MetaData).HasColumnName("meta_data");
            entity.Property(e => e.MetaType).HasColumnName("meta_type");
            entity.Property(e => e.ServiceIdentifier).HasColumnName("service_identifier");
            entity.Property(e => e.Title).HasColumnName("title");

            entity.HasOne(d => d.Endpoint).WithMany(p => p.Events)
                .HasForeignKey(d => d.Endpointid)
                .HasConstraintName("events_endpoints_id_fk");
        });
        
        modelBuilder.Entity<Plugin>(entity =>
        {
            entity.HasKey(e => new { e.Title, e.Version }).HasName("plugins_pk");

            entity.ToTable("plugins");

            entity.Property(e => e.Title)
                .HasColumnName("title");
            entity.Property(e => e.Version)
                .HasColumnName("version");
        });

        modelBuilder.Entity<Pipe>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pipes_pk");

            entity.ToTable("pipes");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Global)
                .HasDefaultValue(false)
                .HasColumnName("global");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<PipeService>(entity =>
        {
            entity.HasKey(e => new { e.PluginTitle, e.ServiceTitle, e.PipeId, e.Order }).HasName("pipe_services_pk");

            entity.ToTable("pipe_services");

            entity.Property(e => e.PluginTitle)
                .HasMaxLength(50)
                .HasColumnName("plugin_title");
            entity.Property(e => e.ServiceTitle)
                .HasMaxLength(50)
                .HasColumnName("service_title");
            entity.Property(e => e.PipeId).HasColumnName("pipe_id");
            entity.Property(e => e.Order).HasColumnName("order");
            entity.Property(e => e.PluginVersion)
                .HasMaxLength(50)
                .HasColumnName("plugin_version");

            entity.HasOne(d => d.Pipe).WithMany(p => p.PipeServices)
                .HasForeignKey(d => d.PipeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pipe_services_pipes_id_fk");
        });

        modelBuilder.Entity<PluginConfig>(entity =>
        {
            entity.HasKey(e => new { e.Key, e.Namespace }).HasName("plugin_configs_pk");

            entity.ToTable("plugin_configs");

            entity.Property(e => e.Key)
                .HasMaxLength(50)
                .HasColumnName("key");
            entity.Property(e => e.Namespace).HasColumnName("namespace");
            entity.Property(e => e.Internal)
                .HasDefaultValue(false)
                .HasColumnName("internal");
            entity.Property(e => e.PipeId).HasColumnName("pipe_id");
            entity.Property(e => e.Value).HasColumnName("value");

            entity.HasOne(d => d.Pipe).WithMany(p => p.PluginConfigs)
                .HasForeignKey(d => d.PipeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("plugin_configs_pipes_id_fk");
        });
        
        modelBuilder.Entity<DeploymentStatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("deployment_statuses_pk");

            entity.ToTable("deployment_statuses");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            
            entity.Property(e => e.HexColour).HasColumnName("hex_colour");
            entity.Property(e => e.Title).HasColumnName("title");
        });

        modelBuilder.Entity<PluginData>(entity =>
        {
            entity.HasKey(e => new { e.Namespace, e.Key }).HasName("plugin_data_pk");

            entity.ToTable("plugin_data");

            entity.HasIndex(e => new { e.Category, e.Key, e.Namespace }, "plugin_data_category_key_namespace_index");

            entity.Property(e => e.Namespace)
                .HasColumnType("character varying")
                .HasColumnName("namespace");
            entity.Property(e => e.Key)
                .HasColumnType("character varying")
                .HasColumnName("key");
            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.Value)
                .HasColumnType("character varying")
                .HasColumnName("value");
        });
        modelBuilder.Entity<Instance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("instances_pk");

            entity.ToTable("instances");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Status)
                .HasColumnName("status");
            entity.Property(e => e.PublicKey)
                .HasColumnName("public_key");
        });

        modelBuilder.Entity<Request>(entity =>
        {
            entity
                .HasKey(e => e.Id);
                entity
                .ToTable("requests");

            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.EndpointId).HasColumnName("endpoint_id").IsRequired(false);
            entity.Property(e => e.SourceAddress).HasColumnName("source_address");
            entity.HasOne(d => d.Endpoint)
                .WithMany(p => p.Requests)
                .HasForeignKey(d => d.EndpointId)
                .HasConstraintName("requests_endpoints_id_fk");
        });

        modelBuilder.Entity<Target>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("targets_pk");

            entity.ToTable("targets");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.BasePath).HasColumnName("base_path");
            entity.Property(e => e.Fallback)
                .HasDefaultValue(false)
                .HasColumnName("fallback");
            entity.Property(e => e.Host).HasColumnName("host");
            entity.Property(e => e.Schema).HasColumnName("schema");
        });
        
        modelBuilder.Entity<Deployment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("deployments_pk");

            entity.ToTable("deployments", "public");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Title)
                .HasColumnName("title");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.HasOne(e => e.Target)
                .WithMany(t => t.Deployments)
                .HasForeignKey(e => e.TargetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("deployments_targets_id_fk");
            entity.HasOne(e => e.Schema)
                .WithMany(s => s.Deployments)
                .HasForeignKey(e => e.SchemaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("deployments_schemas_id_fk");
            
            entity.HasOne(e => e.Status)
                .WithMany(s => s.Deployments)
                .HasForeignKey(e => e.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("deployments_deployment_statuses_id_fk");
            
            entity.HasMany(e => e.Endpoints)
                .WithOne(ep => ep.Deployment)
                .HasForeignKey("DeploymentId")
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("endpoints_deployments_id_fk");
            
        });
        
        modelBuilder.Entity<SchemaEndpoint>(entity =>
        {
            entity.HasKey(e => new { e.SchemaId }).HasName("schema_endpoints_pk");

            entity.ToTable("schema_endpoints", "public");

            entity.Property(e => e.SchemaId).HasColumnName("schema_id");
            entity.Property(e => e.Path).HasColumnName("path");

            entity.HasOne(d => d.Schema).WithMany(p => p.Endpoints)
                .HasForeignKey(d => d.SchemaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("schema_endpoints_schemas_id_fk");
        });
        
        modelBuilder.Entity<Schema>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("schemas_pk");

            entity.ToTable("schemas", "public");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Title)
                .HasColumnName("title");
            entity.Property(e => e.Description)
                .HasColumnName("description");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pk");

            entity.ToTable("users", "supervisor");

            entity.HasIndex(e => e.Username, "users_pk_2").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Passwordhs).HasColumnName("passwordhs");
            entity.Property(e => e.Username)
                .HasMaxLength(32)
                .HasColumnName("username");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
