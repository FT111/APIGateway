namespace GatewayPluginContract.MQ;

public static class Contracts
{
    public readonly struct MqCommandKey : IEquatable<MqCommandKey>
    {
        private readonly string _value;
        private MqCommandKey(string value) => _value = value;
        
        public static bool TryParse(string value, out MqCommandKey key)
        {
            key = new MqCommandKey(value);
            return true;
        }
        
        public static MqCommandKey New(string pluginIdentifier, string cmdIdentifier) => new(pluginIdentifier + "." + cmdIdentifier);
        internal static MqCommandKey Internal (string cmdIdentifier) => new("internal." + cmdIdentifier);
        
        
        public static implicit operator MqCommandKey(string value) => new(value);
        public bool Equals(MqCommandKey other) => _value == other._value;
        public override bool Equals(object? obj) => obj is MqCommandKey other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value;
        
        public static implicit operator string(MqCommandKey key) => key._value;
    }
}