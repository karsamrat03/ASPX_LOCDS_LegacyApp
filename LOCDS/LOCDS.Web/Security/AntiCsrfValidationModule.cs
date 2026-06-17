using System;
using System.Web;
using System.Web.UI;

namespace LOCDS.Web.Security
{
    public sealed class AntiCsrfValidationModule : IHttpModule
    {
        public void Init(HttpApplication context)
        {
            if (context == null)
            {
                return;
            }

            context.PostMapRequestHandler += OnPostMapRequestHandler;
        }

        public void Dispose()
        {
        }

        private static void OnPostMapRequestHandler(object sender, EventArgs e)
        {
            var app = sender as HttpApplication;
            HttpContext context = app != null ? app.Context : null;
            if (context == null)
            {
                return;
            }

            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!(context.CurrentHandler is Page))
            {
                return;
            }

            string formToken = context.Request.Form["__RequestVerificationToken"];
            bool valid = AntiCsrfTokenService.ValidateAntiForgeryToken(context, formToken);
            if (valid)
            {
                return;
            }

            context.Response.Redirect("~/Errors/403.aspx", false);
            context.ApplicationInstance.CompleteRequest();
        }
    }
}
