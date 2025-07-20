namespace GatewayPluginContract;

public static class Exceptions
{
    public abstract class GatewayBaseException : Exception
    {
        protected GatewayBaseException()
        {
        }

        protected GatewayBaseException(string message) : base(message)
        {
        }

        protected GatewayBaseException(string message, Exception inner) : base(message, inner)
        {
        }
    }
    
     public class MissingRequiredServiceException : GatewayBaseException
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
     
     public class PipelineBlockedException : GatewayBaseException
     {
         public PipelineBlockedException()
         {
         }

         public PipelineBlockedException(string message) : base(message)
         {
         }

         public PipelineBlockedException(string message, Exception inner) : base(message, inner)
         {
         }
     }
     public class PipelineEndedException : GatewayBaseException
     {
         public PipelineEndedException()
         {
         }

         public PipelineEndedException(string message) : base(message)
         {
         }

         public PipelineEndedException(string message, Exception inner) : base(message, inner)
         {
         }
     }
     
     public class MisconfiguredServiceException : GatewayBaseException
     {
         public MisconfiguredServiceException()
         {
         }

         public MisconfiguredServiceException(string message) : base(message)
         {
         }

         public MisconfiguredServiceException(string message, Exception inner) : base(message, inner)
         {
         }
     }
}