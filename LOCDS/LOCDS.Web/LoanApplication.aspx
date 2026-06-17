<%@ Page Title="Loan Application" Language="C#" MasterPageFile="~/Site.Master"
AutoEventWireup="true" CodeFile="LoanApplication.aspx.cs"
Inherits="LOCDS.Web.LoanApplicationPage" %>

<asp:Content ID="HeadLoanApp" ContentPlaceHolderID="HeadContent" runat="server">
  <style type="text/css">
    .wizard-shell {
      max-width: 1020px;
    }
    .wizard-card {
      border: 1px solid #d6deeb;
      border-radius: 10px;
      box-shadow: 0 8px 22px rgba(10, 26, 53, 0.08);
    }
    .wizard-title {
      color: #0b1f3a;
      font-weight: 700;
    }
    .progress {
      height: 14px;
      border-radius: 10px;
    }
    .progress-bar {
      background-color: #c9a227;
      color: #15243d;
      font-weight: 600;
    }
    .step-box {
      background: #ffffff;
      border: 1px solid #e1e7f1;
      border-radius: 8px;
      padding: 1rem;
    }
    .summary-row {
      border-bottom: 1px dashed #d9e2ef;
      padding: 0.45rem 0;
    }
    .summary-row:last-child {
      border-bottom: none;
    }
    .required-mark {
      color: #b02a37;
    }
  </style>

  <script type="text/javascript">
    function validateDobAge(sender, args) {
      if (!args.Value) {
        args.IsValid = false;
        return;
      }

      var dob = new Date(args.Value);
      if (isNaN(dob.getTime())) {
        args.IsValid = false;
        return;
      }

      var today = new Date();
      var age = today.getFullYear() - dob.getFullYear();
      var monthDiff = today.getMonth() - dob.getMonth();
      if (
        monthDiff < 0 ||
        (monthDiff === 0 && today.getDate() < dob.getDate())
      ) {
        age--;
      }

      args.IsValid = age >= 21 && age <= 65;
    }

    function validateDeclarationClient(sender, args) {
      var checkbox = document.getElementById("<%= chkDeclaration.ClientID %>");
      args.IsValid = !!(checkbox && checkbox.checked);
    }

    function validateItrFileClient(sender, args) {
      var upload = document.getElementById("<%= fuItr.ClientID %>");
      if (!upload || !upload.value) {
        args.IsValid = true;
        return;
      }

      var lower = upload.value.toLowerCase();
      args.IsValid =
        lower.endsWith(".pdf") ||
        lower.endsWith(".jpg") ||
        lower.endsWith(".jpeg") ||
        lower.endsWith(".png");
    }

    function syncAmountAndPreview() {
      var slider = document.getElementById("<%= rngAmount.ClientID %>");
      var amountText = document.getElementById("<%= txtAmount.ClientID %>");
      if (!slider || !amountText) {
        return;
      }

      amountText.value = slider.value;
      requestEmiPreview();
    }

    function syncAmountFromText() {
      var slider = document.getElementById("<%= rngAmount.ClientID %>");
      var amountText = document.getElementById("<%= txtAmount.ClientID %>");
      if (!slider || !amountText) {
        return;
      }

      var parsed = parseInt(amountText.value || "0", 10);
      if (!isNaN(parsed)) {
        if (parsed < parseInt(slider.min, 10)) {
          parsed = parseInt(slider.min, 10);
        }
        if (parsed > parseInt(slider.max, 10)) {
          parsed = parseInt(slider.max, 10);
        }
        slider.value = parsed;
        amountText.value = parsed;
      }

      requestEmiPreview();
    }

    function requestEmiPreview() {
      var amountText = document.getElementById("<%= txtAmount.ClientID %>");
      var tenureList = document.getElementById("<%= rblTenure.ClientID %>");
      var emiLabel = document.getElementById("<%= lblEmiPreview.ClientID %>");
      if (!amountText || !tenureList || !emiLabel) {
        return;
      }

      var selectedTenureInput = tenureList.querySelector(
        "input[type=radio]:checked",
      );
      if (!selectedTenureInput) {
        emiLabel.innerText = "EMI Preview: Select tenure";
        return;
      }

      var amount = parseFloat(amountText.value || "0");
      var tenure = parseInt(selectedTenureInput.value || "0", 10);
      if (isNaN(amount) || amount <= 0 || isNaN(tenure) || tenure <= 0) {
        emiLabel.innerText = "EMI Preview: Enter valid amount and tenure";
        return;
      }

      var payload = JSON.stringify({ amount: amount, tenureMonths: tenure });
      $.ajax({
        type: "POST",
        url: '<%= ResolveUrl("~/LoanApplication.aspx/GetEmiPreview") %>',
        data: payload,
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
          var value = response && response.d ? response.d : "";
          emiLabel.innerText = "EMI Preview: " + value;
        },
        error: function () {
          emiLabel.innerText = "EMI Preview: unavailable";
        },
      });
    }

    function wireLoanStepEvents() {
      var slider = document.getElementById("<%= rngAmount.ClientID %>");
      var amountText = document.getElementById("<%= txtAmount.ClientID %>");
      var tenureList = document.getElementById("<%= rblTenure.ClientID %>");

      if (slider) {
        slider.addEventListener("input", syncAmountAndPreview);
      }

      if (amountText) {
        amountText.addEventListener("change", syncAmountFromText);
        amountText.addEventListener("keyup", function () {
          if (this.value.length > 2) {
            syncAmountFromText();
          }
        });
      }

      if (tenureList) {
        tenureList.addEventListener("change", requestEmiPreview);
      }

      requestEmiPreview();
    }

    document.addEventListener("DOMContentLoaded", wireLoanStepEvents);
    if (
      typeof Sys !== "undefined" &&
      Sys.WebForms &&
      Sys.WebForms.PageRequestManager
    ) {
      Sys.WebForms.PageRequestManager.getInstance().add_endRequest(
        wireLoanStepEvents,
      );
    }
  </script>
