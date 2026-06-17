using System;

namespace LOCDS.Common.Exceptions
{
    [Serializable]
    public class BureauException : AppException
    {
        public BureauException(string message) : base(message)
        {
        }

        public BureauException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
