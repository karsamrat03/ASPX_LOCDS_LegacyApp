using System;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;

namespace LOCDS.Web
{
    public partial class SiteMaster : MasterPage
    {
        private const string SqlHealthBannerSessionKey = "SqlHealthBanner";

        protected void Page_Init(object sender, EventArgs e)
        {
            __RequestVerificationToken.Value = EnsureCsrfToken(Context);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindUserContext();
                BindRoleMenu();
                hfSessionTimeoutMinutes.Value = Session.Timeout.ToString();
            }

            BindSqlHealthBanner();
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();
            Response.Redirect("~/Login.aspx", true);
        }

        protected void MainScriptManager_AsyncPostBackError(object sender, AsyncPostBackErrorEventArgs e)
        {
            MainScriptManager.AsyncPostBackErrorMessage = "An unexpected error occurred. Please refresh the page and try again.";
        }

        private void BindUserContext()
        {
            string userName = "Guest";
            if (Context != null
                && Context.User != null
                && Context.User.Identity != null
                && Context.User.Identity.IsAuthenticated)
            {
                userName = Context.User.Identity.Name;
            }

            lblLoggedInUser.Text = HttpUtility.HtmlEncode(userName);
            lblRoleBadge.Text = ResolveRoleBadge();
        }

        private void BindRoleMenu()
        {
            string role = ResolveRoleBadge();
            phApplicantMenu.Visible = string.Equals(role, "Applicant", StringComparison.OrdinalIgnoreCase);
            phCreditOfficerMenu.Visible = string.Equals(role, "CreditOfficer", StringComparison.OrdinalIgnoreCase);
            phUnderwriterMenu.Visible = string.Equals(role, "Underwriter", StringComparison.OrdinalIgnoreCase);
            phBranchManagerMenu.Visible = string.Equals(role, "BranchManager", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveRoleBadge()
        {
            if (Context == null
                || Context.User == null
                || Context.User.Identity == null
                || !Context.User.Identity.IsAuthenticated)
            {
                return "Guest";
            }

            var sessionRole = Session != null ? Session["UserRole"] as string : null;
            if (!string.IsNullOrWhiteSpace(sessionRole))
            {
                return sessionRole;
            }

            var formsIdentity = Context.User.Identity as FormsIdentity;
            if (formsIdentity != null
                && formsIdentity.Ticket != null
                && !string.IsNullOrWhiteSpace(formsIdentity.Ticket.UserData))
            {
                return formsIdentity.Ticket.UserData;
            }

            string[] roles = Roles.Enabled ? Roles.GetRolesForUser() : Array.Empty<string>();
            if (roles.Length == 0)
            {
                return "Applicant";
            }

            string[] precedence = { "BranchManager", "Underwriter", "CreditOfficer", "Applicant", "Admin" };
            string selected = precedence.FirstOrDefault(p => roles.Any(r => string.Equals(r, p, StringComparison.OrdinalIgnoreCase)));
            return selected ?? roles[0];
        }

        private void BindSqlHealthBanner()
        {
            string bannerMessage = Session != null ? Session[SqlHealthBannerSessionKey] as string : null;
            bool hasBanner = !string.IsNullOrWhiteSpace(bannerMessage);

            phSqlHealthBanner.Visible = hasBanner;
            lblSqlHealthBanner.Text = hasBanner
                ? HttpUtility.HtmlEncode(bannerMessage)
                : string.Empty;
        }

        private const string CsrfCookieName = "LOCDS-CSRF";

        private static string EnsureCsrfToken(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            HttpCookie csrfCookie = context.Request.Cookies[CsrfCookieName];
            string token = csrfCookie != null ? csrfCookie.Value : null;
            if (string.IsNullOrWhiteSpace(token))
            {
                token = Guid.NewGuid().ToString("N");
                var cookie = new HttpCookie(CsrfCookieName, token)
                {
                    HttpOnly = true,
                    Secure = context.Request.IsSecureConnection,
                    SameSite = SameSiteMode.Strict,
                    Path = "/"
                };

                context.Response.Cookies.Remove(CsrfCookieName);
                context.Response.Cookies.Add(cookie);
            }

            return token;
        }
    }
}
