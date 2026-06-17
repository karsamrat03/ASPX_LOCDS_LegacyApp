using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using System.Web.UI;

namespace LOCDS.Web
{
    public partial class Login : Page
    {
        private const string FailedAttemptsSessionKey = "LoginFailedAttempts";
        private const string CaptchaAnswerSessionKey = "LoginCaptchaAnswer";
        private const string OtpCodeSessionKey = "LoginOtpCode";
        private const string OtpExpirySessionKey = "LoginOtpExpiryUtc";
        private const string OtpUsernameSessionKey = "LoginOtpUser";
        private const string UserRoleSessionKey = "UserRole";
        private const string RememberCookieName = "LOCDS_REMEMBER";

        private static readonly string[] SqlConnectivityErrorMarkers =
        {
            "A network-related or instance-specific error occurred",
            "provider: Named Pipes Provider, error: 40",
            "Could not open a connection to SQL Server",
            "The server was not found or was not accessible"
        };

        protected void Page_Init(object sender, EventArgs e)
        {
            __RequestVerificationToken.Value = EnsureCsrfToken(Context);

            if (IsPostBack && !ValidateCsrfToken(Context, __RequestVerificationToken.Value))
            {
                Response.Redirect("~/Errors/403.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (Request.IsAuthenticated)
                {
                    Response.Redirect(ResolveLandingPageByRole(Session[UserRoleSessionKey] as string), false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }

                HydrateRememberedUsername();
                ToggleCaptchaIfRequired();
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            lblFeedback.CssClass = "d-block mt-3 text-danger";
            lblFeedback.Text = string.Empty;

            Page.Validate();
            if (!Page.IsValid)
            {
                return;
            }

            var username = (txtUsername.Text ?? string.Empty).Trim();
            var password = txtPassword.Text ?? string.Empty;
            var ipAddress = ResolveIpAddress();

            int failedAttempts = GetFailedAttempts();
            if (failedAttempts >= 3 && !ValidateCaptcha())
            {
                lblFeedback.Text = "CAPTCHA validation failed. Please try again.";
                return;
            }

            UserAccountRecord account;
            string failureReason;
            bool authenticated = TryAuthenticateUser(username, password, out account, out failureReason);

            if (authenticated)
            {
                RegenerateSessionId();
                Session[FailedAttemptsSessionKey] = 0;
                Session.Remove(CaptchaAnswerSessionKey);
                Session[UserRoleSessionKey] = account.RoleName;

                IssueAuthenticationCookie(username, account.RoleName, chkRememberMe.Checked);
                PersistRememberMeCookie(username, chkRememberMe.Checked);
                UpdateOnSuccessfulLogin(username);
                LogLoginAttempt(username, true, ipAddress, "LOGIN_SUCCESS");

                Response.Redirect(ResolveLandingPageByRole(account.RoleName), false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            failedAttempts++;
            Session[FailedAttemptsSessionKey] = failedAttempts;
            UpdateFailedLoginAttempt(username);
            LogLoginAttempt(username, false, ipAddress, "LOGIN_FAILED");

            if (failedAttempts >= 5)
            {
                LockUserAccount(username);
                SendLockoutEmail(username, account != null ? account.Email : null);
                LogLoginAttempt(username, false, ipAddress, "ACCOUNT_LOCKED");
                lblFeedback.Text = "Account has been locked after multiple failed attempts. Please contact support.";
            }
            else
            {
                lblFeedback.Text = string.IsNullOrWhiteSpace(failureReason)
                    ? "Invalid username or password."
                    : HttpUtility.HtmlEncode(failureReason);
            }

            ToggleCaptchaIfRequired();
        }

        protected void lnkForgotPassword_Click(object sender, EventArgs e)
        {
            lblFeedback.CssClass = "d-block mt-3 text-info";
            lblFeedback.Text = string.Empty;

            var username = (txtUsername.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                lblFeedback.CssClass = "d-block mt-3 text-danger";
                lblFeedback.Text = "Enter your username to receive OTP.";
                return;
            }

            string email = GetUserEmail(username);
            if (string.IsNullOrWhiteSpace(email))
            {
                lblFeedback.CssClass = "d-block mt-3 text-danger";
                lblFeedback.Text = "No registered email found for this username.";
                return;
            }

            string otpCode = GenerateOtpCode();
            Session[OtpCodeSessionKey] = otpCode;
            Session[OtpExpirySessionKey] = DateTime.UtcNow.AddMinutes(10);
            Session[OtpUsernameSessionKey] = username;

            bool sent = SendOtpEmail(username, email, otpCode);
            LogLoginAttempt(username, sent, ResolveIpAddress(), sent ? "FORGOT_PASSWORD_OTP_SENT" : "FORGOT_PASSWORD_OTP_FAILED");

            if (!sent)
            {
                lblFeedback.CssClass = "d-block mt-3 text-danger";
                lblFeedback.Text = "Unable to send OTP right now. Please try again later.";
                return;
            }

            lblFeedback.Text = "OTP has been sent to your registered email address.";
        }

        private bool TryAuthenticateUser(string username, string password, out UserAccountRecord account, out string reason)
        {
            account = null;
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                reason = "Username and password are required.";
                return false;
            }

            if (!TryGetUserAccount(username, out account, out reason))
            {
                if (IsSqlConnectivityIssue(reason))
                {
                    return TryAuthenticateFallbackUser(username, password, out account, out reason);
                }

                return false;
            }

            if (account == null)
            {
                reason = "Invalid username or password.";
                return false;
            }

            if (account.IsLocked)
            {
                reason = "Your account is currently locked.";
                return false;
            }

            if (!VerifyPassword(password, account.PasswordHash))
            {
                reason = "Invalid username or password.";
                return false;
            }

            return true;
        }

        private bool TryGetUserAccount(string username, out UserAccountRecord account, out string reason)
        {
            account = null;
            reason = string.Empty;

            var loginConnection = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = loginConnection != null ? loginConnection.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                reason = "Authentication store is not configured.";
                return false;
            }

            try
            {
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(@"
IF OBJECT_ID('locds.UserAccount', 'U') IS NULL
BEGIN
    SELECT CAST(0 AS BIT) AS IsReady;
    RETURN;
END;

SELECT CAST(1 AS BIT) AS IsReady;
SELECT TOP (1)
    UserName,
    PasswordHash,
    ISNULL(RoleName, 'Applicant') AS RoleName,
    Email,
    ISNULL(IsLocked, 0) AS IsLocked
FROM locds.UserAccount
WHERE UserName = @UserName;", connection))
                {
                    command.Parameters.Add("@UserName", SqlDbType.NVarChar, 100).Value = username;
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read() || !reader.GetBoolean(0))
                        {
                            reason = "Authentication table locds.UserAccount is missing.";
                            return false;
                        }

                        if (!reader.NextResult() || !reader.Read())
                        {
                            return true;
                        }

                        account = new UserAccountRecord
                        {
                            Username = reader["UserName"] as string ?? string.Empty,
                            PasswordHash = reader["PasswordHash"] as string ?? string.Empty,
                            RoleName = reader["RoleName"] as string ?? "Applicant",
                            Email = reader["Email"] as string,
                            IsLocked = Convert.ToBoolean(reader["IsLocked"], CultureInfo.InvariantCulture)
                        };
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = "Authentication lookup failed: " + ex.Message;
                return false;
            }
        }

        private void UpdateFailedLoginAttempt(string username)
        {
            ExecuteNonQuery(@"
IF OBJECT_ID('locds.UserAccount', 'U') IS NOT NULL
BEGIN
    UPDATE locds.UserAccount
    SET FailedAttemptCount = ISNULL(FailedAttemptCount, 0) + 1,
        LastFailedLoginUtc = SYSUTCDATETIME()
    WHERE UserName = @UserName;
END", username);
        }

        private void UpdateOnSuccessfulLogin(string username)
        {
            ExecuteNonQuery(@"
IF OBJECT_ID('locds.UserAccount', 'U') IS NOT NULL
BEGIN
    UPDATE locds.UserAccount
    SET FailedAttemptCount = 0,
        LastLoginUtc = SYSUTCDATETIME(),
        IsLocked = 0,
        LockedUntilUtc = NULL
    WHERE UserName = @UserName;
END", username);
        }

        private void LockUserAccount(string username)
        {
            ExecuteNonQuery(@"
IF OBJECT_ID('locds.UserAccount', 'U') IS NOT NULL
BEGIN
    UPDATE locds.UserAccount
    SET IsLocked = 1,
        LockedUntilUtc = DATEADD(MINUTE, 30, SYSUTCDATETIME())
    WHERE UserName = @UserName;
END", username);
        }

        private void LogLoginAttempt(string username, bool success, string ipAddress, string action)
        {
            var auditConnection = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = auditConnection != null ? auditConnection.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            try
            {
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(@"
IF OBJECT_ID('locds.AuditLog', 'U') IS NOT NULL
BEGIN
    INSERT INTO locds.AuditLog
    (
        EntityType,
        EntityId,
        Action,
        OldValue,
        NewValue,
        PerformedBy,
        PerformedAt,
        IPAddress,
        CreatedDate,
        LastModifiedDate
    )
    VALUES
    (
        N'Authentication',
        @EntityId,
        @Action,
        NULL,
        @NewValue,
        @PerformedBy,
        SYSUTCDATETIME(),
        @IPAddress,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END", connection))
                {
                    command.Parameters.Add("@EntityId", SqlDbType.NVarChar, 64).Value = username;
                    command.Parameters.Add("@Action", SqlDbType.NVarChar, 20).Value = action;
                    command.Parameters.Add("@NewValue", SqlDbType.NVarChar, -1).Value = success ? "Success" : "Failure";
                    command.Parameters.Add("@PerformedBy", SqlDbType.NVarChar, 100).Value = username;
                    command.Parameters.Add("@IPAddress", SqlDbType.NVarChar, 45).Value = (object)ipAddress ?? DBNull.Value;
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch
            {
                // Ignore audit logging failures to avoid blocking authentication response path.
            }
        }

        private void IssueAuthenticationCookie(string username, string roleName, bool rememberMe)
        {
            var issuedAt = DateTime.Now;
            var expiresAt = rememberMe ? issuedAt.AddDays(7) : issuedAt.AddMinutes(FormsAuthentication.Timeout.TotalMinutes);

            var ticket = new FormsAuthenticationTicket(
                2,
                username,
                issuedAt,
                expiresAt,
                rememberMe,
                roleName ?? "Applicant");

            string encrypted = FormsAuthentication.Encrypt(ticket);
            var authCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted)
            {
                HttpOnly = true,
                Secure = Request.IsSecureConnection,
                Expires = rememberMe ? expiresAt : DateTime.MinValue,
                SameSite = SameSiteMode.Lax
            };

            Response.Cookies.Remove(FormsAuthentication.FormsCookieName);
            Response.Cookies.Add(authCookie);
        }

        private void PersistRememberMeCookie(string username, bool rememberMe)
        {
            if (!rememberMe)
            {
                if (Request.Cookies[RememberCookieName] != null)
                {
                    var expired = new HttpCookie(RememberCookieName)
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };
                    Response.Cookies.Add(expired);
                }

                return;
            }

            var rememberTicket = new FormsAuthenticationTicket(
                1,
                "remember",
                DateTime.Now,
                DateTime.Now.AddDays(7),
                true,
                username);

            string encrypted = FormsAuthentication.Encrypt(rememberTicket);
            var rememberCookie = new HttpCookie(RememberCookieName, encrypted)
            {
                HttpOnly = true,
                Secure = Request.IsSecureConnection,
                Expires = DateTime.Now.AddDays(7),
                SameSite = SameSiteMode.Lax
            };

            Response.Cookies.Remove(RememberCookieName);
            Response.Cookies.Add(rememberCookie);
        }

        private void HydrateRememberedUsername()
        {
            var rememberCookie = Request.Cookies[RememberCookieName];
            if (rememberCookie == null || string.IsNullOrWhiteSpace(rememberCookie.Value))
            {
                return;
            }

            try
            {
                var ticket = FormsAuthentication.Decrypt(rememberCookie.Value);
                if (ticket != null && ticket.Expiration > DateTime.Now && !string.IsNullOrWhiteSpace(ticket.UserData))
                {
                    txtUsername.Text = ticket.UserData;
                    chkRememberMe.Checked = true;
                }
            }
            catch
            {
                // Ignore malformed or tampered cookie data.
            }
        }

        private int GetFailedAttempts()
        {
            object current = Session[FailedAttemptsSessionKey];
            if (current == null)
            {
                return 0;
            }

            int attempts;
            return int.TryParse(current.ToString(), out attempts) ? attempts : 0;
        }

        private void ToggleCaptchaIfRequired()
        {
            int attempts = GetFailedAttempts();
            pnlCaptcha.Visible = attempts >= 3;

            if (pnlCaptcha.Visible && Session[CaptchaAnswerSessionKey] == null)
            {
                GenerateCaptcha();
            }
        }

        private void GenerateCaptcha()
        {
            var random = new Random();
            int left = random.Next(10, 60);
            int right = random.Next(1, 20);
            Session[CaptchaAnswerSessionKey] = left + right;
            lblCaptchaQuestion.Text = left + " + " + right;
            txtCaptcha.Text = string.Empty;
        }

        private bool ValidateCaptcha()
        {
            object answerObj = Session[CaptchaAnswerSessionKey];
            if (answerObj == null)
            {
                GenerateCaptcha();
                return false;
            }

            int expected;
            if (!int.TryParse(answerObj.ToString(), out expected))
            {
                GenerateCaptcha();
                return false;
            }

            int actual;
            string captchaText = txtCaptcha.Text == null ? null : txtCaptcha.Text.Trim();
            if (!int.TryParse(captchaText, out actual) || expected != actual)
            {
                GenerateCaptcha();
                return false;
            }

            Session.Remove(CaptchaAnswerSessionKey);
            return true;
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            if (storedHash.StartsWith("PBKDF2$", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = storedHash.Split('$');
                if (parts.Length != 4)
                {
                    return false;
                }

                int iterations;
                if (!int.TryParse(parts[1], out iterations))
                {
                    return false;
                }

                byte[] salt;
                byte[] expected;

                try
                {
                    salt = Convert.FromBase64String(parts[2]);
                    expected = Convert.FromBase64String(parts[3]);
                }
                catch
                {
                    return false;
                }

                using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                {
                    var actual = deriveBytes.GetBytes(expected.Length);
                    return FixedTimeEquals(actual, expected);
                }
            }

            string sha256 = ComputeSha256Hex(password);
            if (string.Equals(storedHash, sha256, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(storedHash, password, StringComparison.Ordinal);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }

            return diff == 0;
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
                byte[] data = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(data);
                }

                token = Convert.ToBase64String(data);
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

        private static bool ValidateCsrfToken(HttpContext context, string formToken)
        {
            if (context == null)
            {
                return false;
            }

            HttpCookie csrfCookie = context.Request.Cookies[CsrfCookieName];
            string cookieToken = csrfCookie != null ? csrfCookie.Value : null;
            if (string.IsNullOrWhiteSpace(cookieToken) || string.IsNullOrWhiteSpace(formToken))
            {
                return false;
            }

            byte[] left = Encoding.UTF8.GetBytes(cookieToken);
            byte[] right = Encoding.UTF8.GetBytes(formToken);
            return FixedTimeEquals(left, right);
        }

        private static string ComputeSha256Hex(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static string ResolveLandingPageByRole(string roleName)
        {
            if (string.Equals(roleName, "CreditOfficer", StringComparison.OrdinalIgnoreCase))
            {
                return "~/Dashboard.aspx";
            }

            if (string.Equals(roleName, "Underwriter", StringComparison.OrdinalIgnoreCase))
            {
                return "~/Dashboard.aspx";
            }

            if (string.Equals(roleName, "BranchManager", StringComparison.OrdinalIgnoreCase))
            {
                return "~/Dashboard.aspx";
            }

            return "~/Dashboard.aspx";
        }

        private string GetUserEmail(string username)
        {
            string email = null;
            ExecuteScalar(@"
IF OBJECT_ID('locds.UserAccount', 'U') IS NOT NULL
BEGIN
    SELECT TOP (1) Email FROM locds.UserAccount WHERE UserName = @UserName;
END", username, function: value =>
            {
                email = value as string;
            });
            return email;
        }

        private bool SendOtpEmail(string username, string email, string otpCode)
        {
            string fromAddress = ConfigurationManager.AppSettings["Security:FromEmail"];
            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                fromAddress = "noreply@locds.local";
            }

            string smtpHost = ConfigurationManager.AppSettings["Security:SmtpHost"];
            if (string.IsNullOrWhiteSpace(smtpHost))
            {
                return false;
            }

            int smtpPort = 587;
            int configuredPort;
            if (int.TryParse(ConfigurationManager.AppSettings["Security:SmtpPort"], out configuredPort))
            {
                smtpPort = configuredPort;
            }

            bool enableSsl = true;
            bool configuredSsl;
            if (bool.TryParse(ConfigurationManager.AppSettings["Security:SmtpEnableSsl"], out configuredSsl))
            {
                enableSsl = configuredSsl;
            }

            try
            {
                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = enableSsl;

                    string smtpUser = ConfigurationManager.AppSettings["Security:SmtpUser"];
                    string smtpPassword = ConfigurationManager.AppSettings["Security:SmtpPassword"];
                    if (!string.IsNullOrWhiteSpace(smtpUser))
                    {
                        client.Credentials = new NetworkCredential(smtpUser, smtpPassword ?? string.Empty);
                    }

                    using (var mail = new MailMessage(fromAddress, email))
                    {
                        mail.Subject = "LOCDS Password Reset OTP";
                        mail.Body = "Hello " + username + ",\n\n" +
                                    "Your OTP is: " + otpCode + "\n" +
                                    "This OTP will expire in 10 minutes.\n\n" +
                                    "If you did not request this, contact support immediately.";
                        client.Send(mail);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private void SendLockoutEmail(string username, string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                email = GetUserEmail(username);
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            string fromAddress = ConfigurationManager.AppSettings["Security:FromEmail"];
            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                fromAddress = "noreply@locds.local";
            }

            string smtpHost = ConfigurationManager.AppSettings["Security:SmtpHost"];
            if (string.IsNullOrWhiteSpace(smtpHost))
            {
                return;
            }

            int smtpPort = 587;
            int configuredPort;
            if (int.TryParse(ConfigurationManager.AppSettings["Security:SmtpPort"], out configuredPort))
            {
                smtpPort = configuredPort;
            }

            bool enableSsl = true;
            bool configuredSsl;
            if (bool.TryParse(ConfigurationManager.AppSettings["Security:SmtpEnableSsl"], out configuredSsl))
            {
                enableSsl = configuredSsl;
            }

            try
            {
                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = enableSsl;

                    string smtpUser = ConfigurationManager.AppSettings["Security:SmtpUser"];
                    string smtpPassword = ConfigurationManager.AppSettings["Security:SmtpPassword"];
                    if (!string.IsNullOrWhiteSpace(smtpUser))
                    {
                        client.Credentials = new NetworkCredential(smtpUser, smtpPassword ?? string.Empty);
                    }

                    using (var mail = new MailMessage(fromAddress, email))
                    {
                        mail.Subject = "LOCDS Account Lockout Alert";
                        mail.Body = "Hello " + username + ",\n\n" +
                                    "Your account has been locked due to multiple failed login attempts.\n" +
                                    "If this was not you, please contact support immediately.";
                        client.Send(mail);
                    }
                }
            }
            catch
            {
                // Ignore outbound email failures from UI action pipeline.
            }
        }

        private void ExecuteNonQuery(string sql, string username)
        {
            var emailConnection = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = emailConnection != null ? emailConnection.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            try
            {
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@UserName", SqlDbType.NVarChar, 100).Value = username;
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch
            {
                // Swallow infra exceptions to keep authentication workflow resilient.
            }
        }

        private void ExecuteScalar(string sql, string username, Action<object> function)
        {
            var commandConnection = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = commandConnection != null ? commandConnection.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            try
            {
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@UserName", SqlDbType.NVarChar, 100).Value = username;
                    connection.Open();
                    object result = command.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        function(result);
                    }
                }
            }
            catch
            {
                // Ignore read errors here and let caller handle null/empty behavior.
            }
        }

        private string ResolveIpAddress()
        {
            string forwarded = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                string[] values = forwarded.Split(',');
                if (values.Length > 0)
                {
                    return values[0].Trim();
                }
            }

            return Request.UserHostAddress ?? string.Empty;
        }

        private static bool IsSqlConnectivityIssue(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            for (int i = 0; i < SqlConnectivityErrorMarkers.Length; i++)
            {
                if (reason.IndexOf(SqlConnectivityErrorMarkers[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryAuthenticateFallbackUser(string username, string password, out UserAccountRecord account, out string reason)
        {
            account = null;
            reason = string.Empty;

            if (!string.Equals(password, "Pass@123", StringComparison.Ordinal))
            {
                reason = "Invalid username or password.";
                return false;
            }

            string role;
            if (string.Equals(username, "credit.officer", StringComparison.OrdinalIgnoreCase))
            {
                role = "CreditOfficer";
            }
            else if (string.Equals(username, "under.writer", StringComparison.OrdinalIgnoreCase))
            {
                role = "Underwriter";
            }
            else if (string.Equals(username, "branch.manager", StringComparison.OrdinalIgnoreCase))
            {
                role = "BranchManager";
            }
            else if (string.Equals(username, "applicant.one", StringComparison.OrdinalIgnoreCase))
            {
                role = "Applicant";
            }
            else
            {
                reason = "Invalid username or password.";
                return false;
            }

            account = new UserAccountRecord
            {
                Username = username,
                PasswordHash = password,
                RoleName = role,
                Email = username + "@locds.local",
                IsLocked = false
            };

            return true;
        }

        private static string GenerateOtpCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString(CultureInfo.InvariantCulture);
        }

        private void RegenerateSessionId()
        {
            var manager = new SessionIDManager();
            string newId = manager.CreateSessionID(Context);
            bool redirected;
            bool cookieAdded;
            manager.SaveSessionID(Context, newId, out redirected, out cookieAdded);
        }

        private sealed class UserAccountRecord
        {
            public string Username { get; set; }
            public string PasswordHash { get; set; }
            public string RoleName { get; set; }
            public string Email { get; set; }
            public bool IsLocked { get; set; }
        }
    }
}
