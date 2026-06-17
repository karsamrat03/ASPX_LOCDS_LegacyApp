using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;

namespace LOCDS.Web
{
    public partial class LoanOfferPage : Page
    {
        private const string ApplicationIdViewStateKey = "LoanOffer.ApplicationId";
        private const decimal ProcessingFeeRate = 0.0125m;
        private const string SqlHealthBannerSessionKey = "SqlHealthBanner";
        private const string FailFastOnSqlErrorSettingKey = "Demo:FailFastOnSqlError";
        private const string ShowSqlHealthBannerSettingKey = "Demo:ShowSqlHealthBanner";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (!IsApplicant())
                {
                    Response.Redirect("~/Errors/403.aspx", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }

                Guid applicationId = GetApplicationIdFromQuery();
                if (applicationId == Guid.Empty)
                {
                    ShowError("Invalid application identifier.");
                    ToggleOfferActions(false);
                    return;
                }

                ViewState[ApplicationIdViewStateKey] = applicationId;
                BindEmiDateSelector();
                BindOffer(applicationId);
            }
        }

        protected void btnSendOtp_Click(object sender, EventArgs e)
        {
            string otp = GenerateOtp();
            Session["LoanOfferOtp"] = otp;
            Session["LoanOfferOtpExpiresUtc"] = DateTime.UtcNow.AddMinutes(10);

            // In production, send the OTP over registered SMS/email channel.
            lblOtpMessage.CssClass = "text-success small d-block";
            lblOtpMessage.Text = "OTP sent successfully. For demo, use OTP: " + otp;
            RegisterScript("showOtpModal", "openOtpModal();");
        }

        protected void btnConfirmAccept_Click(object sender, EventArgs e)
        {
            Guid applicationId = GetApplicationIdFromState();
            if (applicationId == Guid.Empty)
            {
                ShowError("Application context is missing.");
                return;
            }

            if (!ValidateOtp(txtOtp.Text))
            {
                lblOtpMessage.CssClass = "text-danger small d-block";
                lblOtpMessage.Text = "Invalid or expired OTP. Please request a new OTP.";
                RegisterScript("showOtpModal", "openOtpModal();");
                return;
            }

            int emiDebitDate;
            if (!int.TryParse(ddlEmiDate.SelectedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out emiDebitDate))
            {
                emiDebitDate = 5;
            }

            PersistOfferAction(applicationId, "Accepted", emiDebitDate, otpVerified: true);
            UpdateApplicationStatus(applicationId, 7);
            SetTrackerState(accepted: true, disbursed: false);

            lblOtpMessage.Text = string.Empty;
            txtOtp.Text = string.Empty;
            Session.Remove("LoanOfferOtp");
            Session.Remove("LoanOfferOtpExpiresUtc");

            ShowSuccess("Offer accepted successfully. Disbursement tracker is now active.");
            RegisterScript("hideOtpModal", "closeOtpModal();");
            BindOffer(applicationId);
        }

        protected void btnRejectOffer_Click(object sender, EventArgs e)
        {
            Guid applicationId = GetApplicationIdFromState();
            if (applicationId == Guid.Empty)
            {
                ShowError("Application context is missing.");
                return;
            }

            PersistOfferAction(applicationId, "Rejected", null, otpVerified: false);
            UpdateApplicationStatus(applicationId, 5);
            SetTrackerState(accepted: false, disbursed: false);
            ToggleOfferActions(false);
            ShowSuccess("Offer rejected successfully.");
        }

