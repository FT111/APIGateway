// namespace GatewayPluginContract;
//
// public abstract class Entity
// {}
//
// public class Pipe : Entity
// {
//     public required Guid     Id        { get; set; }
//     public          DateTime CreatedAt { get; set; }
//     public          DateTime UpdatedAt { get; set; }
//     public          bool     Global    { get; set; }
//     public virtual ICollection<PipeService> Services  { get; set; } = new List<PipeService>();
//     public virtual ICollection<Endpoint>     Endpoints { get; set; } = new List<Endpoint>();
//     public virtual ICollection<PluginConfig> Configs    { get; set; } = new List<PluginConfig>();
// }
//
// public class Endpoint : Entity
// {
//     public required Guid   Id               { get; set; }
//     public required string Path             { get; set; }
//     public          string? TargetPathPrefix { get; set; }
//     public virtual required Target Target           { get; set; }
//     public virtual required Pipe   Pipe             { get; set; }
// }
//
// public class PluginData : Entity
// {
//     public required string Namespace { get; set; }
//     public required string Key       { get; set; }
//     public string?        Value     { get; set; }
//     public string Type      { get; set; } = "string"; // Default type is string
// }
//
// public class PipeService : Entity
// {
//     public required string PluginTitle   { get; set; }
//     public required string PluginVersion { get; set; }
//     public required string ServiceTitle  { get; set; }
//     public          long   Order         { get; set; }
//     public required Guid   PipeId        { get; set; }
//     public ServiceFailurePolicies FailurePolicy { get; set; }
//     public virtual required Pipe  Pipe          { get; set; }
// }
// public class PluginConfig : Entity
// {
//     public required string Key       { get; set; }
//     public string?       Value       { get; set; }
//     public required string Type       { get; set; }
//     public required string Namespace  { get; set; }
//     public          bool   Internal   { get; set; } = false;
//     public virtual required Pipe   Pipe       { get; set; }
// }
//
// public class Target : Entity
// {
//     public required Guid   Id       { get; set; }
//     public          string? BasePath { get; set; }
//     public required string  Host     { get; set; }
//     public required string  Schema   { get; set; }
//     public virtual ICollection<Endpoint> Endpoints { get; set; } = new List<Endpoint>();
// }