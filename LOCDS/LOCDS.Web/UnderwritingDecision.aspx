<%@ Page Title="Underwriting Decision" Language="C#"
MasterPageFile="~/Site.Master" AutoEventWireup="true"
CodeFile="UnderwritingDecision.aspx.cs"
Inherits="LOCDS.Web.UnderwritingDecisionPage" %>

<asp:Content
  ID="HeadDecision"
  ContentPlaceHolderID="HeadContent"
  runat="server"
>
  <style type="text/css">
    .uw-panel {
      background: #fff;
      border: 1px solid #d6deeb;
      border-radius: 8px;
      padding: 1rem;
      height: 100%;
    }
    .uw-title {
      font-size: 1rem;
      font-weight: 700;
      color: #0b1f3a;
      margin-bottom: 0.75rem;
    }
    .risk-badge {
      padding: 0.3rem 0.6rem;
      border-radius: 12px;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .risk-low {
      background: #d1e7dd;
      color: #0f5132;
    }
    .risk-medium {
      background: #fff3cd;
      color: #664d03;
    }
    .risk-high {
      background: #f8d7da;
      color: #842029;
    }
    .risk-veryhigh {
      background: #f1aeb5;
      color: #58151c;
    }
    .auto-rec {
      border-left: 4px solid #c9a227;
      background: #fff8e6;
      padding: 0.8rem;
      border-radius: 6px;
    }
    .foir-table td,
    .foir-table th {
      font-size: 0.9rem;
    }
    .history-grid th,
    .history-grid td {
      font-size: 0.88rem;
    }

    /* Manual Decision Form Improvements */
    .manual-decision-form .form-group {
      margin-bottom: 1.25rem;
    }
    .manual-decision-form label {
      font-weight: 600;
      color: #0b1f3a;
      margin-bottom: 0.5rem;
      font-size: 0.95rem;
    }
    .manual-decision-form .form-control,
    .manual-decision-form .form-check-input {
      border: 1px solid #ced4da;
      border-radius: 4px;
      font-size: 0.95rem;
    }
    .manual-decision-form .form-control:focus {
      border-color: #80bdff;
      box-shadow: 0 0 0 0.2rem rgba(0, 123, 255, 0.25);
    }

    .decision-options .form-check {
      margin-bottom: 0.75rem;
      padding-left: 1.5rem;
    }
    .decision-options .form-check-input {
      width: 1.1em;
      height: 1.1em;
      margin-top: 0.15em;
    }
    .decision-options .form-check-label {
      cursor: pointer;
      font-weight: 500;
    }

    .interest-rate-group {
      background: #f8f9fa;
      padding: 1rem;
      border-radius: 6px;
      margin-top: 0.5rem;
    }
    .interest-rate-group .form-check {
      margin-bottom: 0.75rem;
    }

    .docs-checklist {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1rem;
    }
    .docs-checklist .form-check {
      margin-bottom: 0;
      padding-left: 1.5rem;
    }
    .docs-checklist .form-check-input {
      width: 1.1em;
      height: 1.1em;
      margin-top: 0.15em;
    }
    .docs-checklist .form-check-label {
      cursor: pointer;
      font-size: 0.9rem;
    }

    .manual-decision-buttons {
      display: flex;
      gap: 0.75rem;
      margin-top: 1.5rem;
    }
    .manual-decision-buttons .btn {
      padding: 0.6rem 1.2rem;
      font-weight: 600;
      border-radius: 4px;
      transition: all 0.3s ease;
    }
    .manual-decision-buttons .btn-primary {
      background: #007bff;
      border-color: #007bff;
    }
    .manual-decision-buttons .btn-primary:hover {
      background: #0056b3;
      border-color: #0056b3;
    }
    .manual-decision-buttons .btn-outline-secondary {
      color: #6c757d;
      border: 1px solid #6c757d;
    }
    .manual-decision-buttons .btn-outline-secondary:hover {
      background: #6c757d;
      color: #fff;
    }
  </style>

  <script type="text/javascript">
    function onDecisionTypeChanged() {
      var remarksBox = document.getElementById("<%= txtRemarks.ClientID %>");
      if (!remarksBox) {
        return;
      }

      var decisions = [
        document.getElementById("<%= rdApprove.ClientID %>"),
        document.getElementById("<%= rdConditional.ClientID %>"),
        document.getElementById("<%= rdRefer.ClientID %>"),
        document.getElementById("<%= rdReject.ClientID %>"),
      ];

      var checked = null;
      for (var i = 0; i < decisions.length; i++) {
        if (decisions[i] && decisions[i].checked) {
          checked = decisions[i];
          break;
        }
      }

      if (!checked) {
        remarksBox.placeholder = "Add underwriting remarks";
        return;
      }

      var label = checked.parentElement
        ? checked.parentElement.querySelector("label")
        : null;
      var text = label ? label.innerText : checked.value;
      text = (text || "").toLowerCase();

      if (text.indexOf("reject") >= 0 || text.indexOf("refer") >= 0) {
        remarksBox.placeholder = "Remarks are mandatory for Reject/Refer";
      } else {
        remarksBox.placeholder = "Add underwriting remarks";
      }
    }

    function onInterestOverrideToggle() {
      var override = document.getElementById("<%= chkOverrideRate.ClientID %>");
      var rate = document.getElementById("<%= txtInterestRate.ClientID %>");
      var justification = document.getElementById(
        "<%= txtRateJustification.ClientID %>",
      );
      if (!override || !rate || !justification) {
        return;
      }

      var enabled = override.checked;
      rate.readOnly = !enabled;
      justification.disabled = !enabled;
      if (!enabled) {
        justification.value = "";
      }
    }

    function openConfirmModal() {
      if (window.jQuery) {
        jQuery("#decisionConfirmModal").modal("show");
      }
      return false;
    }

    function cleanupConfirmModalArtifacts() {
      if (window.jQuery) {
        jQuery("#decisionConfirmModal").modal("hide");
      }

      var backdrops = document.querySelectorAll(".modal-backdrop");
      for (var i = 0; i < backdrops.length; i++) {
        if (backdrops[i] && backdrops[i].parentNode) {
          backdrops[i].parentNode.removeChild(backdrops[i]);
        }
      }

      if (document.body) {
        document.body.classList.remove("modal-open");
        document.body.style.paddingRight = "";
      }
    }

    function onConfirmSubmitClick() {
      cleanupConfirmModalArtifacts();
      return true;
    }

    document.addEventListener("DOMContentLoaded", function () {
      onDecisionTypeChanged();
      onInterestOverrideToggle();
    });

    if (
      typeof Sys !== "undefined" &&
      Sys.WebForms &&
      Sys.WebForms.PageRequestManager
    ) {
      Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function () {
        cleanupConfirmModalArtifacts();
        onDecisionTypeChanged();
        onInterestOverrideToggle();
      });
    }
  </script>
