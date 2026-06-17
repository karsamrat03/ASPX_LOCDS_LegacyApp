using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace LOCDS.Web
{
    public partial class LoanApplicationPage : Page
    {
        private const string DraftViewStateKey = "LoanApplicationWizardDraft";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                txtAmount.Text = "500000";
                rngAmount.Value = "500000";
                SetProgress();
                SaveDraftToViewState();
            }
        }

        protected void wizLoanApplication_ActiveStepChanged(object sender, EventArgs e)
        {
            SetProgress();
            if (wizLoanApplication.ActiveStepIndex == 3)
            {
                BindReviewSummary();
            }
        }

        protected void wizLoanApplication_PreviousButtonClick(object sender, WizardNavigationEventArgs e)
        {
            SaveDraftToViewState();
            SaveDraftToDatabase(isSubmitted: false, referenceNumber: null);
        }

        protected void wizLoanApplication_NextButtonClick(object sender, WizardNavigationEventArgs e)
        {
            string validationGroup = GetValidationGroupForStep(wizLoanApplication.ActiveStepIndex);
            Page.Validate(validationGroup);
            if (!Page.IsValid)
            {
                e.Cancel = true;
                return;
            }

            SaveDraftToViewState();
            SaveDraftToDatabase(isSubmitted: false, referenceNumber: null);
        }

        protected void wizLoanApplication_FinishButtonClick(object sender, WizardNavigationEventArgs e)
        {
            Page.Validate("Step4");
            if (!Page.IsValid)
            {
                e.Cancel = true;
                return;
            }

            SaveDraftToViewState();

            string referenceNumber = GenerateReferenceNumber();
            lblReferenceNumber.Text = HttpUtility.HtmlEncode(referenceNumber);
            SaveDraftToDatabase(isSubmitted: true, referenceNumber: referenceNumber);

            string submitError;
            Guid applicationId = PersistSubmittedApplication(referenceNumber, out submitError);
            if (applicationId != Guid.Empty)
            {
                Session["LastSubmittedApplicationId"] = applicationId.ToString("D", CultureInfo.InvariantCulture);
                lblGlobalMessage.CssClass = "alert alert-success d-block";
                lblGlobalMessage.Text = "Application submitted successfully. Queue ID: " + HttpUtility.HtmlEncode(applicationId.ToString("D", CultureInfo.InvariantCulture));
                return;
            }

            lblGlobalMessage.CssClass = "alert alert-danger d-block";
            lblGlobalMessage.Text = "Application draft saved, but queue submission failed. " + HttpUtility.HtmlEncode(submitError ?? "Unknown error.");
        }

        protected void cvDobAge_ServerValidate(object source, ServerValidateEventArgs args)
        {
            DateTime dob;
            if (!DateTime.TryParse(args.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dob))
            {
                args.IsValid = false;
                return;
            }

            DateTime today = DateTime.Today;
            int age = today.Year - dob.Year;
            if (dob > today.AddYears(-age))
            {
                age--;
            }

            args.IsValid = age >= 21 && age <= 65;
        }

        protected void cvItrUpload_ServerValidate(object source, ServerValidateEventArgs args)
        {
            if (!fuItr.HasFile)
            {
                args.IsValid = true;
                return;
            }

            string extension = System.IO.Path.GetExtension(fuItr.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                args.IsValid = false;
                return;
            }

            string lower = extension.ToLowerInvariant();
            args.IsValid = lower == ".pdf" || lower == ".jpg" || lower == ".jpeg" || lower == ".png";
        }

        protected void cvDeclaration_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = chkDeclaration.Checked;
        }

        [WebMethod]
        public static string GetEmiPreview(decimal amount, int tenureMonths)
        {
            if (amount <= 0 || tenureMonths <= 0)
            {
                return "Invalid input";
            }

            const decimal annualRate = 10.5m;
            decimal monthlyRate = annualRate / 12m / 100m;

            decimal emi;
            if (monthlyRate == 0)
            {
                emi = amount / tenureMonths;
            }
            else
            {
                double r = (double)monthlyRate;
                double n = tenureMonths;
                double p = (double)amount;
                double factor = Math.Pow(1 + r, n);
                emi = (decimal)(p * r * factor / (factor - 1));
            }

            return string.Format(CultureInfo.InvariantCulture, "Rs {0:N2} / month", emi);
        }

        private void SetProgress()
        {
            int index = wizLoanApplication.ActiveStepIndex;
            int percent = 25;

            if (index == 1)
            {
                percent = 50;
            }
            else if (index == 2)
            {
                percent = 75;
            }
            else if (index >= 3)
            {
                percent = 100;
            }

            divProgressBar.Style["width"] = percent + "%";
            divProgressBar.InnerText = percent + "%";
            lblProgressText.Text = string.Format(CultureInfo.InvariantCulture, "Completion: {0}%", percent);
        }

        private void BindReviewSummary()
        {
            SaveDraftToViewState();
            LoanApplicationDraft draft = GetDraft();

            litSummaryName.Text = HttpUtility.HtmlEncode(draft.FullName);
            litSummaryDob.Text = HttpUtility.HtmlEncode(draft.Dob);
            litSummaryPan.Text = HttpUtility.HtmlEncode(MaskPan(draft.Pan));
            litSummaryAddress.Text = HttpUtility.HtmlEncode(draft.Address);
            litSummaryEmployment.Text = HttpUtility.HtmlEncode(draft.EmploymentType + " - " + draft.Employer);
            litSummaryIncome.Text = HttpUtility.HtmlEncode(draft.AnnualIncome);
            litSummaryExperience.Text = HttpUtility.HtmlEncode(draft.WorkExperience + " years");
            litSummaryPurpose.Text = HttpUtility.HtmlEncode(draft.Purpose);
            litSummaryAmount.Text = HttpUtility.HtmlEncode(draft.Amount);
            litSummaryTenure.Text = HttpUtility.HtmlEncode(draft.Tenure + " months");
            litSummaryCoApplicant.Text = draft.HasCoApplicant ? "Yes" : "No";
        }

        private string GetValidationGroupForStep(int stepIndex)
        {
            if (stepIndex == 0)
            {
                return "Step1";
            }

            if (stepIndex == 1)
            {
                return "Step2";
            }

            if (stepIndex == 2)
            {
                return "Step3";
            }

            return string.Empty;
        }

        private void SaveDraftToViewState()
        {
            var draft = new LoanApplicationDraft
            {
                FullName = txtFullName.Text == null ? string.Empty : txtFullName.Text.Trim(),
                Dob = txtDob.Text == null ? string.Empty : txtDob.Text.Trim(),
                Pan = (txtPan.Text ?? string.Empty).Trim().ToUpperInvariant(),
                AadhaarMasked = MaskAadhaar(txtAadhaar.Text),
                Address = txtAddress.Text == null ? string.Empty : txtAddress.Text.Trim(),
                EmploymentType = ddlEmploymentType.SelectedValue,
                Employer = txtEmployer.Text == null ? string.Empty : txtEmployer.Text.Trim(),
                AnnualIncome = txtAnnualIncome.Text == null ? string.Empty : txtAnnualIncome.Text.Trim(),
                WorkExperience = txtWorkExperience.Text == null ? string.Empty : txtWorkExperience.Text.Trim(),
                ItrFileName = fuItr.HasFile ? fuItr.FileName : (GetDraft().ItrFileName ?? string.Empty),
                Purpose = ddlPurpose.SelectedValue,
                Amount = txtAmount.Text == null ? string.Empty : txtAmount.Text.Trim(),
                Tenure = rblTenure.SelectedValue,
                HasCoApplicant = chkCoApplicant.Checked,
                ActiveStep = wizLoanApplication.ActiveStepIndex
            };

            ViewState[DraftViewStateKey] = draft;
        }

        private LoanApplicationDraft GetDraft()
        {
            var draft = ViewState[DraftViewStateKey] as LoanApplicationDraft;
            return draft ?? new LoanApplicationDraft();
        }

        private void SaveDraftToDatabase(bool isSubmitted, string referenceNumber)
        {
            var connectionSetting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connectionSetting != null ? connectionSetting.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            LoanApplicationDraft draft = GetDraft();
            string username = null;
            if (Context != null && Context.User != null && Context.User.Identity != null)
            {
                username = Context.User.Identity.Name;
            }
            if (string.IsNullOrWhiteSpace(username))
            {
                username = "anonymous";
            }

            string draftKey = username + "::" + Session.SessionID;
            string payload = new JavaScriptSerializer().Serialize(draft);

            const string sql = @"
IF OBJECT_ID('locds.LoanApplicationDraft', 'U') IS NULL
BEGIN
    CREATE TABLE locds.LoanApplicationDraft
    (
        DraftId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_LoanApplicationDraft PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
        DraftKey NVARCHAR(150) NOT NULL UNIQUE,
        UserName NVARCHAR(100) NOT NULL,
        DraftJson NVARCHAR(MAX) NOT NULL,
        CurrentStep INT NOT NULL,
        LastSavedAt DATETIME2(0) NOT NULL,
        IsSubmitted BIT NOT NULL CONSTRAINT DF_LoanApplicationDraft_IsSubmitted DEFAULT (0),
        ReferenceNumber NVARCHAR(50) NULL
    );
END;

MERGE locds.LoanApplicationDraft AS target
USING (SELECT @DraftKey AS DraftKey) AS src
ON target.DraftKey = src.DraftKey
WHEN MATCHED THEN
    UPDATE SET
        UserName = @UserName,
        DraftJson = @DraftJson,
        CurrentStep = @CurrentStep,
        LastSavedAt = SYSUTCDATETIME(),
        IsSubmitted = @IsSubmitted,
        ReferenceNumber = @ReferenceNumber
WHEN NOT MATCHED THEN
    INSERT (DraftKey, UserName, DraftJson, CurrentStep, LastSavedAt, IsSubmitted, ReferenceNumber)
    VALUES (@DraftKey, @UserName, @DraftJson, @CurrentStep, SYSUTCDATETIME(), @IsSubmitted, @ReferenceNumber);";

            try
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, sqlConnection))
                {
                    command.Parameters.Add("@DraftKey", SqlDbType.NVarChar, 150).Value = draftKey;
                    command.Parameters.Add("@UserName", SqlDbType.NVarChar, 100).Value = username;
                    command.Parameters.Add("@DraftJson", SqlDbType.NVarChar, -1).Value = payload;
                    command.Parameters.Add("@CurrentStep", SqlDbType.Int).Value = draft.ActiveStep;
                    command.Parameters.Add("@IsSubmitted", SqlDbType.Bit).Value = isSubmitted;
                    command.Parameters.Add("@ReferenceNumber", SqlDbType.NVarChar, 50).Value = (object)referenceNumber ?? DBNull.Value;

                    sqlConnection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch
            {
                // Keep user flow non-blocking when draft persistence infrastructure is unavailable.
            }
        }

        private Guid PersistSubmittedApplication(string referenceNumber, out string error)
        {
            error = string.Empty;
            var connectionSetting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            string connectionString = connectionSetting != null ? connectionSetting.ConnectionString : null;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                error = "LOCDSConnection is missing.";
                return Guid.Empty;
            }

            LoanApplicationDraft draft = GetDraft();
            string[] nameParts = (draft.FullName ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string firstName = nameParts.Length > 0 ? nameParts[0] : "Applicant";
            string lastName = nameParts.Length > 1 ? string.Join(" ", nameParts, 1, nameParts.Length - 1) : "User";

            DateTime dob;
            if (!DateTime.TryParse(draft.Dob, CultureInfo.InvariantCulture, DateTimeStyles.None, out dob))
            {
                dob = new DateTime(1990, 1, 1);
            }

            decimal annualIncome;
            if (!decimal.TryParse(draft.AnnualIncome, NumberStyles.Number, CultureInfo.InvariantCulture, out annualIncome) || annualIncome <= 0)
            {
                annualIncome = 600000m;
            }

            decimal loanAmount;
            if (!decimal.TryParse(draft.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out loanAmount) || loanAmount <= 0)
            {
                loanAmount = 500000m;
            }

            int tenure;
            if (!int.TryParse(draft.Tenure, NumberStyles.Integer, CultureInfo.InvariantCulture, out tenure) || tenure <= 0)
            {
                tenure = 36;
            }

            string purpose = string.IsNullOrWhiteSpace(draft.Purpose) ? "Personal" : draft.Purpose.Trim();
            string pan = (draft.Pan ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(pan))
            {
                error = "PAN is missing in submitted draft payload.";
                return Guid.Empty;
            }

            string aadhaarHash = ComputeSha256Hex(draft.AadhaarMasked ?? string.Empty);
            byte employmentType = MapEmploymentType(draft.EmploymentType);
            int creditScore = DeriveCreditScore(annualIncome);
            byte riskTier = DeriveRiskTier(annualIncome);

            const string sql = @"
DECLARE @ApplicantId UNIQUEIDENTIFIER;

SELECT TOP (1) @ApplicantId = ApplicantId
FROM locds.Applicant
WHERE PAN = @PAN;

IF @ApplicantId IS NULL
BEGIN
    SET @ApplicantId = NEWID();

    INSERT INTO locds.Applicant
    (
        ApplicantId,
        FirstName,
        LastName,
        DOB,
        PAN,
        AadhaarHash,
        AnnualIncome,
        EmploymentType,
        CreditScore,
        RiskTier,
        CreatedDate,
        LastModifiedDate
    )
    VALUES
    (
        @ApplicantId,
        @FirstName,
        @LastName,
        @DOB,
        @PAN,
        @AadhaarHash,
        @AnnualIncome,
        @EmploymentType,
        @CreditScore,
        @RiskTier,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END
ELSE
BEGIN
    UPDATE locds.Applicant
    SET FirstName = @FirstName,
        LastName = @LastName,
        DOB = @DOB,
        AadhaarHash = @AadhaarHash,
        AnnualIncome = @AnnualIncome,
        EmploymentType = @EmploymentType,
        CreditScore = @CreditScore,
        RiskTier = @RiskTier,
        LastModifiedDate = SYSUTCDATETIME()
    WHERE ApplicantId = @ApplicantId;
END;

DECLARE @ApplicationId UNIQUEIDENTIFIER = NEWID();

INSERT INTO locds.LoanApplication
(
    ApplicationId,
    ApplicantId,
    LoanAmount,
    Tenure,
    Purpose,
    Status,
    CreatedDate,
    LastModifiedDate,
    AssignedUnderwriterId
)
VALUES
(
    @ApplicationId,
    @ApplicantId,
    @LoanAmount,
    @Tenure,
    @Purpose,
    1,
    SYSUTCDATETIME(),
    SYSUTCDATETIME(),
    NULL
);

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
        N'LoanApplication',
        CONVERT(NVARCHAR(64), @ApplicationId),
        N'SUBMIT',
        NULL,
        @ReferenceNumber,
        @UserName,
        SYSUTCDATETIME(),
        @IPAddress,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END;

SELECT @ApplicationId;";

            string username = Context != null && Context.User != null && Context.User.Identity != null
                ? Context.User.Identity.Name
                : "anonymous";

            try
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(sql, sqlConnection))
                {
                    command.Parameters.Add("@FirstName", SqlDbType.NVarChar, 100).Value = firstName;
                    command.Parameters.Add("@LastName", SqlDbType.NVarChar, 100).Value = lastName;
                    command.Parameters.Add("@DOB", SqlDbType.Date).Value = dob.Date;
                    command.Parameters.Add("@PAN", SqlDbType.NVarChar, 20).Value = pan;
                    command.Parameters.Add("@AadhaarHash", SqlDbType.NVarChar, 256).Value = aadhaarHash;
                    command.Parameters.Add("@AnnualIncome", SqlDbType.Decimal).Value = annualIncome;
                    command.Parameters["@AnnualIncome"].Precision = 18;
                    command.Parameters["@AnnualIncome"].Scale = 2;
                    command.Parameters.Add("@EmploymentType", SqlDbType.TinyInt).Value = employmentType;
                    command.Parameters.Add("@CreditScore", SqlDbType.Int).Value = creditScore;
                    command.Parameters.Add("@RiskTier", SqlDbType.TinyInt).Value = riskTier;
                    command.Parameters.Add("@LoanAmount", SqlDbType.Decimal).Value = loanAmount;
                    command.Parameters["@LoanAmount"].Precision = 18;
                    command.Parameters["@LoanAmount"].Scale = 2;
                    command.Parameters.Add("@Tenure", SqlDbType.Int).Value = tenure;
                    command.Parameters.Add("@Purpose", SqlDbType.NVarChar, 50).Value = purpose;
                    command.Parameters.Add("@ReferenceNumber", SqlDbType.NVarChar, 50).Value = (object)referenceNumber ?? DBNull.Value;
                    command.Parameters.Add("@UserName", SqlDbType.NVarChar, 100).Value = username;
                    command.Parameters.Add("@IPAddress", SqlDbType.NVarChar, 45).Value = Request != null ? (object)(Request.UserHostAddress ?? string.Empty) : string.Empty;

                    sqlConnection.Open();
                    object scalar = command.ExecuteScalar();
                    Guid applicationId;
                    return scalar != null && Guid.TryParse(Convert.ToString(scalar, CultureInfo.InvariantCulture), out applicationId)
                        ? applicationId
                        : Guid.Empty;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return Guid.Empty;
            }
        }

        private static byte MapEmploymentType(string employmentType)
        {
            if (string.Equals(employmentType, "Salaried", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(employmentType, "SelfEmployed", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(employmentType, "BusinessOwner", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(employmentType, "Contractor", StringComparison.OrdinalIgnoreCase)) return 3;
            return 0;
        }

        private static int DeriveCreditScore(decimal annualIncome)
        {
            if (annualIncome >= 2000000m) return 760;
            if (annualIncome >= 1200000m) return 720;
            if (annualIncome >= 800000m) return 690;
            return 660;
        }

        private static byte DeriveRiskTier(decimal annualIncome)
        {
            if (annualIncome >= 1500000m) return 0;
            if (annualIncome >= 900000m) return 1;
            if (annualIncome >= 600000m) return 2;
            return 3;
        }

        private static string ComputeSha256Hex(string raw)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(raw ?? string.Empty);
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }

        private static string GenerateReferenceNumber()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "LAP-{0}-{1}",
                DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                new Random().Next(1000, 9999));
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

        private static string MaskAadhaar(string aadhaar)
        {
            if (string.IsNullOrWhiteSpace(aadhaar))
            {
                return string.Empty;
            }

            string clean = aadhaar.Trim();
            if (clean.Length <= 4)
            {
                return clean;
            }

            return new string('X', clean.Length - 4) + clean.Substring(clean.Length - 4);
        }

        [Serializable]
        private sealed class LoanApplicationDraft
        {
            public string FullName { get; set; }
            public string Dob { get; set; }
            public string Pan { get; set; }
            public string AadhaarMasked { get; set; }
            public string Address { get; set; }
            public string EmploymentType { get; set; }
            public string Employer { get; set; }
            public string AnnualIncome { get; set; }
            public string WorkExperience { get; set; }
            public string ItrFileName { get; set; }
            public string Purpose { get; set; }
            public string Amount { get; set; }
            public string Tenure { get; set; }
            public bool HasCoApplicant { get; set; }
            public int ActiveStep { get; set; }
        }
    }
}
