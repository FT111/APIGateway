namespace GatewayPluginContract.Attributes;


[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class QueryableAttribute : Attribute
{
    // See the attribute guidelines at 
    //  http://go.microsoft.com/fwlink/?LinkId=85236
    public QueryableAttribute()
    {
    }
}