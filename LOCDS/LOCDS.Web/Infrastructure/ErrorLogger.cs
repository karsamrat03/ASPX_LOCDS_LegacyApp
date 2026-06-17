using System;
using System.Text;
using System.Web;
using LOCDS.Common.Infrastructure;
using log4net;
using log4net.Util;

namespace LOCDS.Web.Infrastructure
{
    public static class ErrorLogger
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorLogger));

        public static void LogException(Exception exception, HttpContext context = null)
        {
            if (exception == null)
            {
                return;
            }

            string correlationId = CorrelationContext.CorrelationId;
            LogicalThreadContext.Properties["CorrelationId"] = correlationId;

            string userId = ResolveUserId(context);
            string sessionId = ResolveSessionId(context);
            string requestUrl = ResolveRequestUrl(context);
            string timestamp = DateTime.UtcNow.ToString("o");

            var builder = new StringBuilder();
            builder.AppendLine("Unhandled exception captured");
            builder.AppendLine("CorrelationId: " + correlationId);
            builder.AppendLine("ExceptionType: " + exception.GetType().FullName);
            builder.AppendLine("Message: " + exception.Message);
            builder.AppendLine("UserId: " + userId);
            builder.AppendLine("SessionId: " + sessionId);
            builder.AppendLine("Url: " + requestUrl);
            builder.AppendLine("TimestampUtc: " + timestamp);
            builder.AppendLine("StackTrace: " + (exception.StackTrace ?? string.Empty));

            Logger.Error(builder.ToString(), exception);
        }

        public static void LogAsyncPostbackError(Exception exception, HttpContext context = null)
        {
            if (exception == null)
            {
                return;
            }

            string correlationId = CorrelationContext.CorrelationId;
            LogicalThreadContext.Properties["CorrelationId"] = correlationId;

            string userId = ResolveUserId(context);
            string sessionId = ResolveSessionId(context);
            string requestUrl = ResolveRequestUrl(context);
            string timestamp = DateTime.UtcNow.ToString("o");

            var builder = new StringBuilder();
            builder.AppendLine("Async postback exception captured");
            builder.AppendLine("CorrelationId: " + correlationId);
            builder.AppendLine("ExceptionType: " + exception.GetType().FullName);
            builder.AppendLine("Message: " + exception.Message);
            builder.AppendLine("UserId: " + userId);
            builder.AppendLine("SessionId: " + sessionId);
            builder.AppendLine("Url: " + requestUrl);
            builder.AppendLine("TimestampUtc: " + timestamp);
            builder.AppendLine("StackTrace: " + (exception.StackTrace ?? string.Empty));

            Logger.Error(builder.ToString(), exception);
        }

        private static string ResolveUserId(HttpContext context)
        {
            string user = "anonymous";
            if (context != null
                && context.User != null
                && context.User.Identity != null
                && context.User.Identity.IsAuthenticated)
            {
                user = context.User.Identity.Name;
            }

            return string.IsNullOrWhiteSpace(user) ? "anonymous" : user;
        }

        private static string ResolveSessionId(HttpContext context)
        {
            try
            {
                string sessionId = null;
                if (context != null && context.Session != null)
                {
                    sessionId = context.Session.SessionID;
                }
                return string.IsNullOrWhiteSpace(sessionId) ? "n/a" : sessionId;
            }
            catch
            {
                return "n/a";
            }
        }

        private static string ResolveRequestUrl(HttpContext context)
        {
            string url = null;
            if (context != null && context.Request != null && context.Request.Url != null)
            {
                url = context.Request.Url.ToString();
            }
            return string.IsNullOrWhiteSpace(url) ? "n/a" : url;
        }
    }
}
