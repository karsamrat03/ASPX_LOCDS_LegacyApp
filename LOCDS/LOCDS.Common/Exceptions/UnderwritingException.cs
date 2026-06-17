using System;

namespace LOCDS.Common.Exceptions
{
    [Serializable]
    public class UnderwritingException : AppException
    {
        public UnderwritingException(string message) : base(message)
        {
        }

        public UnderwritingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