        private void BindOffer(Guid applicationId)
        {
            OfferContext offer = FetchOffer(applicationId);
            if (offer == null)
            {
                if (IsFailFastOnSqlError())
                {
                    ShowError("Database is unavailable. Fail-fast mode is enabled, so fallback offer data is disabled.");
                }
                else
                {
                    ShowError("Unable to load offer details.");
                }

                ToggleOfferActions(false);
                return;
            }

            lblApplicationId.Text = applicationId.ToString("D", CultureInfo.InvariantCulture);
            lblApprovedAmount.Text = FormatMoney(offer.ApprovedAmount);
            lblInterestRate.Text = offer.InterestRate.ToString("0.00", CultureInfo.InvariantCulture) + "%";
            lblTenure.Text = offer.TenureMonths.ToString(CultureInfo.InvariantCulture) + " months";

            decimal emi = CalculateEmi(offer.ApprovedAmount, offer.InterestRate, offer.TenureMonths);
            lblEmi.Text = FormatMoney(emi);

            DataTable schedule = BuildAmortizationSchedule(offer.ApprovedAmount, offer.InterestRate, offer.TenureMonths, emi);
            gvAmortization.DataSource = schedule;
            gvAmortization.DataBind();

            decimal totalInterest = 0m;
            foreach (DataRow row in schedule.Rows)
            {
                totalInterest += Convert.ToDecimal(row["Interest"], CultureInfo.InvariantCulture);
            }

            decimal processingFee = Math.Round(offer.ApprovedAmount * ProcessingFeeRate, 2, MidpointRounding.AwayFromZero);
            decimal totalCost = offer.ApprovedAmount + totalInterest + processingFee;

            lblPrincipal.Text = FormatMoney(offer.ApprovedAmount);
            lblTotalInterest.Text = FormatMoney(totalInterest);
            lblProcessingFee.Text = FormatMoney(processingFee);
            lblTotalCost.Text = FormatMoney(totalCost);

            DateTime offerExpiryUtc = offer.OfferCreatedUtc.AddHours(48);
            hfOfferExpiryUtc.Value = offerExpiryUtc.ToString("o", CultureInfo.InvariantCulture);

            bool isExpired = DateTime.UtcNow > offerExpiryUtc;
            if (isExpired)
            {
                ToggleOfferActions(false);
                ShowError("Offer validity has expired. Please contact support for a refreshed offer.");
            }
            else
            {
                ToggleOfferActions(!offer.IsAccepted && !offer.IsRejected);
            }

            SetTrackerState(offer.IsAccepted || offer.IsDisbursed, offer.IsDisbursed);
        }

        private OfferContext FetchOffer(Guid applicationId)
        {
            var connection = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connection != null ? connection.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                SetSqlHealthBanner("Loan Offer is using fallback data because LOCDSConnection is unavailable.");
                if (IsFailFastOnSqlError())
                {
                    return null;
                }

                return BuildDemoOffer(applicationId);
            }

            const string sql = @"
SELECT TOP (1)
    LA.ApplicationId,
    LA.LoanAmount,
    LA.Tenure,
    LA.Status,
    LA.LastModifiedDate,
    UD.ApprovedAmount,
    UD.InterestRate,
    UD.DecidedAt
FROM locds.LoanApplication LA
LEFT JOIN locds.UnderwritingDecision UD ON UD.ApplicationId = LA.ApplicationId
WHERE LA.ApplicationId = @ApplicationId
ORDER BY UD.DecidedAt DESC;";

