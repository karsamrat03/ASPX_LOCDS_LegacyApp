using System;
using LOCDS.Common.Infrastructure;

namespace LOCDS.Common.Exceptions
{
    [Serializable]
    public class AppException : Exception
    {
        public string CorrelationId { get; }

        public AppException(string message)
            : this(message, null, CorrelationContext.CorrelationId)
        {
        }

        public AppException(string message, Exception innerException)
            : this(message, innerException, CorrelationContext.CorrelationId)
        {
        }

        public AppException(string message, Exception innerException, string correlationId)
            : base(message, innerException)
        {
            CorrelationId = string.IsNullOrWhiteSpace(correlationId)
                ? CorrelationContext.CorrelationId
                : correlationId;
        }
    }
}
