using System;
using System.Security.Cryptography;
using System.Web;

namespace LOCDS.Web.Security
{
    internal static class AntiCsrfTokenService
    {
        private const string CookieName = "LOCDS-CSRF";

        internal static string EnsureToken(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            HttpCookie cookieValue = context.Request.Cookies[CookieName];
            string token = cookieValue != null ? cookieValue.Value : null;
            if (string.IsNullOrWhiteSpace(token))
            {
                token = CreateToken();
                var cookie = new HttpCookie(CookieName, token)
                {
                    HttpOnly = true,
                    Secure = context.Request.IsSecureConnection,
                    SameSite = SameSiteMode.Strict,
                    Path = "/"
                };

                context.Response.Cookies.Remove(CookieName);
                context.Response.Cookies.Add(cookie);
            }

            return token;
        }

        internal static bool ValidateAntiForgeryToken(HttpContext context, string formToken)
        {
            if (context == null)
            {
                return false;
            }

            HttpCookie cookieValue = context.Request.Cookies[CookieName];
            string cookieToken = cookieValue != null ? cookieValue.Value : null;
            if (string.IsNullOrWhiteSpace(cookieToken) || string.IsNullOrWhiteSpace(formToken))
            {
                return false;
            }

            return FixedTimeEquals(cookieToken, formToken);
        }

        private static string CreateToken()
        {
            byte[] data = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }

            return Convert.ToBase64String(data);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            byte[] l = System.Text.Encoding.UTF8.GetBytes(left);
            byte[] r = System.Text.Encoding.UTF8.GetBytes(right);

            if (l.Length != r.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < l.Length; i++)
            {
                diff |= l[i] ^ r[i];
            }

            return diff == 0;
        }
    }
}
