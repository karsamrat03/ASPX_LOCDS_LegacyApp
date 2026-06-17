using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using System.Web.UI;

namespace LOCDS.Web
{
    public partial class DecisionSheet : Page
    {
        private const string SqlHealthBannerSessionKey = "SqlHealthBanner";
        private const string FailFastOnSqlErrorSettingKey = "Demo:FailFastOnSqlError";
        private const string ShowSqlHealthBannerSettingKey = "Demo:ShowSqlHealthBanner";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                Guid applicationId = GetApplicationId();
                if (applicationId == Guid.Empty)
                {
                    return;
                }

                BindSheet(applicationId);
            }
        }

        private void BindSheet(Guid applicationId)
        {
            var connectionSetting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connectionSetting != null ? connectionSetting.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                SetSqlHealthBanner("Decision Sheet is using fallback data because LOCDSConnection is unavailable.");
                if (IsFailFastOnSqlError())
                {
                    BindSqlUnavailable(applicationId, "Database connection is unavailable.");
                    return;
                }

                BindFromSessionOrDemo(applicationId);
                return;
            }

            const string sql = @"
SELECT TOP (1)
    LA.ApplicationId,
    CONCAT(A.FirstName, ' ', A.LastName) AS ApplicantName,
    LA.LoanAmount,
    UD.RecommendedAction,
    UD.ApprovedAmount,
    UD.InterestRate,
    UD.Remarks,
    UD.DecidedBy,
    UD.DecidedAt
FROM locds.LoanApplication LA
INNER JOIN locds.Applicant A ON A.ApplicantId = LA.ApplicantId
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
                            BindFromSessionOrDemo(applicationId);
                            return;
                        }

                        lblApplicationId.Text = Convert.ToString(reader["ApplicationId"], CultureInfo.InvariantCulture) ?? string.Empty;
                        lblApplicantName.Text = HttpUtility.HtmlEncode(Convert.ToString(reader["ApplicantName"], CultureInfo.InvariantCulture) ?? string.Empty);
                        lblRequestedAmount.Text = reader["LoanAmount"] == DBNull.Value ? "-" : Convert.ToDecimal(reader["LoanAmount"], CultureInfo.InvariantCulture).ToString("N2", CultureInfo.InvariantCulture);
                        lblRecommendedDecision.Text = Convert.ToString(reader["RecommendedAction"], CultureInfo.InvariantCulture) ?? "-";
                        lblManualDecision.Text = lblRecommendedDecision.Text;
                        lblApprovedAmount.Text = reader["ApprovedAmount"] == DBNull.Value ? "-" : Convert.ToDecimal(reader["ApprovedAmount"], CultureInfo.InvariantCulture).ToString("N2", CultureInfo.InvariantCulture);
                        lblInterestRate.Text = reader["InterestRate"] == DBNull.Value ? "-" : Convert.ToDecimal(reader["InterestRate"], CultureInfo.InvariantCulture).ToString("N2", CultureInfo.InvariantCulture) + "%";
                        lblRemarks.Text = HttpUtility.HtmlEncode(Convert.ToString(reader["Remarks"], CultureInfo.InvariantCulture) ?? string.Empty);
                        lblDecidedBy.Text = HttpUtility.HtmlEncode(Convert.ToString(reader["DecidedBy"], CultureInfo.InvariantCulture) ?? string.Empty);
                        lblDecidedAt.Text = reader["DecidedAt"] == DBNull.Value ? "-" : Convert.ToDateTime(reader["DecidedAt"], CultureInfo.InvariantCulture).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                    }
                }

                ClearSqlHealthBanner();
            }
            catch (Exception ex)
            {
                SetSqlHealthBanner("Decision Sheet database error: " + ex.Message);
                if (IsFailFastOnSqlError())
                {
                    BindSqlUnavailable(applicationId, "Database query failed while loading decision sheet.");
                    return;
                }

                BindFromSessionOrDemo(applicationId);
            }
        }

        private void BindSqlUnavailable(Guid applicationId, string reason)
        {
            lblApplicationId.Text = applicationId.ToString("D", CultureInfo.InvariantCulture);
            lblApplicantName.Text = "-";
            lblRequestedAmount.Text = "-";
            lblRecommendedDecision.Text = "-";
            lblManualDecision.Text = "-";
            lblApprovedAmount.Text = "-";
            lblInterestRate.Text = "-";
            lblRemarks.Text = HttpUtility.HtmlEncode("Fail-fast mode is enabled. " + reason);
            lblDecidedBy.Text = "-";
            lblDecidedAt.Text = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private void BindFromSessionOrDemo(Guid applicationId)
        {
            lblApplicationId.Text = (Session["PrintDecision.ApplicationId"] as string) ?? applicationId.ToString("D", CultureInfo.InvariantCulture);
            lblApplicantName.Text = HttpUtility.HtmlEncode((Session["PrintDecision.ApplicantName"] as string) ?? "Priya Sharma");
            lblRequestedAmount.Text = (Session["PrintDecision.RequestedAmount"] as string) ?? "750,000.00";
            lblRecommendedDecision.Text = (Session["PrintDecision.RecommendedDecision"] as string) ?? "Review";
            lblManualDecision.Text = (Session["PrintDecision.ManualDecision"] as string) ?? lblRecommendedDecision.Text;
            lblApprovedAmount.Text = (Session["PrintDecision.ApprovedAmount"] as string) ?? "700,000.00";
            lblInterestRate.Text = (Session["PrintDecision.InterestRate"] as string) ?? "10.75%";
            lblRemarks.Text = HttpUtility.HtmlEncode((Session["PrintDecision.Remarks"] as string) ?? "Demo decision sheet generated without database connectivity.");
            lblDecidedBy.Text = HttpUtility.HtmlEncode((Session["PrintDecision.DecidedBy"] as string) ?? "under.writer");
            lblDecidedAt.Text = (Session["PrintDecision.DecidedAt"] as string) ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private Guid GetApplicationId()
        {
            string raw = Request.QueryString["appId"];
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = Request.QueryString["applicationId"];
            }

            Guid id;
            return Guid.TryParse(raw, out id) ? id : Guid.Empty;
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
    }
}
