using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;

namespace LOCDS.Web.Security
{
    public sealed class RoleBasedAuthorizationModule : IHttpModule
    {
        private static readonly Dictionary<string, string[]> ProtectedPathRoles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "~/Dashboard.aspx", new[] { "CreditOfficer", "BranchManager", "Underwriter", "Applicant" } },
            { "~/UnderwritingDecision.aspx", new[] { "Underwriter", "BranchManager", "CreditOfficer" } },
            { "~/DecisionSheet.aspx", new[] { "Underwriter", "BranchManager", "CreditOfficer", "Applicant" } },
            { "~/LoanApplication.aspx", new[] { "Applicant", "CreditOfficer", "Underwriter", "BranchManager" } },
            { "~/LoanOffer.aspx", new[] { "Applicant", "CreditOfficer", "Underwriter", "BranchManager" } },
            { "~/CreditOfficer/", new[] { "CreditOfficer", "BranchManager" } },
            { "~/Underwriter/", new[] { "Underwriter", "BranchManager" } },
            { "~/BranchManager/", new[] { "BranchManager" } },
            { "~/Applicant/", new[] { "Applicant" } }
        };

        public void Init(HttpApplication context)
        {
            if (context == null)
            {
                return;
            }

            context.PostAcquireRequestState += OnAuthorizeRequest;
        }

        public void Dispose()
        {
        }

        private static void OnAuthorizeRequest(object sender, EventArgs e)
        {
            var app = sender as HttpApplication;
            HttpContext context = app != null ? app.Context : null;
            if (context == null)
            {
                return;
            }

            string appRelativePath = context.Request.AppRelativeCurrentExecutionFilePath ?? string.Empty;
            if (IsIgnoredPath(appRelativePath))
            {
                return;
            }

            string[] allowedRoles = ResolveAllowedRoles(appRelativePath);
            if (allowedRoles == null || allowedRoles.Length == 0)
            {
                return;
            }

            if (context.User == null || context.User.Identity == null || !context.User.Identity.IsAuthenticated)
            {
                FormsAuthentication.RedirectToLoginPage();
                context.ApplicationInstance.CompleteRequest();
                return;
            }

            string currentRole = ResolveRole(context);
            if (string.IsNullOrWhiteSpace(currentRole) || !allowedRoles.Any(r => string.Equals(r, currentRole, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.Redirect("~/Errors/403.aspx", false);
                context.ApplicationInstance.CompleteRequest();
            }
        }

        private static string[] ResolveAllowedRoles(string appRelativePath)
        {
            foreach (var rule in ProtectedPathRoles)
            {
                if (rule.Key.EndsWith("/", StringComparison.Ordinal) && appRelativePath.StartsWith(rule.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return rule.Value;
                }

                if (!rule.Key.EndsWith("/", StringComparison.Ordinal) && string.Equals(rule.Key, appRelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return rule.Value;
                }
            }

            return Array.Empty<string>();
        }

        private static string ResolveRole(HttpContext context)
        {
            string sessionRole = context.Session != null ? context.Session["UserRole"] as string : null;
            if (!string.IsNullOrWhiteSpace(sessionRole))
            {
                return sessionRole;
            }

            var formsIdentity = context.User != null ? context.User.Identity as FormsIdentity : null;
            if (formsIdentity != null && formsIdentity.Ticket != null && !string.IsNullOrWhiteSpace(formsIdentity.Ticket.UserData))
            {
                return formsIdentity.Ticket.UserData;
            }

            if (Roles.Enabled)
            {
                string[] roles = Roles.GetRolesForUser(context.User.Identity.Name);
                if (roles != null && roles.Length > 0)
                {
                    return roles[0];
                }
            }

            return string.Empty;
        }

        private static bool IsIgnoredPath(string appRelativePath)
        {
            if (string.IsNullOrWhiteSpace(appRelativePath))
            {
                return true;
            }

            return appRelativePath.StartsWith("~/Login.aspx", StringComparison.OrdinalIgnoreCase)
                || appRelativePath.StartsWith("~/Errors/", StringComparison.OrdinalIgnoreCase)
                || appRelativePath.StartsWith("~/Scripts/", StringComparison.OrdinalIgnoreCase)
                || appRelativePath.StartsWith("~/Content/", StringComparison.OrdinalIgnoreCase)
                || appRelativePath.StartsWith("~/favicon.ico", StringComparison.OrdinalIgnoreCase)
                || appRelativePath.EndsWith(".axd", StringComparison.OrdinalIgnoreCase);
        }
    }
}
