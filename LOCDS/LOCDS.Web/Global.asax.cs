using System;
using System.Web;

namespace LOCDS.Web
{
    public partial class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            // Dev runtime fallback: skip external logger bootstrapping.
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            if (Context == null || Context.Request == null || Context.Response == null)
            {
                return;
            }

            string correlationId = Guid.NewGuid().ToString("N");
            Context.Items["CorrelationId"] = correlationId;
            Context.Response.Headers["X-Correlation-ID"] = correlationId;

            if (Context.Request.IsSecureConnection)
            {
                return;
            }

            string forwardedProto = Context.Request.Headers["X-Forwarded-Proto"];
            if (string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string host = Context.Request.Url.Host;
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var builder = new UriBuilder(Context.Request.Url)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = 443
            };

            Context.Response.Redirect(builder.Uri.ToString(), true);
        }

        protected void Application_EndRequest(object sender, EventArgs e)
        {
            // No-op for dev runtime fallback.
        }

        protected void Application_PreSendRequestHeaders(object sender, EventArgs e)
        {
            if (Context == null || Context.Response == null)
            {
                return;
            }

            Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            Context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception exception = Server.GetLastError();
            if (exception == null)
            {
                return;
            }

            if (Context != null
                && Context.Request != null
                && Context.Request.Url != null
                && string.Equals(Context.Request.Url.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Server.ClearError();

            if (Context == null || Context.Response == null)
            {
                return;
            }

            Context.Response.TrySkipIisCustomErrors = true;
            Context.Response.Redirect("~/Errors/500.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
