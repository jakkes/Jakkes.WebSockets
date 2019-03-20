using System;
using System.Runtime.Serialization;

namespace Jakkes.WebSockets { 

    [Serializable]
    public class ConnectionClosedException : Exception
    {
        public ConnectionClosedException()
        {
        }

        public ConnectionClosedException(string message) : base(message)
        {
        }

        public ConnectionClosedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ConnectionClosedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class UnmaskedMessageException : Exception
    {
        public UnmaskedMessageException()
        {
        }

        public UnmaskedMessageException(string message) : base(message)
        {
        }

        public UnmaskedMessageException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected UnmaskedMessageException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

}