            try
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, sqlConnection))
                {
                    command.Parameters.Add("@ApplicationId", SqlDbType.UniqueIdentifier).Value = applicationId;
                    sqlConnection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return BuildDemoOffer(applicationId);
                        }

                        decimal loanAmount = reader["LoanAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["LoanAmount"], CultureInfo.InvariantCulture);
                        decimal approvedAmount = reader["ApprovedAmount"] == DBNull.Value ? loanAmount : Convert.ToDecimal(reader["ApprovedAmount"], CultureInfo.InvariantCulture);
                        decimal interestRate = reader["InterestRate"] == DBNull.Value ? 11m : Convert.ToDecimal(reader["InterestRate"], CultureInfo.InvariantCulture);
                        int tenure = reader["Tenure"] == DBNull.Value ? 36 : Convert.ToInt32(reader["Tenure"], CultureInfo.InvariantCulture);
                        int status = reader["Status"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Status"], CultureInfo.InvariantCulture);

                        DateTime baseUtc;
                        if (reader["DecidedAt"] != DBNull.Value)
                        {
                            baseUtc = DateTime.SpecifyKind(Convert.ToDateTime(reader["DecidedAt"], CultureInfo.InvariantCulture), DateTimeKind.Utc);
                        }
                        else if (reader["LastModifiedDate"] != DBNull.Value)
                        {
                            baseUtc = DateTime.SpecifyKind(Convert.ToDateTime(reader["LastModifiedDate"], CultureInfo.InvariantCulture), DateTimeKind.Utc);
                        }
                        else
                        {
                            baseUtc = DateTime.UtcNow;
                        }

                        return new OfferContext
                        {
                            ApprovedAmount = approvedAmount,
                            InterestRate = interestRate,
                            TenureMonths = tenure,
                            OfferCreatedUtc = baseUtc,
                            IsAccepted = status == 7 || status == 8,
                            IsDisbursed = status == 8,
                            IsRejected = status == 5
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                SetSqlHealthBanner("Loan Offer database error: " + ex.Message);
                if (IsFailFastOnSqlError())
                {
                    return null;
                }

                return BuildDemoOffer(applicationId);
            }
        }

        private static DataTable BuildAmortizationSchedule(decimal principal, decimal annualRate, int tenureMonths, decimal emi)
        {
            var table = new DataTable();
            table.Columns.Add("Month", typeof(int));
            table.Columns.Add("EMI", typeof(decimal));
            table.Columns.Add("Principal", typeof(decimal));
            table.Columns.Add("Interest", typeof(decimal));
            table.Columns.Add("ClosingBalance", typeof(decimal));

            if (principal <= 0m || annualRate < 0m || tenureMonths <= 0)
            {
                return table;
            }

            decimal monthlyRate = annualRate / 1200m;
            decimal balance = principal;

            for (int month = 1; month <= tenureMonths; month++)
            {
                decimal interest = Math.Round(balance * monthlyRate, 2, MidpointRounding.AwayFromZero);
                decimal principalPart = Math.Round(emi - interest, 2, MidpointRounding.AwayFromZero);

                if (month == tenureMonths)
                {
                    principalPart = balance;
                    emi = Math.Round(principalPart + interest, 2, MidpointRounding.AwayFromZero);
                }

                balance = Math.Round(balance - principalPart, 2, MidpointRounding.AwayFromZero);
                if (balance < 0m)
                {
                    balance = 0m;
                }

                table.Rows.Add(month, emi, principalPart, interest, balance);
            }

            return table;
        }

        private static decimal CalculateEmi(decimal principal, decimal annualRate, int tenureMonths)
        {
            if (principal <= 0m || tenureMonths <= 0)
            {
                return 0m;
            }

            decimal monthlyRate = annualRate / 1200m;
            if (monthlyRate <= 0m)
            {
                return Math.Round(principal / tenureMonths, 2, MidpointRounding.AwayFromZero);
            }

            double r = (double)monthlyRate;
            double n = tenureMonths;
            double p = (double)principal;
            double factor = Math.Pow(1 + r, n);

            return Math.Round((decimal)(p * r * factor / (factor - 1)), 2, MidpointRounding.AwayFromZero);
        }

        private void PersistOfferAction(Guid applicationId, string action, int? emiDate, bool otpVerified)
        {
            var connection = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connection != null ? connection.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            string userName = null;
            if (Context != null && Context.User != null && Context.User.Identity != null)
            {
                userName = Context.User.Identity.Name;
            }
            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = "Applicant";
            }

            const string sql = @"
IF OBJECT_ID('locds.LoanOfferAction', 'U') IS NULL
BEGIN
    CREATE TABLE locds.LoanOfferAction
    (
        OfferActionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_LoanOfferAction PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        ApplicationId NVARCHAR(64) NOT NULL,
        ActionTaken NVARCHAR(20) NOT NULL,
        EmiDebitDate TINYINT NULL,
        OtpVerified BIT NOT NULL,
        ActionBy NVARCHAR(120) NOT NULL,
        ActionAt DATETIME2(0) NOT NULL,
        CreatedDate DATETIME2(0) NOT NULL,
        LastModifiedDate DATETIME2(0) NOT NULL
    );
END;

INSERT INTO locds.LoanOfferAction
(
    ApplicationId,
    ActionTaken,
    EmiDebitDate,
    OtpVerified,
    ActionBy,
    ActionAt,
    CreatedDate,
    LastModifiedDate
)
VALUES
(
    @ApplicationId,
    @ActionTaken,
    @EmiDebitDate,
    @OtpVerified,
    @ActionBy,
    SYSUTCDATETIME(),
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
);";

            try
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, sqlConnection))
                {
                    command.Parameters.Add("@ApplicationId", SqlDbType.NVarChar, 64).Value = applicationId.ToString("D", CultureInfo.InvariantCulture);
                    command.Parameters.Add("@ActionTaken", SqlDbType.NVarChar, 20).Value = action;
                    command.Parameters.Add("@EmiDebitDate", SqlDbType.TinyInt).Value = (object)emiDate ?? DBNull.Value;
                    command.Parameters.Add("@OtpVerified", SqlDbType.Bit).Value = otpVerified;
                    command.Parameters.Add("@ActionBy", SqlDbType.NVarChar, 120).Value = userName;
                    sqlConnection.Open();
                    command.ExecuteNonQuery();
                }

                ClearSqlHealthBanner();
            }
            catch (Exception ex)
            {
                SetSqlHealthBanner("Loan Offer action persistence error: " + ex.Message);
            }
        }

        private void UpdateApplicationStatus(Guid applicationId, int status)
        {
            var connection = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connection != null ? connection.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                SetSqlHealthBanner("Loan Offer status update failed because LOCDSConnection is unavailable.");
                return;
            }

            const string sql = @"
