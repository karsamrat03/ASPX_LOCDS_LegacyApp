using System;
using System.Threading;

namespace LOCDS.Common.Infrastructure
{
    public static class CorrelationContext
    {
        private static readonly AsyncLocal<string> Current = new AsyncLocal<string>();

        public static string CorrelationId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Current.Value))
                {
                    Current.Value = Guid.NewGuid().ToString("N");
                }

                return Current.Value;
            }
        }

        public static void Set(string correlationId)
        {
            Current.Value = string.IsNullOrWhiteSpace(correlationId)
                ? Guid.NewGuid().ToString("N")
                : correlationId;
        }

        public static void Clear()
        {
            Current.Value = null;
        }
    }
}
