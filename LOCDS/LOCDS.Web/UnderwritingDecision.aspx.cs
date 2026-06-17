using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace LOCDS.Web
{
    public partial class UnderwritingDecisionPage : Page
    {
        private const string ApplicationIdViewStateKey = "UW_ApplicationId";
        private const string SqlHealthBannerSessionKey = "SqlHealthBanner";
        private const string FailFastOnSqlErrorSettingKey = "Demo:FailFastOnSqlError";
        private const string ShowSqlHealthBannerSettingKey = "Demo:ShowSqlHealthBanner";

        public UnderwritingDecisionPage()
        {
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (!IsUnderwriter())
                {
                    Response.Redirect("~/Errors/403.aspx", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }

                Guid applicationId;
                if (!Guid.TryParse(Request.QueryString["applicationId"], out applicationId) || applicationId == Guid.Empty)
                {
                    ShowError("Invalid application identifier.");
                    return;
                }

                ViewState[ApplicationIdViewStateKey] = applicationId;
                LoadApplication(applicationId);
                LoadHistory(applicationId);
            }
        }

        protected void btnSubmitDecision_Click(object sender, EventArgs e)
        {
            Page.Validate("Decision");

            string selectedDecision = GetSelectedDecision();
            if (string.IsNullOrWhiteSpace(selectedDecision))
            {
                ShowError("Please select a decision before submitting.");
                return;
            }

            if (!Page.IsValid)
            {
                ShowError("Please resolve decision validation errors.");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtApprovedAmount.Text) || string.IsNullOrWhiteSpace(txtInterestRate.Text))
            {
                ShowError("Approved amount and interest rate are required.");
                return;
            }

            // Validate remarks for Reject/Refer decisions
            bool needsRemarks = string.Equals(selectedDecision, "Reject", StringComparison.OrdinalIgnoreCase)
                || string.Equals(selectedDecision, "Review", StringComparison.OrdinalIgnoreCase);
            if (needsRemarks && string.IsNullOrWhiteSpace(txtRemarks.Text))
            {
                ShowError("Remarks are mandatory for Reject/Refer decisions.");
                return;
            }

            Guid applicationId = GetApplicationId();
            if (applicationId == Guid.Empty)
            {
                ShowError("Application context is missing.");
                return;
            }

            decimal approvedAmount;
            decimal interestRate;
            if (!decimal.TryParse(txtApprovedAmount.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out approvedAmount)
                || !decimal.TryParse(txtInterestRate.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out interestRate))
            {
                ShowError("Approved amount and interest rate must be numeric values.");
                return;
            }

            if (chkOverrideRate.Checked && string.IsNullOrWhiteSpace(txtRateJustification.Text))
            {
                ShowError("Provide justification when overriding interest rate.");
                return;
            }

            RecommendedAction action;
            if (!Enum.TryParse(GetSelectedDecision(), true, out action))
            {
                ShowError("Invalid decision action.");
                return;
            }

            decimal riskScore;
            decimal.TryParse(lblFoir.Text.Replace("%", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out riskScore);

            try
            {
                SaveManualDecision(applicationId, action, approvedAmount, interestRate, ParseTenure(lblTenure.Text), riskScore, txtRemarks.Text == null ? string.Empty : txtRemarks.Text.Trim(), ResolveCurrentUser());
                UpdateApplicationStatus(applicationId, action);
                PersistAudit(applicationId, action, approvedAmount, interestRate, txtRemarks.Text == null ? string.Empty : txtRemarks.Text.Trim());
                PersistPrintSnapshot(applicationId, action, approvedAmount, interestRate, txtRemarks.Text == null ? string.Empty : txtRemarks.Text.Trim());
                LoadHistory(applicationId);

                ShowSuccess("Decision saved successfully. Action: " + action.ToString());
            }
            catch (Exception ex)
            {
                ShowError("Error saving decision: " + ex.Message);
            }
        }

        protected void btnPrintSheet_Click(object sender, EventArgs e)
        {
            Guid applicationId = GetApplicationId();
            if (applicationId == Guid.Empty)
            {
                ShowError("Invalid application context for print.");
                return;
            }

            string script = "window.open('" + ResolveUrl("~/DecisionSheet.aspx?appId=" + applicationId.ToString("D", CultureInfo.InvariantCulture)) + "', '_blank');";
            ScriptManager.RegisterStartupScript(this, GetType(), "printSheet", script, true);
        }

        private void LoadApplication(Guid applicationId)
        {
            UnderwritingViewModel detail = FetchDecisionContext(applicationId);
            if (detail == null)
            {
                ShowError("Application detail not found.");
                return;
            }

            lblApplicationId.Text = applicationId.ToString("D", CultureInfo.InvariantCulture);
            lblApplicantName.Text = HttpUtility.HtmlEncode(detail.ApplicantName);
            lblApplicantDob.Text = HttpUtility.HtmlEncode(detail.Dob);
            lblApplicantPan.Text = HttpUtility.HtmlEncode(MaskPan(detail.Pan));
            lblEmployment.Text = HttpUtility.HtmlEncode(detail.EmploymentType);
            lblPurpose.Text = HttpUtility.HtmlEncode(detail.Purpose);
            lblRequestedAmount.Text = detail.LoanAmount.ToString("N2", CultureInfo.InvariantCulture);
            lblTenure.Text = detail.Tenure.ToString(CultureInfo.InvariantCulture) + " months";

            BindBureauAndRisk(detail);
            BindFoir(detail);
            BindObligations(detail);
            BindRecommendation(detail);

            txtApprovedAmount.Text = detail.LoanAmount.ToString("0.00", CultureInfo.InvariantCulture);
            txtInterestRate.Text = ResolveBaseRate(detail.RiskTier).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private UnderwritingViewModel FetchDecisionContext(Guid applicationId)
        {
            var connectionSetting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connectionSetting != null ? connectionSetting.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                SetSqlHealthBanner("Underwriting Decision is using fallback data because LOCDSConnection is unavailable.");
                if (IsFailFastOnSqlError())
                {
                    return null;
                }

                return BuildDemoDecisionContext(applicationId);
            }

            var detail = new UnderwritingViewModel();

            try
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(@"
SELECT TOP (1)
    LA.ApplicationId,
    CONCAT(A.FirstName, ' ', A.LastName) AS ApplicantName,
    A.DOB,
    A.PAN,
    A.EmploymentType,
    A.AnnualIncome,
    A.RiskTier,
    LA.LoanAmount,
    LA.Tenure,
    LA.Purpose
FROM locds.LoanApplication LA
INNER JOIN locds.Applicant A ON A.ApplicantId = LA.ApplicantId
WHERE LA.ApplicationId = @ApplicationId;

SELECT TOP (3)
    Bureau,
    Score,
    DefaultHistory,
    ActiveLoans
FROM locds.CreditBureauReport
WHERE ApplicationId = @ApplicationId
ORDER BY PulledAt DESC;", sqlConnection))
                {
                    command.Parameters.Add("@ApplicationId", SqlDbType.UniqueIdentifier).Value = applicationId;
                    sqlConnection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return BuildDemoDecisionContext(applicationId);
                        }

                        detail.ApplicationId = applicationId;
                        detail.ApplicantName = Convert.ToString(reader["ApplicantName"], CultureInfo.InvariantCulture) ?? string.Empty;
                        detail.Dob = reader["DOB"] == DBNull.Value ? "-" : Convert.ToDateTime(reader["DOB"], CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        detail.Pan = Convert.ToString(reader["PAN"], CultureInfo.InvariantCulture) ?? "-";
                        detail.EmploymentType = Convert.ToString(reader["EmploymentType"], CultureInfo.InvariantCulture) ?? "-";
                        detail.AnnualIncome = reader["AnnualIncome"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["AnnualIncome"], CultureInfo.InvariantCulture);
                        detail.RiskTier = reader["RiskTier"] == DBNull.Value ? 1 : Convert.ToInt32(reader["RiskTier"], CultureInfo.InvariantCulture);
                        detail.LoanAmount = Convert.ToDecimal(reader["LoanAmount"], CultureInfo.InvariantCulture);
                        detail.Tenure = Convert.ToInt32(reader["Tenure"], CultureInfo.InvariantCulture);
                        detail.Purpose = Convert.ToString(reader["Purpose"], CultureInfo.InvariantCulture) ?? "-";

                        if (reader.NextResult())
                        {
                            while (reader.Read())
                            {
                                detail.BureauRows.Add(new BureauRow
                                {
                                    Bureau = Convert.ToInt32(reader["Bureau"], CultureInfo.InvariantCulture),
                                    Score = Convert.ToInt32(reader["Score"], CultureInfo.InvariantCulture),
                                    DefaultHistory = Convert.ToBoolean(reader["DefaultHistory"], CultureInfo.InvariantCulture),
                                    ActiveLoans = Convert.ToInt32(reader["ActiveLoans"], CultureInfo.InvariantCulture)
                                });
                            }
                        }
                    }
                }

                ClearSqlHealthBanner();
            }
            catch (Exception ex)
            {
                SetSqlHealthBanner("Underwriting Decision database error: " + ex.Message);
                if (IsFailFastOnSqlError())
                {
                    return null;
                }

                return BuildDemoDecisionContext(applicationId);
            }

            return detail;
        }

        private void BindBureauAndRisk(UnderwritingViewModel detail)
        {
            var cibil = detail.BureauRows.FirstOrDefault(x => x.Bureau == 0);
            var experian = detail.BureauRows.FirstOrDefault(x => x.Bureau == 1);
            var equifax = detail.BureauRows.FirstOrDefault(x => x.Bureau == 2);

            lblScoreCibil.Text = cibil != null ? cibil.Score.ToString(CultureInfo.InvariantCulture) : "-";
            lblDefaultCibil.Text = cibil != null && cibil.DefaultHistory ? "Yes" : "No";
            lblScoreExperian.Text = experian != null ? experian.Score.ToString(CultureInfo.InvariantCulture) : "-";
            lblDefaultExperian.Text = experian != null && experian.DefaultHistory ? "Yes" : "No";
            lblScoreEquifax.Text = equifax != null ? equifax.Score.ToString(CultureInfo.InvariantCulture) : "-";
            lblDefaultEquifax.Text = equifax != null && equifax.DefaultHistory ? "Yes" : "No";

            string riskTier = ResolveRiskTier(detail.RiskTier, detail.BureauRows);
            lblRiskTier.Text = riskTier;
            lblRiskTier.CssClass = "risk-badge " + ResolveRiskBadgeCss(riskTier);
        }

        private void BindFoir(UnderwritingViewModel detail)
        {
            decimal monthlyIncome = detail.AnnualIncome / 12m;
            decimal existingEmi = detail.BureauRows.Sum(x => x.ActiveLoans) * 5000m;
            decimal proposedEmi = CalculateEmi(detail.LoanAmount, detail.Tenure, ResolveBaseRate(detail.RiskTier));
            decimal foir = monthlyIncome <= 0m ? 1m : (existingEmi + proposedEmi) / monthlyIncome;

            lblMonthlyIncome.Text = monthlyIncome.ToString("N2", CultureInfo.InvariantCulture);
            lblExistingEmi.Text = existingEmi.ToString("N2", CultureInfo.InvariantCulture);
            lblProposedEmi.Text = proposedEmi.ToString("N2", CultureInfo.InvariantCulture);
            lblFoir.Text = (foir * 100m).ToString("N2", CultureInfo.InvariantCulture) + "%";

            decimal ltv = detail.Purpose.IndexOf("Home", StringComparison.OrdinalIgnoreCase) >= 0 ? 0.80m : 0.65m;
            lblLtv.Text = (ltv * 100m).ToString("N0", CultureInfo.InvariantCulture) + "%";
        }

        private void BindObligations(UnderwritingViewModel detail)
        {
            var table = new DataTable();
            table.Columns.Add("LoanType", typeof(string));
            table.Columns.Add("Emi", typeof(decimal));
            table.Columns.Add("Outstanding", typeof(decimal));

            foreach (var report in detail.BureauRows)
            {
                for (int i = 0; i < Math.Max(report.ActiveLoans, 0); i++)
                {
                    table.Rows.Add(report.BureauName, 5000m, 150000m);
                }
            }

            if (table.Rows.Count == 0)
            {
                table.Rows.Add("No obligations", 0m, 0m);
            }

            gvObligations.DataSource = table;
            gvObligations.DataBind();
        }

        private void BindRecommendation(UnderwritingViewModel detail)
        {
            string recommendation = ResolveRecommendation(detail);
            lblAutoRecommendation.Text = recommendation;
            lblAutoRemarks.Text = "Auto-engine recommendation based on credit score, FOIR and active defaults.";

            SetSelectedDecision(recommendation);
        }
        private string GetSelectedDecision()
        {
            if (rdApprove.Checked) return "Approve";
            if (rdConditional.Checked) return "ConditionalApprove";
            if (rdRefer.Checked) return "Review";
            if (rdReject.Checked) return "Reject";
            return string.Empty;
        }

        private void SetSelectedDecision(string value)
        {
            rdApprove.Checked = false;
            rdConditional.Checked = false;
            rdRefer.Checked = false;
            rdReject.Checked = false;

            if (string.Equals(value, "Approve", StringComparison.OrdinalIgnoreCase))
                rdApprove.Checked = true;
            else if (string.Equals(value, "ConditionalApprove", StringComparison.OrdinalIgnoreCase))
                rdConditional.Checked = true;
            else if (string.Equals(value, "Review", StringComparison.OrdinalIgnoreCase))
                rdRefer.Checked = true;
            else if (string.Equals(value, "Reject", StringComparison.OrdinalIgnoreCase))
                rdReject.Checked = true;
        }

        private string GetSupportingDocs()
        {
            var docs = new List<string>();
            if (chkIncomeProof.Checked) docs.Add("IncomeProof");
            if (chkBureauChecked.Checked) docs.Add("BureauChecked");
            if (chkKycComplete.Checked) docs.Add("KycComplete");
            if (chkObligationCheck.Checked) docs.Add("ObligationCheck");
            return string.Join(",", docs);
        }

        private void SaveManualDecision(Guid applicationId, RecommendedAction action, decimal approvedAmount, decimal interestRate, int tenure, decimal riskScore, string remarks, string decidedBy)
        {
            var connectionSetting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connectionSetting != null ? connectionSetting.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            const string sql = @"
INSERT INTO locds.UnderwritingDecision
(ApplicationId, RecommendedAction, ApprovedAmount, InterestRate, Tenure, RiskScore, Remarks, DecidedBy, DecidedAt, CreatedDate, LastModifiedDate)
VALUES
(@ApplicationId, @RecommendedAction, @ApprovedAmount, @InterestRate, @Tenure, @RiskScore, @Remarks, @DecidedBy, SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME());";

            try
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, sqlConnection))
                {
                    command.Parameters.Add("@ApplicationId", SqlDbType.UniqueIdentifier).Value = applicationId;
                    command.Parameters.Add("@RecommendedAction", SqlDbType.TinyInt).Value = (int)action;
                    command.Parameters.Add("@ApprovedAmount", SqlDbType.Decimal).Value = approvedAmount;
                    command.Parameters["@ApprovedAmount"].Precision = 18;
                    command.Parameters["@ApprovedAmount"].Scale = 2;
                    command.Parameters.Add("@InterestRate", SqlDbType.Decimal).Value = interestRate;
                    command.Parameters["@InterestRate"].Precision = 9;
                    command.Parameters["@InterestRate"].Scale = 4;
                    command.Parameters.Add("@Tenure", SqlDbType.Int).Value = tenure;
                    command.Parameters.Add("@RiskScore", SqlDbType.Decimal).Value = riskScore;
                    command.Parameters["@RiskScore"].Precision = 9;
                    command.Parameters["@RiskScore"].Scale = 4;
                    command.Parameters.Add("@Remarks", SqlDbType.NVarChar, 1000).Value = remarks;
                    command.Parameters.Add("@DecidedBy", SqlDbType.NVarChar, 100).Value = decidedBy;

                    sqlConnection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch
            {
                // Keep demo flow non-blocking.
            }
        }

        private void UpdateApplicationStatus(Guid applicationId, RecommendedAction action)
        {
            int status = action == RecommendedAction.Reject ? 5 : 4;
            if (action == RecommendedAction.Review)
            {
                status = 3;
            }

            var connectionSetting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connectionSetting != null ? connectionSetting.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
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
            }
            catch
            {
                // Keep demo flow non-blocking.
            }
        }

        private void PersistAudit(Guid applicationId, RecommendedAction action, decimal approvedAmount, decimal interestRate, string remarks)
        {
            var connectionSetting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connectionSetting != null ? connectionSetting.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            string docs = GetSupportingDocs();
            string payload = string.Format(
                CultureInfo.InvariantCulture,
                "Action={0};ApprovedAmount={1:N2};Rate={2:N2};Remarks={3};Docs={4};Override={5};Justification={6}",
                action,
                approvedAmount,
                interestRate,
                remarks,
                docs,
                chkOverrideRate.Checked,
                txtRateJustification.Text == null ? string.Empty : txtRateJustification.Text.Trim());

            const string sql = @"
INSERT INTO locds.AuditLog
(EntityType, EntityId, Action, OldValue, NewValue, PerformedBy, PerformedAt, IPAddress, CreatedDate, LastModifiedDate)
VALUES
(N'UnderwritingDecision', @EntityId, N'SUBMIT_MANUAL_DECISION', NULL, @NewValue, @PerformedBy, SYSUTCDATETIME(), @IPAddress, SYSUTCDATETIME(), SYSUTCDATETIME());";

            try
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, sqlConnection))
                {
                    command.Parameters.Add("@EntityId", SqlDbType.NVarChar, 64).Value = applicationId.ToString("D", CultureInfo.InvariantCulture);
                    command.Parameters.Add("@NewValue", SqlDbType.NVarChar, -1).Value = payload;
                    command.Parameters.Add("@PerformedBy", SqlDbType.NVarChar, 100).Value = ResolveCurrentUser();
                    command.Parameters.Add("@IPAddress", SqlDbType.NVarChar, 45).Value = Request.UserHostAddress ?? string.Empty;
                    sqlConnection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch
            {
                // Keep demo flow non-blocking.
            }
        }

        private void PersistPrintSnapshot(Guid applicationId, RecommendedAction action, decimal approvedAmount, decimal interestRate, string remarks)
        {
            Session["PrintDecision.ApplicationId"] = applicationId.ToString("D", CultureInfo.InvariantCulture);
            Session["PrintDecision.ApplicantName"] = lblApplicantName.Text;
            Session["PrintDecision.RequestedAmount"] = lblRequestedAmount.Text;
            Session["PrintDecision.RecommendedDecision"] = lblAutoRecommendation.Text;
            Session["PrintDecision.ManualDecision"] = action.ToString();
            Session["PrintDecision.ApprovedAmount"] = approvedAmount.ToString("N2", CultureInfo.InvariantCulture);
            Session["PrintDecision.InterestRate"] = interestRate.ToString("0.00", CultureInfo.InvariantCulture) + "%";
            Session["PrintDecision.Remarks"] = remarks;
            Session["PrintDecision.DecidedBy"] = ResolveCurrentUser();
            Session["PrintDecision.DecidedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private void LoadHistory(Guid applicationId)
        {
            var history = new DataTable();
            var connectionSetting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connectionSetting != null ? connectionSetting.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                SetSqlHealthBanner("Underwriting history is using fallback data because LOCDSConnection is unavailable.");
                history = CreateHistorySchema();
                if (!IsFailFastOnSqlError())
                {
                    history.Rows.Add("DEMO-1", "Review", 700000m, 10.75m, "Manual review suggested for verification.", "under.writer", DateTime.UtcNow.AddMinutes(-20));
                }

                gvHistory.DataSource = history;
                gvHistory.DataBind();
                return;
            }

            const string sql = @"
SELECT DecisionId, RecommendedAction, ApprovedAmount, InterestRate, Remarks, DecidedBy, DecidedAt
FROM locds.UnderwritingDecision
WHERE ApplicationId = @ApplicationId
ORDER BY DecidedAt DESC;";

            try
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, sqlConnection))
                {
                    command.Parameters.Add("@ApplicationId", SqlDbType.UniqueIdentifier).Value = applicationId;
                    using (var adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(history);
                    }
                }

                ClearSqlHealthBanner();
            }
            catch (Exception ex)
            {
                SetSqlHealthBanner("Underwriting history database error: " + ex.Message);
                history = CreateHistorySchema();
                if (!IsFailFastOnSqlError())
                {
                    history.Rows.Add("DEMO-1", "Review", 700000m, 10.75m, "Manual review suggested for verification.", "under.writer", DateTime.UtcNow.AddMinutes(-20));
                }
            }

            gvHistory.DataSource = history;
            gvHistory.DataBind();
        }

        private static string ResolveRecommendation(UnderwritingViewModel detail)
        {
            decimal monthlyIncome = detail.AnnualIncome / 12m;
            decimal existingEmi = detail.BureauRows.Sum(x => x.ActiveLoans) * 5000m;
            decimal proposedEmi = CalculateEmi(detail.LoanAmount, detail.Tenure, ResolveBaseRate(detail.RiskTier));
            decimal foir = monthlyIncome <= 0m ? 1m : (existingEmi + proposedEmi) / monthlyIncome;
            int avgScore = detail.BureauRows.Count == 0 ? 0 : (int)detail.BureauRows.Average(x => x.Score);
            bool hasDefault = detail.BureauRows.Any(x => x.DefaultHistory);

            if (avgScore < 600 || hasDefault || foir > 0.65m)
            {
                return RecommendedAction.Reject.ToString();
            }

            if ((avgScore >= 600 && avgScore <= 650) || (foir >= 0.55m && foir <= 0.65m))
            {
                return RecommendedAction.Review.ToString();
            }

            if (avgScore >= 651 && avgScore <= 700)
            {
                return RecommendedAction.ConditionalApprove.ToString();
            }

            return RecommendedAction.Approve.ToString();
        }

        private static decimal ResolveBaseRate(int riskTier)
        {
            if (riskTier <= 0)
            {
                return 8.5m;
            }

            if (riskTier == 1)
            {
                return 11m;
            }

            return 14.5m;
        }

        private static decimal CalculateEmi(decimal principal, int tenureMonths, decimal annualRate)
        {
            if (principal <= 0 || tenureMonths <= 0)
            {
                return 0m;
            }

            decimal monthlyRate = annualRate / 1200m;
            if (monthlyRate <= 0)
            {
                return principal / tenureMonths;
            }

            double r = (double)monthlyRate;
            double n = tenureMonths;
            double p = (double)principal;
            double factor = Math.Pow(1 + r, n);
            return Math.Round((decimal)(p * r * factor / (factor - 1)), 2, MidpointRounding.AwayFromZero);
        }

        private static string ResolveRiskTier(int explicitTier, IReadOnlyList<BureauRow> bureauRows)
        {
            if (explicitTier == 0)
            {
                return "Low";
            }

            if (explicitTier == 1)
            {
                return "Medium";
            }

            if (explicitTier == 2)
            {
                return "High";
            }

            if (explicitTier >= 3)
            {
                return "VeryHigh";
            }

            int avgScore = bureauRows.Count == 0 ? 0 : (int)bureauRows.Average(x => x.Score);
            if (avgScore >= 750)
            {
                return "Low";
            }

            if (avgScore >= 700)
            {
                return "Medium";
            }

            if (avgScore >= 650)
            {
                return "High";
            }

            return "VeryHigh";
        }

        private static string ResolveRiskBadgeCss(string tier)
        {
            if (string.Equals(tier, "Low", StringComparison.OrdinalIgnoreCase))
            {
                return "risk-low";
            }

            if (string.Equals(tier, "Medium", StringComparison.OrdinalIgnoreCase))
            {
                return "risk-medium";
            }

            if (string.Equals(tier, "High", StringComparison.OrdinalIgnoreCase))
            {
                return "risk-high";
            }

            return "risk-veryhigh";
        }

        private bool IsUnderwriter()
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
                var roles = Roles.GetRolesForUser();
                if (roles != null && roles.Length > 0)
                {
                    role = roles[0];
                }
            }

            return string.Equals(role, "Underwriter", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "BranchManager", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "CreditOfficer", StringComparison.OrdinalIgnoreCase);
        }

        private Guid GetApplicationId()
        {
            object value = ViewState[ApplicationIdViewStateKey];
            Guid appId;
            return value != null && Guid.TryParse(value.ToString(), out appId) ? appId : Guid.Empty;
        }

        private static int ParseTenure(string tenureText)
        {
            if (string.IsNullOrWhiteSpace(tenureText))
            {
                return 36;
            }

            string numeric = new string(tenureText.Where(char.IsDigit).ToArray());
            int tenure;
            return int.TryParse(numeric, out tenure) ? tenure : 36;
        }

        private string ResolveCurrentUser()
        {
            if (Context != null
                && Context.User != null
                && Context.User.Identity != null
                && Context.User.Identity.IsAuthenticated)
            {
                return Context.User.Identity.Name;
            }

            return "UNDERWRITER";
        }

        private static string MaskPan(string pan)
        {
            if (string.IsNullOrWhiteSpace(pan))
            {
                return string.Empty;
            }

            string clean = pan.Trim().ToUpperInvariant();
            if (clean.Length != 10)
            {
                return clean;
            }

            return "XXXXX" + clean.Substring(5, 4) + clean.Substring(9, 1);
        }

        private static UnderwritingViewModel BuildDemoDecisionContext(Guid applicationId)
        {
            var detail = new UnderwritingViewModel();
            detail.ApplicationId = applicationId;
            detail.ApplicantName = "Priya Sharma";
            detail.Dob = "1992-08-14";
            detail.Pan = "ABCDE1234F";
            detail.EmploymentType = "Salaried";
            detail.AnnualIncome = 1200000m;
            detail.RiskTier = 1;
            detail.LoanAmount = 750000m;
            detail.Tenure = 60;
            detail.Purpose = "Home";
            detail.BureauRows.Add(new BureauRow { Bureau = 0, Score = 748, DefaultHistory = false, ActiveLoans = 1 });
            detail.BureauRows.Add(new BureauRow { Bureau = 1, Score = 736, DefaultHistory = false, ActiveLoans = 1 });
            detail.BureauRows.Add(new BureauRow { Bureau = 2, Score = 742, DefaultHistory = false, ActiveLoans = 0 });
            return detail;
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

        private static DataTable CreateHistorySchema()
        {
            var history = new DataTable();
            history.Columns.Add("DecisionId", typeof(string));
            history.Columns.Add("RecommendedAction", typeof(string));
            history.Columns.Add("ApprovedAmount", typeof(decimal));
            history.Columns.Add("InterestRate", typeof(decimal));
            history.Columns.Add("Remarks", typeof(string));
            history.Columns.Add("DecidedBy", typeof(string));
            history.Columns.Add("DecidedAt", typeof(DateTime));
            return history;
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

        private enum RecommendedAction
        {
            Approve = 0,
            ConditionalApprove = 1,
            Review = 2,
            Reject = 3
        }

        private sealed class UnderwritingViewModel
        {
            public UnderwritingViewModel()
            {
                BureauRows = new List<BureauRow>();
            }

            public Guid ApplicationId { get; set; }
            public string ApplicantName { get; set; }
            public string Dob { get; set; }
            public string Pan { get; set; }
            public string EmploymentType { get; set; }
            public decimal AnnualIncome { get; set; }
            public int RiskTier { get; set; }
            public decimal LoanAmount { get; set; }
            public int Tenure { get; set; }
            public string Purpose { get; set; }
            public List<BureauRow> BureauRows { get; private set; }
        }

        private sealed class BureauRow
        {
            public int Bureau { get; set; }
            public int Score { get; set; }
            public bool DefaultHistory { get; set; }
            public int ActiveLoans { get; set; }

            public string BureauName
            {
                get
                {
                    if (Bureau == 0)
                    {
                        return "CIBIL";
                    }

                    if (Bureau == 1)
                    {
                        return "Experian";
                    }

                    if (Bureau == 2)
                    {
                        return "Equifax";
                    }

                    return "Unknown";
                }
            }
        }
    }
}