UPDATE locds.LoanApplication
SET Status = @Status,
    LastModifiedDate = SYSUTCDATETIME()
WHERE ApplicationId = @ApplicationId;";

            try
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, sqlConnection))
                {
                    command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = status;
                    command.Parameters.Add("@ApplicationId", SqlDbType.UniqueIdentifier).Value = applicationId;
                    sqlConnection.Open();
                    command.ExecuteNonQuery();
                }

                ClearSqlHealthBanner();
            }
            catch (Exception ex)
            {
                SetSqlHealthBanner("Loan Offer status update error: " + ex.Message);
            }
        }

        private static string FormatMoney(decimal value)
        {
            return "Rs " + value.ToString("N2", CultureInfo.InvariantCulture);
        }

        private static string GenerateOtp()
        {
            byte[] bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            int value = Math.Abs(BitConverter.ToInt32(bytes, 0));
            int otp = (value % 900000) + 100000;
            return otp.ToString(CultureInfo.InvariantCulture);
        }

        private bool ValidateOtp(string providedOtp)
        {
            string expectedOtp = Session["LoanOfferOtp"] as string;
            object expiryObj = Session["LoanOfferOtpExpiresUtc"];
            if (string.IsNullOrWhiteSpace(expectedOtp) || expiryObj == null)
            {
                return false;
            }

            DateTime expiryUtc;
            if (!(expiryObj is DateTime) || !DateTime.TryParse(expiryObj.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out expiryUtc))
            {
                expiryUtc = (DateTime)expiryObj;
            }

            if (DateTime.UtcNow > expiryUtc)
            {
                return false;
            }

            return string.Equals((providedOtp ?? string.Empty).Trim(), expectedOtp, StringComparison.Ordinal);
        }

        private void BindEmiDateSelector()
        {
            if (ddlEmiDate.Items.Count > 0)
            {
                ddlEmiDate.SelectedValue = "5";
            }
        }

        private void ToggleOfferActions(bool enabled)
        {
            btnAcceptOffer.Enabled = enabled;
            btnRejectOffer.Enabled = enabled;
            ddlEmiDate.Enabled = enabled;
        }

        private void SetTrackerState(bool accepted, bool disbursed)
        {
            pnlDisbursementTracker.Visible = accepted;
            if (!accepted)
            {
                return;
            }

            step1.Attributes["class"] = "tracker-step tracker-done";
            step2.Attributes["class"] = "tracker-step " + (disbursed ? "tracker-done" : "tracker-active");
            step3.Attributes["class"] = "tracker-step " + (disbursed ? "tracker-done" : "tracker-pending");
            step4.Attributes["class"] = "tracker-step " + (disbursed ? "tracker-done" : "tracker-pending");
        }

        private bool IsApplicant()
        {
            string role = Session != null ? Session["UserRole"] as string : null;
            FormsIdentity formsIdentity = null;
            if (Context != null && Context.User != null)
            {
                formsIdentity = Context.User.Identity as FormsIdentity;
            }

            if (string.IsNullOrWhiteSpace(role) && formsIdentity != null)
            {
                role = formsIdentity.Ticket.UserData;
            }

            if (string.IsNullOrWhiteSpace(role) && Roles.Enabled)
            {
                string[] roles = Roles.GetRolesForUser();
                if (roles != null && roles.Length > 0)
                {
                    role = roles[0];
                }
            }

            return string.IsNullOrWhiteSpace(role)
                || string.Equals(role, "Applicant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "CreditOfficer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Underwriter", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "BranchManager", StringComparison.OrdinalIgnoreCase);
        }

        private static OfferContext BuildDemoOffer(Guid applicationId)
        {
            decimal principal = 750000m;

            return new OfferContext
            {
                ApprovedAmount = principal,
                InterestRate = 10.75m,
                TenureMonths = 60,
                OfferCreatedUtc = DateTime.UtcNow,
                IsAccepted = false,
                IsDisbursed = false,
                IsRejected = false
            };
        }

        private Guid GetApplicationIdFromQuery()
        {
            string raw = Request.QueryString["applicationId"];
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = Request.QueryString["appId"];
            }

            Guid id;
            return Guid.TryParse(raw, out id) ? id : Guid.Empty;
        }

        private Guid GetApplicationIdFromState()
        {
            object value = ViewState[ApplicationIdViewStateKey];
            Guid id;
            return value != null && Guid.TryParse(value.ToString(), out id) ? id : Guid.Empty;
        }

        private void RegisterScript(string key, string script)
        {
            ScriptManager.RegisterStartupScript(this, GetType(), key, script, true);
        }

        private void ShowError(string message)
        {
            lblPageMessage.CssClass = "alert alert-danger d-block";
            lblPageMessage.Text = HttpUtility.HtmlEncode(message);
        }

        private void ShowSuccess(string message)
        {
            lblPageMessage.CssClass = "alert alert-success d-block";
            lblPageMessage.Text = HttpUtility.HtmlEncode(message);
        }

        private void SetSqlHealthBanner(string message)
        {
            if (!IsSqlHealthBannerEnabled() || Session == null)
            {
                return;
            }

            Session[SqlHealthBannerSessionKey] = message;
        }

        private void ClearSqlHealthBanner()
        {
            if (Session == null)
            {
                return;
            }

            Session.Remove(SqlHealthBannerSessionKey);
        }

        private static bool IsFailFastOnSqlError()
        {
            return GetAppSettingAsBool(FailFastOnSqlErrorSettingKey, false);
        }

        private static bool IsSqlHealthBannerEnabled()
        {
            return GetAppSettingAsBool(ShowSqlHealthBannerSettingKey, true);
        }

        private static bool GetAppSettingAsBool(string key, bool defaultValue)
        {
            string raw = ConfigurationManager.AppSettings[key];
            bool parsed;
            return bool.TryParse(raw, out parsed) ? parsed : defaultValue;
        }

        private sealed class OfferContext
        {
            public decimal ApprovedAmount { get; set; }
            public decimal InterestRate { get; set; }
            public int TenureMonths { get; set; }
            public DateTime OfferCreatedUtc { get; set; }
            public bool IsAccepted { get; set; }
            public bool IsDisbursed { get; set; }
            public bool IsRejected { get; set; }
        }
    }
}