</asp:Content>

<asp:Content
  ID="MainDecision"
  ContentPlaceHolderID="MainContent"
  runat="server"
>
  <asp:UpdatePanel ID="upDecision" runat="server" UpdateMode="Conditional">
    <ContentTemplate>
      <asp:Label ID="lblPageMessage" runat="server" EnableViewState="false" />
      <div class="row">
        <div class="col-lg-4 mb-3">
          <div class="uw-panel">
            <div class="uw-title">Applicant Profile &amp; Bureau</div>
            <p class="mb-1">
              <strong>Name:</strong>
              <asp:Label ID="lblApplicantName" runat="server" />
            </p>
            <p class="mb-1">
              <strong>DOB:</strong>
              <asp:Label ID="lblApplicantDob" runat="server" />
            </p>
            <p class="mb-1">
              <strong>PAN:</strong>
              <asp:Label ID="lblApplicantPan" runat="server" />
            </p>
            <p class="mb-2">
              <strong>Employment:</strong>
              <asp:Label ID="lblEmployment" runat="server" />
            </p>
            <p class="mb-2">
              <strong>Risk Tier:</strong>
              <asp:Label
                ID="lblRiskTier"
                runat="server"
                CssClass="risk-badge"
              />
            </p>

            <h6 class="mt-3">Bureau Scores (3 bureaus)</h6>
            <table class="table table-sm table-bordered">
              <thead>
                <tr>
                  <th>Bureau</th>
                  <th>Score</th>
                  <th>Default</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>CIBIL</td>
                  <td><asp:Label ID="lblScoreCibil" runat="server" /></td>
                  <td><asp:Label ID="lblDefaultCibil" runat="server" /></td>
                </tr>
                <tr>
                  <td>Experian</td>
                  <td><asp:Label ID="lblScoreExperian" runat="server" /></td>
                  <td><asp:Label ID="lblDefaultExperian" runat="server" /></td>
                </tr>
                <tr>
                  <td>Equifax</td>
                  <td><asp:Label ID="lblScoreEquifax" runat="server" /></td>
                  <td><asp:Label ID="lblDefaultEquifax" runat="server" /></td>
                </tr>
              </tbody>
            </table>

            <h6>FOIR Breakdown</h6>
            <table class="table table-sm foir-table">
              <tr>
                <th>Monthly Income</th>
                <td><asp:Label ID="lblMonthlyIncome" runat="server" /></td>
              </tr>
              <tr>
                <th>Existing EMI</th>
                <td><asp:Label ID="lblExistingEmi" runat="server" /></td>
              </tr>
              <tr>
                <th>Proposed EMI</th>
                <td><asp:Label ID="lblProposedEmi" runat="server" /></td>
              </tr>
              <tr>
                <th>FOIR %</th>
                <td><asp:Label ID="lblFoir" runat="server" /></td>
              </tr>
            </table>
          </div>
        </div>

        <div class="col-lg-4 mb-3">
          <div class="uw-panel">
            <div class="uw-title">Loan Context &amp; Recommendation</div>
            <p class="mb-1">
              <strong>Application ID:</strong>
              <asp:Label ID="lblApplicationId" runat="server" />
            </p>
            <p class="mb-1">
              <strong>Purpose:</strong>
              <asp:Label ID="lblPurpose" runat="server" />
            </p>
            <p class="mb-1">
              <strong>Requested Amount:</strong>
              <asp:Label ID="lblRequestedAmount" runat="server" />
            </p>
            <p class="mb-1">
              <strong>Tenure:</strong>
              <asp:Label ID="lblTenure" runat="server" />
            </p>

            <h6 class="mt-3">Existing Obligations</h6>
            <asp:GridView
              ID="gvObligations"
              runat="server"
              AutoGenerateColumns="false"
              CssClass="table table-sm table-bordered"
            >
              <Columns>
                <asp:BoundField HeaderText="Loan Type" DataField="LoanType" />
                <asp:BoundField
                  HeaderText="EMI"
                  DataField="Emi"
                  DataFormatString="{0:N2}"
                />
                <asp:BoundField
                  HeaderText="Outstanding"
                  DataField="Outstanding"
                  DataFormatString="{0:N2}"
                />
              </Columns>
            </asp:GridView>

            <p class="mb-2">
              <strong>LTV:</strong> <asp:Label ID="lblLtv" runat="server" />
            </p>

            <div class="auto-rec">
              <div><strong>Recommended Decision (Auto Engine)</strong></div>
              <div><asp:Label ID="lblAutoRecommendation" runat="server" /></div>
              <div class="small text-muted">
                <asp:Label ID="lblAutoRemarks" runat="server" />
              </div>
            </div>
          </div>
        </div>

        <div class="col-lg-4 mb-3">
          <div class="uw-panel">
            <div class="uw-title">Manual Decision</div>

            <div class="manual-decision-form">
              <div class="form-group">
                <label>Decision</label>
                <div class="decision-options">
                  <div class="form-check">
                    <asp:RadioButton
                      ID="rdApprove"
                      runat="server"
                      GroupName="Decision"
                      Value="Approve"
                      CssClass="form-check-input"
                      onchange="onDecisionTypeChanged()"
                    />
                    <label
                      class="form-check-label"
                      for="<%= rdApprove.ClientID %>"
                    >
                      Approve
                    </label>
                  </div>
                  <div class="form-check">
                    <asp:RadioButton
                      ID="rdConditional"
                      runat="server"
                      GroupName="Decision"
                      Value="ConditionalApprove"
                      CssClass="form-check-input"
                      onchange="onDecisionTypeChanged()"
                    />
                    <label
                      class="form-check-label"
                      for="<%= rdConditional.ClientID %>"
                    >
                      Conditional Approve
                    </label>
                  </div>
                  <div class="form-check">
                    <asp:RadioButton
                      ID="rdRefer"
                      runat="server"
                      GroupName="Decision"
                      Value="Review"
                      CssClass="form-check-input"
                      onchange="onDecisionTypeChanged()"
                    />
                    <label
                      class="form-check-label"
                      for="<%= rdRefer.ClientID %>"
                    >
                      Refer
                    </label>
                  </div>
                  <div class="form-check">
                    <asp:RadioButton
                      ID="rdReject"
                      runat="server"
                      GroupName="Decision"
                      Value="Reject"
                      CssClass="form-check-input"
                      onchange="onDecisionTypeChanged()"
                    />
                    <label
                      class="form-check-label"
                      for="<%= rdReject.ClientID %>"
                    >
                      Reject
                    </label>
                  </div>
                </div>
              </div>

              <div class="form-group">
                <label>Approved Amount</label>
                <div class="input-group">
                  <span class="input-group-text">₹</span>
                  <asp:TextBox
                    ID="txtApprovedAmount"
                    runat="server"
                    CssClass="form-control"
                    TextMode="Number"
                    placeholder="Enter amount"
                  />
                </div>
              </div>

              <div class="form-group">
                <label>Interest Rate (%)</label>
                <div class="interest-rate-group">
                  <div class="form-row">
                    <div class="col">
                      <asp:TextBox
                        ID="txtInterestRate"
                        runat="server"
                        CssClass="form-control"
                        TextMode="Number"
                        Step="0.01"
                        placeholder="Auto-calculated"
                      />
                    </div>
                  </div>
                  <div class="form-check mt-2">
                    <asp:CheckBox
                      ID="chkOverrideRate"
                      runat="server"
                      CssClass="form-check-input"
                      onclick="onInterestOverrideToggle()"
                    />
                    <label
                      class="form-check-label"
                      for="<%= chkOverrideRate.ClientID %>"
                    >
                      Override auto-calculated rate
                    </label>
                  </div>
                  <asp:TextBox
                    ID="txtRateJustification"
                    runat="server"
                    CssClass="form-control mt-2"
                    TextMode="MultiLine"
                    Rows="2"
                    placeholder="Justification for overriding rate"
                    Enabled="false"
                  />
                </div>
              </div>

              <div class="form-group">
                <label>Remarks</label>
                <asp:TextBox
                  ID="txtRemarks"
                  runat="server"
                  CssClass="form-control"
                  TextMode="MultiLine"
                  Rows="4"
                  placeholder="Add underwriting remarks"
                />
              </div>

              <div class="form-group">
                <label>Supporting Docs Checklist</label>
                <div class="docs-checklist">
                  <div class="form-check">
                    <asp:CheckBox
                      ID="chkIncomeProof"
                      runat="server"
                      CssClass="form-check-input"
                      Value="IncomeProof"
                    />
                    <label
                      class="form-check-label"
                      for="<%= chkIncomeProof.ClientID %>"
                    >
                      Income proof validated
                    </label>
                  </div>
                  <div class="form-check">
                    <asp:CheckBox
                      ID="chkBureauChecked"
                      runat="server"
                      CssClass="form-check-input"
                      Value="BureauChecked"
                    />
                    <label
                      class="form-check-label"
                      for="<%= chkBureauChecked.ClientID %>"
                    >
                      Bureau discrepancy checked
                    </label>
                  </div>
                  <div class="form-check">
                    <asp:CheckBox
                      ID="chkKycComplete"
                      runat="server"
                      CssClass="form-check-input"
                      Value="KycComplete"
                    />
                    <label
                      class="form-check-label"
                      for="<%= chkKycComplete.ClientID %>"
                    >
                      KYC complete
                    </label>
                  </div>
                  <div class="form-check">
                    <asp:CheckBox
                      ID="chkObligationCheck"
                      runat="server"
                      CssClass="form-check-input"
                      Value="ObligationCheck"
                    />
                    <label
                      class="form-check-label"
                      for="<%= chkObligationCheck.ClientID %>"
                    >
                      Obligations cross-verified
                    </label>
                  </div>
                </div>
              </div>

              <div class="manual-decision-buttons">
                <asp:Button
                  ID="btnShowConfirm"
                  runat="server"
                  Text="Submit Decision"
                  CssClass="btn btn-primary"
                  OnClientClick="return openConfirmModal();"
                  UseSubmitBehavior="false"
                />
                <asp:Button
                  ID="btnPrintSheet"
                  runat="server"
                  Text="Print Decision Sheet"
                  CssClass="btn btn-outline-secondary"
                  OnClick="btnPrintSheet_Click"
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="uw-panel mt-2">
        <div class="uw-title">Decision History</div>
        <asp:GridView
          ID="gvHistory"
          runat="server"
          AutoGenerateColumns="false"
          CssClass="table table-sm table-striped table-bordered history-grid"
        >
          <Columns>
            <asp:BoundField HeaderText="Decision ID" DataField="DecisionId" />
            <asp:BoundField HeaderText="Action" DataField="RecommendedAction" />
            <asp:BoundField
              HeaderText="Approved Amount"
              DataField="ApprovedAmount"
              DataFormatString="{0:N2}"
            />
            <asp:BoundField
              HeaderText="Rate"
              DataField="InterestRate"
              DataFormatString="{0:N2}"
            />
            <asp:BoundField HeaderText="Remarks" DataField="Remarks" />
            <asp:BoundField HeaderText="Decided By" DataField="DecidedBy" />
            <asp:BoundField
              HeaderText="Decided At"
              DataField="DecidedAt"
              DataFormatString="{0:yyyy-MM-dd HH:mm}"
            />
          </Columns>
        </asp:GridView>
      </div>

      <div
        class="modal fade"
        id="decisionConfirmModal"
        tabindex="-1"
        role="dialog"
        aria-labelledby="decisionConfirmLabel"
        aria-hidden="true"
      >
        <div class="modal-dialog" role="document">
          <div class="modal-content">
            <div class="modal-header bg-warning">
              <h5 class="modal-title" id="decisionConfirmLabel">
                Confirm Submission
              </h5>
              <button
                type="button"
                class="close"
                data-dismiss="modal"
                aria-label="Close"
              >
                <span aria-hidden="true">&times;</span>
              </button>
            </div>
            <div class="modal-body">
              Are you sure you want to submit this underwriting decision?
            </div>
            <div class="modal-footer">
              <button
                type="button"
                class="btn btn-secondary"
                data-dismiss="modal"
              >
                Cancel
              </button>
              <asp:Button
                ID="btnSubmitDecision"
                runat="server"
                Text="Confirm Submit"
                CssClass="btn btn-primary"
                ValidationGroup="Decision"
                OnClientClick="return onConfirmSubmitClick();"
                OnClick="btnSubmitDecision_Click"
              />
            </div>
          </div>
        </div>
      </div>
    </ContentTemplate>
    <Triggers>
      <asp:PostBackTrigger ControlID="btnPrintSheet" />
    </Triggers>
  </asp:UpdatePanel>
</asp:Content>
