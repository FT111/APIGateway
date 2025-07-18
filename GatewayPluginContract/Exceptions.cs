namespace GatewayPluginContract;

public static class Exceptions
{
     public class MissingRequiredServiceException : Exception
    {
        public MissingRequiredServiceException()
        {
        }

        public MissingRequiredServiceException(string message) : base(message)
        {
        }

        public MissingRequiredServiceException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}