</asp:Content>

<asp:Content ID="MainLoanApp" ContentPlaceHolderID="MainContent" runat="server">
  <div class="container-fluid wizard-shell">
    <div class="mb-3">
      <h3 class="wizard-title">Loan Application Wizard</h3>
      <asp:Label
        ID="lblProgressText"
        runat="server"
        CssClass="small text-muted"
      />
      <div class="progress mt-2">
        <div
          id="divProgressBar"
          runat="server"
          class="progress-bar"
          role="progressbar"
          style="width: 25%"
        >
          25%
        </div>
      </div>
    </div>

    <asp:Label ID="lblGlobalMessage" runat="server" EnableViewState="false" />

    <div class="wizard-card p-3">
      <asp:Wizard
        ID="wizLoanApplication"
        runat="server"
        ActiveStepIndex="0"
        DisplaySideBar="false"
        OnNextButtonClick="wizLoanApplication_NextButtonClick"
        OnPreviousButtonClick="wizLoanApplication_PreviousButtonClick"
        OnFinishButtonClick="wizLoanApplication_FinishButtonClick"
        OnActiveStepChanged="wizLoanApplication_ActiveStepChanged"
      >
        <WizardSteps>
          <asp:WizardStep
            ID="wsPersonal"
            runat="server"
            Title="Personal Details"
            StepType="Start"
          >
            <div class="step-box">
              <h5>Step 1 - Personal Details</h5>
              <div class="form-row">
                <div class="form-group col-md-6">
                  <label>Full Name <span class="required-mark">*</span></label>
                  <asp:TextBox
                    ID="txtFullName"
                    runat="server"
                    CssClass="form-control"
                    MaxLength="120"
                  />
                  <asp:RequiredFieldValidator
                    ID="rfvFullName"
                    runat="server"
                    ControlToValidate="txtFullName"
                    ValidationGroup="Step1"
                    CssClass="text-danger"
                    ErrorMessage="Name is required."
                    Display="Dynamic"
                  />
                </div>
                <div class="form-group col-md-6">
                  <label>DOB <span class="required-mark">*</span></label>
                  <asp:TextBox
                    ID="txtDob"
                    runat="server"
                    CssClass="form-control"
                    TextMode="Date"
                  />
                  <asp:RequiredFieldValidator
                    ID="rfvDob"
                    runat="server"
                    ControlToValidate="txtDob"
                    ValidationGroup="Step1"
                    CssClass="text-danger"
                    ErrorMessage="DOB is required."
                    Display="Dynamic"
                  />
                  <asp:CustomValidator
                    ID="cvDobAge"
                    runat="server"
                    ControlToValidate="txtDob"
                    ValidationGroup="Step1"
                    ClientValidationFunction="validateDobAge"
                    OnServerValidate="cvDobAge_ServerValidate"
                    CssClass="text-danger"
                    ErrorMessage="Applicant age must be between 21 and 65."
                    Display="Dynamic"
                  />
                </div>
              </div>

              <div class="form-row">
                <div class="form-group col-md-6">
                  <label>PAN <span class="required-mark">*</span></label>
                  <asp:TextBox
                    ID="txtPan"
                    runat="server"
                    CssClass="form-control text-uppercase"
                    MaxLength="10"
                  />
                  <asp:RequiredFieldValidator
                    ID="rfvPan"
                    runat="server"
                    ControlToValidate="txtPan"
                    ValidationGroup="Step1"
                    CssClass="text-danger"
                    ErrorMessage="PAN is required."
                    Display="Dynamic"
                  />
                  <asp:RegularExpressionValidator
                    ID="revPan"
                    runat="server"
                    ControlToValidate="txtPan"
                    ValidationGroup="Step1"
                    CssClass="text-danger"
                    Display="Dynamic"
                    ValidationExpression="^[A-Z]{5}[0-9]{4}[A-Z]{1}$"
                    ErrorMessage="PAN format invalid (e.g. ABCDE1234F)."
                  />
                </div>
                <div class="form-group col-md-6">
                  <label
                    >Aadhaar (masked)
                    <span class="required-mark">*</span></label
                  >
                  <asp:TextBox
                    ID="txtAadhaar"
                    runat="server"
                    CssClass="form-control"
                    TextMode="Password"
                    MaxLength="12"
                  />
                  <asp:RequiredFieldValidator
                    ID="rfvAadhaar"
                    runat="server"
                    ControlToValidate="txtAadhaar"
                    ValidationGroup="Step1"
                    CssClass="text-danger"
                    ErrorMessage="Aadhaar is required."
                    Display="Dynamic"
                  />
                  <asp:RegularExpressionValidator
                    ID="revAadhaar"
                    runat="server"
                    ControlToValidate="txtAadhaar"
                    ValidationGroup="Step1"
                    CssClass="text-danger"
                    Display="Dynamic"
                    ValidationExpression="^[0-9]{12}$"
                    ErrorMessage="Aadhaar must be 12 digits."
                  />
                </div>
              </div>

              <div class="form-group">
                <label>Address <span class="required-mark">*</span></label>
                <asp:TextBox
                  ID="txtAddress"
                  runat="server"
                  CssClass="form-control"
                  TextMode="MultiLine"
                  Rows="3"
                  MaxLength="500"
                />
                <asp:RequiredFieldValidator
                  ID="rfvAddress"
                  runat="server"
                  ControlToValidate="txtAddress"
                  ValidationGroup="Step1"
                  CssClass="text-danger"
                  ErrorMessage="Address is required."
                  Display="Dynamic"
                />
              </div>
            </div>
          </asp:WizardStep>

          <asp:WizardStep
            ID="wsEmployment"
            runat="server"
            Title="Employment &amp; Income"
            StepType="Step"
          >
            <div class="step-box">
              <h5>Step 2 - Employment &amp; Income</h5>
              <div class="form-row">
                <div class="form-group col-md-6">
                  <label
                    >Employment Type <span class="required-mark">*</span></label
                  >
                  <asp:DropDownList
                    ID="ddlEmploymentType"
                    runat="server"
                    CssClass="form-control"
                  >
                    <asp:ListItem Text="Select" Value="" />
                    <asp:ListItem Text="Salaried" Value="Salaried" />
                    <asp:ListItem Text="SelfEmployed" Value="SelfEmployed" />
                    <asp:ListItem Text="BusinessOwner" Value="BusinessOwner" />
                    <asp:ListItem Text="Contractor" Value="Contractor" />
                  </asp:DropDownList>
                  <asp:RequiredFieldValidator
                    ID="rfvEmploymentType"
                    runat="server"
                    ControlToValidate="ddlEmploymentType"
                    InitialValue=""
                    ValidationGroup="Step2"
                    CssClass="text-danger"
                    ErrorMessage="Employment Type is required."
                    Display="Dynamic"
                  />
                </div>
                <div class="form-group col-md-6">
                  <label
                    >Employer Name <span class="required-mark">*</span></label
                  >
                  <asp:TextBox
                    ID="txtEmployer"
                    runat="server"
                    CssClass="form-control"
                    MaxLength="150"
                  />
                  <asp:RequiredFieldValidator
                    ID="rfvEmployer"
                    runat="server"
                    ControlToValidate="txtEmployer"
                    ValidationGroup="Step2"
                    CssClass="text-danger"
                    ErrorMessage="Employer is required."
                    Display="Dynamic"
                  />
                </div>
              </div>

              <div class="form-row">
                <div class="form-group col-md-6">
                  <label
                    >Annual Income <span class="required-mark">*</span></label
                  >
                  <asp:TextBox
                    ID="txtAnnualIncome"
                    runat="server"
                    CssClass="form-control"
                    TextMode="Number"
                  />
                  <asp:RequiredFieldValidator
                    ID="rfvAnnualIncome"
                    runat="server"
                    ControlToValidate="txtAnnualIncome"
                    ValidationGroup="Step2"
                    CssClass="text-danger"
                    ErrorMessage="Annual income is required."
                    Display="Dynamic"
                  />
                  <asp:RangeValidator
                    ID="rvAnnualIncome"
                    runat="server"
                    ControlToValidate="txtAnnualIncome"
                    ValidationGroup="Step2"
                    CssClass="text-danger"
                    Display="Dynamic"
                    Type="Double"
                    MinimumValue="100000"
                    MaximumValue="100000000"
                    ErrorMessage="Income must be between 1,00,000 and 10,00,00,000."
                  />
                </div>
                <div class="form-group col-md-6">
                  <label
                    >Work Experience (years)
                    <span class="required-mark">*</span></label
                  >
                  <asp:TextBox
                    ID="txtWorkExperience"
                    runat="server"
                    CssClass="form-control"
                    TextMode="Number"
                  />
                  <asp:RequiredFieldValidator
                    ID="rfvWorkExperience"
                    runat="server"
                    ControlToValidate="txtWorkExperience"
                    ValidationGroup="Step2"
                    CssClass="text-danger"
                    ErrorMessage="Work Experience is required."
                    Display="Dynamic"
                  />
                  <asp:RangeValidator
                    ID="rvWorkExperience"
                    runat="server"
                    ControlToValidate="txtWorkExperience"
                    ValidationGroup="Step2"
                    CssClass="text-danger"
                    Display="Dynamic"
                    Type="Integer"
                    MinimumValue="0"
                    MaximumValue="45"
                    ErrorMessage="Work Experience must be between 0 and 45 years."
                  />
                </div>
              </div>

              <div class="form-group">
                <label>ITR Upload (pdf/jpg/jpeg/png)</label>
                <asp:FileUpload
                  ID="fuItr"
                  runat="server"
                  CssClass="form-control-file"
                />
                <asp:CustomValidator
                  ID="cvItrUpload"
                  runat="server"
                  ValidationGroup="Step2"
                  Display="Dynamic"
                  CssClass="text-danger"
                  ErrorMessage="Only pdf, jpg, jpeg, png files are allowed."
                  ClientValidationFunction="validateItrFileClient"
                  OnServerValidate="cvItrUpload_ServerValidate"
                />
              </div>
            </div>
          </asp:WizardStep>

          <asp:WizardStep
            ID="wsLoanDetails"
            runat="server"
            Title="Loan Details"
            StepType="Step"
          >
            <div class="step-box">
              <h5>Step 3 - Loan Details</h5>
              <div class="form-row">
                <div class="form-group col-md-6">
                  <label>Purpose <span class="required-mark">*</span></label>
                  <asp:DropDownList
                    ID="ddlPurpose"
                    runat="server"
                    CssClass="form-control"
                  >
                    <asp:ListItem Text="Select" Value="" />
                    <asp:ListItem Text="Home" Value="Home" />
                    <asp:ListItem Text="Personal" Value="Personal" />
                    <asp:ListItem Text="Education" Value="Education" />
                    <asp:ListItem Text="Vehicle" Value="Vehicle" />
                    <asp:ListItem Text="Business" Value="Business" />
                  </asp:DropDownList>
                  <asp:RequiredFieldValidator
                    ID="rfvPurpose"
                    runat="server"
                    ControlToValidate="ddlPurpose"
                    InitialValue=""
                    ValidationGroup="Step3"
                    CssClass="text-danger"
                    ErrorMessage="Loan purpose is required."
                    Display="Dynamic"
                  />
                </div>
                <div class="form-group col-md-6">
                  <label
                    >Loan Amount <span class="required-mark">*</span></label
                  >
                  <asp:TextBox
                    ID="txtAmount"
                    runat="server"
                    CssClass="form-control"
                    TextMode="Number"
                  />
                  <small class="form-text text-muted"
                    >Range 50,000 to 50,00,000</small
                  >
                  <asp:RangeValidator
                    ID="rvAmount"
                    runat="server"
                    ControlToValidate="txtAmount"
                    ValidationGroup="Step3"
                    CssClass="text-danger"
                    Display="Dynamic"
                    Type="Double"
                    MinimumValue="50000"
                    MaximumValue="5000000"
                    ErrorMessage="Amount must be between 50,000 and 50,00,000."
                  />
                  <asp:RequiredFieldValidator
                    ID="rfvAmount"
                    runat="server"
                    ControlToValidate="txtAmount"
                    ValidationGroup="Step3"
                    CssClass="text-danger"
                    ErrorMessage="Amount is required."
                    Display="Dynamic"
                  />
                  <input
                    id="rngAmount"
                    runat="server"
                    type="range"
                    min="50000"
                    max="5000000"
                    step="10000"
                    value="500000"
                    class="custom-range mt-2"
                  />
                </div>
              </div>

              <div class="form-group">
                <label>Tenure <span class="required-mark">*</span></label>
                <asp:RadioButtonList
                  ID="rblTenure"
                  runat="server"
                  RepeatDirection="Horizontal"
                  CssClass="d-flex flex-wrap"
                >
                  <asp:ListItem Text="12 Months" Value="12" />
                  <asp:ListItem Text="24 Months" Value="24" />
                  <asp:ListItem Text="36 Months" Value="36" Selected="True" />
                  <asp:ListItem Text="48 Months" Value="48" />
                  <asp:ListItem Text="60 Months" Value="60" />
                </asp:RadioButtonList>
                <asp:RequiredFieldValidator
                  ID="rfvTenure"
                  runat="server"
                  ControlToValidate="rblTenure"
                  ValidationGroup="Step3"
                  CssClass="text-danger"
                  ErrorMessage="Tenure is required."
                  Display="Dynamic"
                />
              </div>

              <div class="form-group form-check">
                <asp:CheckBox
                  ID="chkCoApplicant"
                  runat="server"
                  CssClass="form-check-input"
                />
                <label
                  class="form-check-label"
                  for="<%= chkCoApplicant.ClientID %>"
                  >Include Co-applicant</label
                >
              </div>

              <asp:Label
                ID="lblEmiPreview"
                runat="server"
                CssClass="alert alert-info d-block"
                Text="EMI Preview: --"
              />
            </div>
          </asp:WizardStep>

          <asp:WizardStep
            ID="wsReview"
            runat="server"
            Title="Review &amp; Submit"
            StepType="Finish"
          >
            <div class="step-box">
              <h5>Step 4 - Review &amp; Submit</h5>
              <asp:Panel ID="pnlSummary" runat="server" CssClass="mb-3">
                <div class="summary-row">
                  <strong>Name:</strong>
                  <asp:Literal ID="litSummaryName" runat="server" />
                </div>
                <div class="summary-row">
                  <strong>DOB:</strong>
                  <asp:Literal ID="litSummaryDob" runat="server" />
                </div>
                <div class="summary-row">
                  <strong>PAN:</strong>
                  <asp:Literal ID="litSummaryPan" runat="server" />
                </div>
                <div class="summary-row">
                  <strong>Address:</strong>
                  <asp:Literal ID="litSummaryAddress" runat="server" />
                </div>
                <div class="summary-row">
                  <strong>Employment:</strong>
                  <asp:Literal ID="litSummaryEmployment" runat="server" />
                </div>
                <div class="summary-row">
                  <strong>Annual Income:</strong>
                  <asp:Literal ID="litSummaryIncome" runat="server" />
                </div>
                <div class="summary-row">
                  <strong>Work Experience:</strong>
                  <asp:Literal ID="litSummaryExperience" runat="server" />
                </div>
                <div class="summary-row">
                  <strong>Purpose:</strong>
                  <asp:Literal ID="litSummaryPurpose" runat="server" />
                </div>
                <div class="summary-row">
                  <strong>Amount:</strong>
                  <asp:Literal ID="litSummaryAmount" runat="server" />
                </div>
                <div class="summary-row">
                  <strong>Tenure:</strong>
                  <asp:Literal ID="litSummaryTenure" runat="server" />
                </div>
                <div class="summary-row">
                  <strong>Co-applicant:</strong>
                  <asp:Literal ID="litSummaryCoApplicant" runat="server" />
                </div>
              </asp:Panel>

              <div class="form-group form-check">
                <asp:CheckBox
                  ID="chkDeclaration"
                  runat="server"
                  CssClass="form-check-input"
                />
                <label
                  class="form-check-label"
                  for="<%= chkDeclaration.ClientID %>"
                >
                  I declare the information submitted is true and accurate.
                </label>
                <asp:CustomValidator
                  ID="cvDeclaration"
                  runat="server"
                  ValidationGroup="Step4"
                  Display="Dynamic"
                  CssClass="text-danger"
                  ErrorMessage="You must accept declaration before submit."
                  OnServerValidate="cvDeclaration_ServerValidate"
                  ClientValidationFunction="validateDeclarationClient"
                />
              </div>
            </div>
          </asp:WizardStep>

          <asp:WizardStep
            ID="wsComplete"
            runat="server"
            Title="Completed"
            StepType="Complete"
          >
            <div class="step-box">
              <h5 class="text-success">Application Submitted</h5>
              <p>Your loan application has been submitted successfully.</p>
              <p>
                <strong>Application Reference Number:</strong>
                <asp:Label ID="lblReferenceNumber" runat="server" />
              </p>
            </div>
          </asp:WizardStep>
        </WizardSteps>

        <StartNavigationTemplate>
          <asp:Button
            ID="Step1NextButton"
            runat="server"
            CommandName="MoveNext"
            Text="Next"
            CssClass="btn btn-primary"
            ValidationGroup="Step1"
          />
        </StartNavigationTemplate>

        <StepNavigationTemplate>
          <asp:Button
            ID="StepPrevButton"
            runat="server"
            CommandName="MovePrevious"
            Text="Back"
            CssClass="btn btn-outline-secondary"
            CausesValidation="false"
          />
          <asp:Button
            ID="StepNextButton"
            runat="server"
            CommandName="MoveNext"
            Text="Next"
            CssClass="btn btn-primary ml-2"
            ValidationGroup='<%# wizLoanApplication.ActiveStepIndex == 1 ? "Step2" : "Step3" %>'
          />
        </StepNavigationTemplate>

        <FinishNavigationTemplate>
          <asp:Button
            ID="FinishPrevButton"
            runat="server"
            CommandName="MovePrevious"
            Text="Back"
            CssClass="btn btn-outline-secondary"
            CausesValidation="false"
          />
          <asp:Button
            ID="FinishButton"
            runat="server"
            CommandName="MoveComplete"
            Text="Submit Application"
            CssClass="btn btn-success ml-2"
            ValidationGroup="Step4"
          />
        </FinishNavigationTemplate>
      </asp:Wizard>
    </div>
  </div>
</asp:Content>
