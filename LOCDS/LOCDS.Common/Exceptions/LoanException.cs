using System;

namespace LOCDS.Common.Exceptions
{
    [Serializable]
    public class LoanException : AppException
    {
        public LoanException(string message) : base(message)
        {
        }

        public LoanException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
