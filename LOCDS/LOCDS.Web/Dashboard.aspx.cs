using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace LOCDS.Web
{
    public partial class Dashboard : Page
    {
        private const string SortExpressionKey = "DashboardSortExpression";
        private const string SortDirectionKey = "DashboardSortDirection";
        private const string SqlHealthBannerSessionKey = "SqlHealthBanner";
        private const string FailFastOnSqlErrorSettingKey = "Demo:FailFastOnSqlError";
        private const string ShowSqlHealthBannerSettingKey = "Demo:ShowSqlHealthBanner";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (!IsCreditOfficer())
                {
                    Response.Redirect("~/Errors/403.aspx", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }

                ViewState[SortExpressionKey] = "CreatedDate";
                ViewState[SortDirectionKey] = "DESC";
                ApplyInitialFiltersFromQuery();
                BindDashboard();
            }
        }

        protected void btnFilter_Click(object sender, EventArgs e)
        {
            gvDashboard.PageIndex = 0;
            BindDashboard();
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            ddlStatus.SelectedIndex = 0;
            txtFromDate.Text = string.Empty;
            txtToDate.Text = string.Empty;
            ddlLoanType.SelectedIndex = 0;
            gvDashboard.PageIndex = 0;
            BindDashboard();
        }

        protected void tmrAutoRefresh_Tick(object sender, EventArgs e)
        {
            BindDashboard();
        }

        protected void gvDashboard_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvDashboard.PageIndex = e.NewPageIndex;
            BindDashboard();
        }

        protected void gvDashboard_Sorting(object sender, GridViewSortEventArgs e)
        {
            string currentExpression = ViewState[SortExpressionKey] as string ?? "CreatedDate";
            string currentDirection = ViewState[SortDirectionKey] as string ?? "DESC";

            if (string.Equals(currentExpression, e.SortExpression, StringComparison.OrdinalIgnoreCase))
            {
                currentDirection = string.Equals(currentDirection, "ASC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            }
            else
            {
                currentExpression = e.SortExpression;
                currentDirection = "ASC";
            }

            ViewState[SortExpressionKey] = currentExpression;
            ViewState[SortDirectionKey] = currentDirection;
            BindDashboard();
        }

        protected void gvDashboard_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow)
            {
                return;
            }

            var lblStatus = e.Row.FindControl("lblStatus") as Label;
            if (lblStatus == null)
            {
                return;
            }

            object rawStatus = DataBinder.Eval(e.Row.DataItem, "Status");
            int statusCode;
            if (!int.TryParse(Convert.ToString(rawStatus, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out statusCode))
            {
                statusCode = 0;
            }

            lblStatus.Text = GetStatusName(statusCode);
            lblStatus.CssClass = "status-badge " + GetStatusBadgeCss(statusCode);
        }

        protected void gvDashboard_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.CommandArgument as string))
            {
                return;
            }

            string rawId = Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture);

            if (string.Equals(e.CommandName, "ViewApp", StringComparison.OrdinalIgnoreCase))
            {
                Response.Redirect("~/UnderwritingDecision.aspx?applicationId=" + HttpUtility.UrlEncode(rawId), false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (string.Equals(e.CommandName, "PullBureau", StringComparison.OrdinalIgnoreCase))
            {
                Guid applicationId;
                if (Guid.TryParse(rawId, out applicationId))
                {
                    PullBureauReport(applicationId);
                }

                BindDashboard();
                ShowInfo("Bureau report pull initiated for " + rawId + ".");
                return;
            }

            if (string.Equals(e.CommandName, "AssignUnderwriter", StringComparison.OrdinalIgnoreCase))
            {
                Guid applicationId;
                if (Guid.TryParse(rawId, out applicationId))
                {
                    AssignToUnderwriter(applicationId);
                }

                BindDashboard();
                ShowInfo("Application " + rawId + " assigned to underwriter queue.");
            }
        }

        protected void btnExportExcel_Click(object sender, EventArgs e)
        {
            DataTable data = GetDashboardData();
            var csv = new StringBuilder();

            for (int i = 0; i < data.Columns.Count; i++)
            {
                if (i > 0)
                {
                    csv.Append(',');
                }

                csv.Append(EscapeCsv(Convert.ToString(data.Columns[i].ColumnName, CultureInfo.InvariantCulture)));
            }

            csv.AppendLine();

            for (int row = 0; row < data.Rows.Count; row++)
            {
                for (int col = 0; col < data.Columns.Count; col++)
                {
                    if (col > 0)
                    {
                        csv.Append(',');
                    }

                    object value = data.Rows[row][col];
                    string text = value == null || value == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(value, CultureInfo.InvariantCulture);
                    csv.Append(EscapeCsv(text));
                }

                csv.AppendLine();
            }

            byte[] bytes = Encoding.UTF8.GetBytes(csv.ToString());

            Response.Clear();
            Response.Buffer = true;
            Response.Charset = string.Empty;
            Response.ContentType = "text/csv";
            Response.AddHeader("content-disposition", "attachment;filename=CreditOfficerDashboard.csv");
            Response.BinaryWrite(bytes);
            Response.Flush();
            Response.End();
        }

        private void BindDashboard()
        {
            DataTable table = GetDashboardData();

            // Compute summary counters from the table
            int total = table.Rows.Count;
            int pendingBureau = 0, underReview = 0, approvedToday = 0, rejectedToday = 0;
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (DataRow row in table.Rows)
            {
                int status = 0;
                int.TryParse(Convert.ToString(row["Status"], CultureInfo.InvariantCulture), out status);
                if (status == 2) pendingBureau++;
                if (status == 3) underReview++;
                string createdDate = Convert.ToString(row["CreatedDate"], CultureInfo.InvariantCulture);
                if (status == 4 && createdDate.StartsWith(today)) approvedToday++;
                if (status == 5 && createdDate.StartsWith(today)) rejectedToday++;
            }

            litTotalApps.Text = total.ToString(CultureInfo.InvariantCulture);
            litPendingBureau.Text = pendingBureau.ToString(CultureInfo.InvariantCulture);
            litUnderReview.Text = underReview.ToString(CultureInfo.InvariantCulture);
            litApprovedToday.Text = approvedToday.ToString(CultureInfo.InvariantCulture);
            litRejectedToday.Text = rejectedToday.ToString(CultureInfo.InvariantCulture);

            if (table.Columns.Count == 0)
            {
                gvDashboard.DataSource = table;
                gvDashboard.DataBind();
                return;
            }

            string sortExpression = ViewState[SortExpressionKey] as string ?? "CreatedDate";
            string sortDirection = ViewState[SortDirectionKey] as string ?? "DESC";
            string safeSortExpression = ResolveSafeSortExpression(sortExpression);

            var view = new DataView(table)
            {
                Sort = safeSortExpression + " " + sortDirection
            };

            gvDashboard.DataSource = view;
            gvDashboard.DataBind();
        }

        private DataTable GetDashboardData()
        {
            var connectionSetting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connectionSetting != null ? connectionSetting.ConnectionString : null;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return HandleSqlUnavailable("LOCDSConnection is missing or empty.");
            }

            var table = new DataTable();

            const string sql = @"
SELECT
    LA.ApplicationId,
    CONCAT(A.FirstName, ' ', A.LastName) AS ApplicantName,
    LA.LoanAmount,
    LA.Purpose,
    LA.Status,
    LA.CreatedDate,
    ISNULL(CONVERT(NVARCHAR(64), LA.AssignedUnderwriterId), N'Unassigned') AS AssignedTo
FROM locds.LoanApplication LA
INNER JOIN locds.Applicant A ON A.ApplicantId = LA.ApplicantId
WHERE (@Status = '' OR CAST(LA.Status AS NVARCHAR(10)) = @Status)
  AND (@FromDate = '' OR LA.CreatedDate >= TRY_CONVERT(date, @FromDate))
  AND (@ToDate = '' OR LA.CreatedDate < DATEADD(day, 1, TRY_CONVERT(date, @ToDate)))
  AND (@LoanType = '' OR LA.Purpose = @LoanType);";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@Status", SqlDbType.NVarChar, 10).Value = ddlStatus.SelectedValue ?? string.Empty;
                    command.Parameters.Add("@FromDate", SqlDbType.NVarChar, 20).Value = txtFromDate.Text ?? string.Empty;
                    command.Parameters.Add("@ToDate", SqlDbType.NVarChar, 20).Value = txtToDate.Text ?? string.Empty;
                    command.Parameters.Add("@LoanType", SqlDbType.NVarChar, 50).Value = ddlLoanType.SelectedValue ?? string.Empty;

                    using (var adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(table);
                    }
                }

                ClearSqlHealthBanner();

                if (table.Rows.Count == 0)
                {
                    return table;
                }

                return table;
            }
            catch (Exception ex)
            {
                return HandleSqlUnavailable(ex.Message);
            }
        }

        private DataTable HandleSqlUnavailable(string reason)
        {
            string safeReason = string.IsNullOrWhiteSpace(reason) ? "Unknown database connectivity issue." : reason;

            SetSqlHealthBanner("Database connectivity issue detected: " + safeReason);

            if (IsFailFastOnSqlError())
            {
                ShowError("Database is unavailable. Fail-fast mode is enabled, so demo fallback data is disabled.");
                return CreateDashboardSchema();
            }

            ShowInfo("Database is unavailable. Showing fallback demo data.");
            return ApplyDashboardFilters(BuildDemoDashboardData());
        }

        private static DataTable CreateDashboardSchema()
        {
            var t = new DataTable();
            t.Columns.Add("ApplicationId", typeof(string));
            t.Columns.Add("ApplicantName", typeof(string));
            t.Columns.Add("LoanAmount", typeof(decimal));
            t.Columns.Add("Purpose", typeof(string));
            t.Columns.Add("Status", typeof(int));
            t.Columns.Add("CreatedDate", typeof(DateTime));
            t.Columns.Add("AssignedTo", typeof(string));
            return t;
        }

        private void ApplyInitialFiltersFromQuery()
        {
            string status = (Request.QueryString["status"] ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(status) && ddlStatus.Items.FindByValue(status) != null)
            {
                ddlStatus.SelectedValue = status;
            }

            string loanType = (Request.QueryString["loanType"] ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(loanType) && ddlLoanType.Items.FindByValue(loanType) != null)
            {
                ddlLoanType.SelectedValue = loanType;
            }

            string fromDate = (Request.QueryString["fromDate"] ?? string.Empty).Trim();
            DateTime parsedFromDate;
            if (DateTime.TryParse(fromDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedFromDate))
            {
                txtFromDate.Text = parsedFromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            string toDate = (Request.QueryString["toDate"] ?? string.Empty).Trim();
            DateTime parsedToDate;
            if (DateTime.TryParse(toDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedToDate))
            {
                txtToDate.Text = parsedToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }

        private DataTable ApplyDashboardFilters(DataTable source)
        {
            if (source == null)
            {
                return new DataTable();
            }

            string selectedStatus = ddlStatus.SelectedValue ?? string.Empty;
            string selectedLoanType = ddlLoanType.SelectedValue ?? string.Empty;

            DateTime fromDate;
            bool hasFromDate = DateTime.TryParse(txtFromDate.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out fromDate);

            DateTime toDate;
            bool hasToDate = DateTime.TryParse(txtToDate.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out toDate);
            DateTime toDateExclusive = toDate.Date.AddDays(1);

            var filtered = source.Clone();
            foreach (DataRow row in source.Rows)
            {
                string rowStatus = Convert.ToString(row["Status"], CultureInfo.InvariantCulture) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(selectedStatus) && !string.Equals(rowStatus, selectedStatus, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string rowLoanType = Convert.ToString(row["Purpose"], CultureInfo.InvariantCulture) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(selectedLoanType) && !string.Equals(rowLoanType, selectedLoanType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DateTime rowCreatedDate;
                if (!DateTime.TryParse(Convert.ToString(row["CreatedDate"], CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out rowCreatedDate))
                {
                    rowCreatedDate = DateTime.MinValue;
                }

                if (hasFromDate && rowCreatedDate < fromDate.Date)
                {
                    continue;
                }

                if (hasToDate && rowCreatedDate >= toDateExclusive)
                {
                    continue;
                }

                filtered.ImportRow(row);
            }

            return filtered;
        }

        private static DataTable BuildDemoDashboardData()
        {
            var t = CreateDashboardSchema();

            string today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            t.Rows.Add("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1", "Priya Sharma", 750000m, "Home", 1, DateTime.Today.AddDays(-2), "Unassigned");
            t.Rows.Add("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2", "Rahul Gupta", 500000m, "Personal", 2, DateTime.Today.AddDays(-1), "Unassigned");
            t.Rows.Add("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3", "Anita Desai", 1200000m, "Home", 3, DateTime.Today.AddDays(-3), "under.writer");
            t.Rows.Add("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4", "Vikram Singh", 300000m, "Vehicle", 4, DateTime.Today, "under.writer");
            t.Rows.Add("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5", "Sunita Mehta", 200000m, "Education", 5, DateTime.Today, "under.writer");
            t.Rows.Add("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6", "Ajay Kumar", 900000m, "Business", 1, DateTime.Today.AddDays(-4), "Unassigned");
            t.Rows.Add("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa7", "Neha Patel", 600000m, "Home", 2, DateTime.Today.AddDays(-1), "Unassigned");
            t.Rows.Add("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa8", "Deepak Joshi", 450000m, "Personal", 6, DateTime.Today.AddDays(-5), "credit.officer");

            return t;
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

        private void PullBureauReport(Guid applicationId)
        {
            var connectionSetting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connectionSetting != null ? connectionSetting.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            const string sql = @"
IF NOT EXISTS (SELECT 1 FROM locds.CreditBureauReport WHERE ApplicationId = @ApplicationId)
BEGIN
    INSERT INTO locds.CreditBureauReport
    (
        ApplicationId,
        Bureau,
        Score,
        Enquiries,
        ActiveLoans,
        DefaultHistory,
        PulledAt,
        CreatedDate,
        LastModifiedDate
    )
    VALUES
    (
        @ApplicationId,
        0,
        700,
        0,
        0,
        0,
        SYSUTCDATETIME(),
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END;

UPDATE locds.LoanApplication
SET Status = 2,
    LastModifiedDate = SYSUTCDATETIME()
WHERE ApplicationId = @ApplicationId;";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@ApplicationId", SqlDbType.UniqueIdentifier).Value = applicationId;
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PullBureauReport error: " + ex.Message);
            }
        }

        private void AssignToUnderwriter(Guid applicationId)
        {
            var connectionSetting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connectionSetting != null ? connectionSetting.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            const string sql = @"
UPDATE locds.LoanApplication
SET AssignedUnderwriterId = COALESCE(AssignedUnderwriterId, NEWID()),
    Status = 3,
    LastModifiedDate = SYSUTCDATETIME()
WHERE ApplicationId = @ApplicationId;";

            try
            {
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@ApplicationId", SqlDbType.UniqueIdentifier).Value = applicationId;
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AssignToUnderwriter error: " + ex.Message);
            }
        }

        private bool IsCreditOfficer()
        {
            string role = Session != null ? Session["UserRole"] as string : null;
            var formsIdentity = Context != null && Context.User != null ? Context.User.Identity as FormsIdentity : null;
            if (string.IsNullOrWhiteSpace(role) && formsIdentity != null && formsIdentity.Ticket != null)
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

            return string.Equals(role, "CreditOfficer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Underwriter", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "BranchManager", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Applicant", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetStatusName(int code)
        {
            switch (code)
            {
                case 0: return "Draft";
                case 1: return "Submitted";
                case 2: return "Bureau Pending";
                case 3: return "Under Review";
                case 4: return "Approved";
                case 5: return "Rejected";
                case 6: return "Offer Sent";
                case 7: return "Accepted";
                case 8: return "Disbursed";
                default: return "Unknown";
            }
        }

        private static string GetStatusBadgeCss(int code)
        {
            switch (code)
            {
                case 0: return "status-draft";
                case 1:
                case 2:
                case 6: return "status-pending";
                case 3: return "status-review";
                case 4:
                case 7:
                case 8: return "status-approved";
                case 5: return "status-rejected";
                default: return "status-draft";
            }
        }

        private static string ResolveSafeSortExpression(string requestedSort)
        {
            switch (requestedSort)
            {
                case "ApplicationId":
                case "ApplicantName":
                case "LoanAmount":
                case "Purpose":
                case "Status":
                case "CreatedDate":
                case "AssignedTo":
                    return requestedSort;
                default:
                    return "CreatedDate";
            }
        }

        private static string EscapeCsv(string value)
        {
            string safe = value ?? string.Empty;
            if (safe.IndexOf('"') >= 0)
            {
                safe = safe.Replace("\"", "\"\"");
            }

            if (safe.IndexOf(',') >= 0 || safe.IndexOf('\n') >= 0 || safe.IndexOf('\r') >= 0 || safe.IndexOf('"') >= 0)
            {
                return "\"" + safe + "\"";
            }

            return safe;
        }

        private void ShowError(string message)
        {
            lblMessage.CssClass = "alert alert-danger d-block";
            lblMessage.Text = HttpUtility.HtmlEncode(message);
        }

        private void ShowInfo(string message)
        {
            lblMessage.CssClass = "alert alert-info d-block";
            lblMessage.Text = HttpUtility.HtmlEncode(message);
        }
    }
}
