<%@ Page Title="Loan Offer" Language="C#" MasterPageFile="~/Site.Master"
AutoEventWireup="true" CodeFile="LoanOffer.aspx.cs"
Inherits="LOCDS.Web.LoanOfferPage" %>

<asp:Content ID="OfferHead" ContentPlaceHolderID="HeadContent" runat="server">
  <style type="text/css">
    .offer-card {
      border: 1px solid #d8e2ef;
      border-radius: 10px;
      box-shadow: 0 10px 22px rgba(10, 26, 53, 0.08);
      background: #ffffff;
    }
    .offer-title {
      color: #0b1f3a;
      font-weight: 700;
    }
    .metric-box {
      border-radius: 8px;
      border: 1px solid #dce6f4;
      background: linear-gradient(180deg, #fbfdff 0%, #f3f7fe 100%);
      padding: 0.85rem;
      height: 100%;
    }
    .metric-label {
      font-size: 0.8rem;
      color: #5a6f8f;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .metric-value {
      font-size: 1.15rem;
      color: #102644;
      font-weight: 700;
    }
    .countdown-chip {
      display: inline-block;
      padding: 0.35rem 0.65rem;
      border-radius: 14px;
      background: #fff3cd;
      color: #664d03;
      font-weight: 600;
      font-size: 0.85rem;
    }
    .tracker-step {
      border: 1px solid #d8e2ef;
      border-left-width: 5px;
      border-left-color: #ced4da;
      border-radius: 8px;
      padding: 0.65rem 0.85rem;
      margin-bottom: 0.55rem;
      background: #fbfdff;
    }
    .tracker-done {
      border-left-color: #198754;
      background: #edf9f1;
    }
    .tracker-active {
      border-left-color: #0d6efd;
      background: #eef4ff;
    }
    .tracker-pending {
      border-left-color: #adb5bd;
      background: #f8f9fa;
    }
  </style>

  <script type="text/javascript">
    var offerCountdownTimer = null;

    function openOtpModal() {
      if (window.jQuery) {
        jQuery("#otpConfirmModal").modal("show");
      }
      return false;
    }

    function closeOtpModal() {
      if (window.jQuery) {
        jQuery("#otpConfirmModal").modal("hide");
      }
    }

    function startOfferCountdown() {
      var expiryField = document.getElementById(
        "<%= hfOfferExpiryUtc.ClientID %>",
      );
      var label = document.getElementById("offerCountdownLabel");
      if (!expiryField || !label || !expiryField.value) {
        return;
      }

      var expiry = new Date(expiryField.value);
      if (isNaN(expiry.getTime())) {
        label.innerText = "Validity unavailable";
        return;
      }

      if (offerCountdownTimer) {
        window.clearInterval(offerCountdownTimer);
      }

      function tick() {
        var now = new Date();
        var diff = expiry.getTime() - now.getTime();
        if (diff <= 0) {
          label.innerText = "Offer expired";
          window.clearInterval(offerCountdownTimer);
          return;
        }

        var totalSeconds = Math.floor(diff / 1000);
        var hours = Math.floor(totalSeconds / 3600);
        var minutes = Math.floor((totalSeconds % 3600) / 60);
        var seconds = totalSeconds % 60;

        var hh = hours < 10 ? "0" + hours : "" + hours;
        var mm = minutes < 10 ? "0" + minutes : "" + minutes;
        var ss = seconds < 10 ? "0" + seconds : "" + seconds;

        label.innerText = "Offer expires in " + hh + ":" + mm + ":" + ss;
      }

      tick();
      offerCountdownTimer = window.setInterval(tick, 1000);
    }

    document.addEventListener("DOMContentLoaded", startOfferCountdown);
    if (
      typeof Sys !== "undefined" &&
      Sys.WebForms &&
      Sys.WebForms.PageRequestManager
    ) {
      Sys.WebForms.PageRequestManager.getInstance().add_endRequest(
        startOfferCountdown,
      );
    }
  </script>
</asp:Content>

<asp:Content ID="OfferMain" ContentPlaceHolderID="MainContent" runat="server">
  <div class="container-fluid">
    <asp:HiddenField ID="hfOfferExpiryUtc" runat="server" />
    <asp:Label ID="lblPageMessage" runat="server" EnableViewState="false" />

    <div class="offer-card p-3 mb-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="offer-title mb-1">Your Loan Offer</h4>
          <div class="text-muted small">
            Application ID: <asp:Label ID="lblApplicationId" runat="server" />
          </div>
        </div>
        <div class="countdown-chip" id="offerCountdownLabel">
          Loading offer validity...
        </div>
      </div>

      <div class="row">
        <div class="col-md-3 mb-2">
          <div class="metric-box">
            <div class="metric-label">Approved Amount</div>
            <div class="metric-value">
              <asp:Label ID="lblApprovedAmount" runat="server" />
            </div>
          </div>
        </div>
        <div class="col-md-3 mb-2">
          <div class="metric-box">
            <div class="metric-label">Interest Rate</div>
            <div class="metric-value">
              <asp:Label ID="lblInterestRate" runat="server" />
            </div>
          </div>
        </div>
        <div class="col-md-3 mb-2">
          <div class="metric-box">
            <div class="metric-label">Tenure</div>
            <div class="metric-value">
              <asp:Label ID="lblTenure" runat="server" />
            </div>
          </div>
        </div>
        <div class="col-md-3 mb-2">
          <div class="metric-box">
            <div class="metric-label">Monthly EMI</div>
            <div class="metric-value">
              <asp:Label ID="lblEmi" runat="server" />
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="offer-card p-3 mb-3">
      <h5 class="mb-3">Amortization Schedule</h5>
      <asp:GridView
        ID="gvAmortization"
        runat="server"
        CssClass="table table-sm table-striped table-bordered"
        AutoGenerateColumns="False"
      >
        <Columns>
          <asp:BoundField HeaderText="Month" DataField="Month" />
          <asp:BoundField
            HeaderText="EMI"
            DataField="EMI"
            DataFormatString="{0:N2}"
            HtmlEncode="false"
          />
          <asp:BoundField
            HeaderText="Principal"
            DataField="Principal"
            DataFormatString="{0:N2}"
            HtmlEncode="false"
          />
          <asp:BoundField
            HeaderText="Interest"
            DataField="Interest"
            DataFormatString="{0:N2}"
            HtmlEncode="false"
          />
          <asp:BoundField
            HeaderText="Closing Balance"
            DataField="ClosingBalance"
            DataFormatString="{0:N2}"
            HtmlEncode="false"
          />
        </Columns>
      </asp:GridView>
    </div>

    <div class="row">
      <div class="col-md-6 mb-3">
        <div class="offer-card p-3">
          <h5 class="mb-3">Total Cost Breakdown</h5>
          <table class="table table-sm mb-0">
            <tr>
              <th>Principal</th>
              <td class="text-right">
                <asp:Label ID="lblPrincipal" runat="server" />
              </td>
            </tr>
            <tr>
              <th>Total Interest</th>
              <td class="text-right">
                <asp:Label ID="lblTotalInterest" runat="server" />
              </td>
            </tr>
            <tr>
              <th>Processing Fee</th>
              <td class="text-right">
                <asp:Label ID="lblProcessingFee" runat="server" />
              </td>
            </tr>
            <tr>
              <th>Total Cost</th>
              <td class="text-right font-weight-bold">
                <asp:Label ID="lblTotalCost" runat="server" />
              </td>
            </tr>
          </table>
        </div>
      </div>

      <div class="col-md-6 mb-3">
        <div class="offer-card p-3">
          <h5 class="mb-3">Offer Action</h5>
          <div class="form-group">
            <label>Select EMI Debit Date</label>
            <asp:DropDownList
              ID="ddlEmiDate"
              runat="server"
              CssClass="form-control"
            >
              <asp:ListItem Text="1st of every month" Value="1" />
              <asp:ListItem Text="5th of every month" Value="5" />
              <asp:ListItem Text="10th of every month" Value="10" />
              <asp:ListItem Text="15th of every month" Value="15" />
            </asp:DropDownList>
          </div>

          <div class="mt-3">
            <asp:Button
              ID="btnAcceptOffer"
              runat="server"
              Text="Accept Offer"
              CssClass="btn btn-success mr-2"
              OnClientClick="return openOtpModal();"
              UseSubmitBehavior="false"
            />
            <asp:Button
              ID="btnRejectOffer"
              runat="server"
              Text="Reject Offer"
              CssClass="btn btn-outline-danger"
              OnClick="btnRejectOffer_Click"
            />
          </div>
        </div>
      </div>
    </div>

    <asp:Panel
      ID="pnlDisbursementTracker"
      runat="server"
      Visible="false"
      CssClass="offer-card p-3 mb-3"
    >
      <h5 class="mb-3">Disbursement Tracker</h5>
      <div id="step1" runat="server" class="tracker-step">Offer Accepted</div>
      <div id="step2" runat="server" class="tracker-step">
        KYC & Agreement Verification
      </div>
      <div id="step3" runat="server" class="tracker-step">
        Disbursement Scheduled
      </div>
      <div id="step4" runat="server" class="tracker-step">Amount Disbursed</div>
    </asp:Panel>
  </div>

  <div
    class="modal fade"
    id="otpConfirmModal"
    tabindex="-1"
    role="dialog"
    aria-hidden="true"
  >
    <div class="modal-dialog" role="document">
      <div class="modal-content">
        <div class="modal-header bg-primary text-white">
          <h5 class="modal-title">OTP Confirmation</h5>
          <button
            type="button"
            class="close text-white"
            data-dismiss="modal"
            aria-label="Close"
          >
            <span aria-hidden="true">&times;</span>
          </button>
        </div>
        <div class="modal-body">
          <p class="mb-2">
            Enter the OTP sent to your registered contact to accept this offer.
          </p>
          <div class="form-group mb-2">
            <asp:TextBox
              ID="txtOtp"
              runat="server"
              CssClass="form-control"
              MaxLength="6"
              placeholder="Enter 6-digit OTP"
            />
          </div>
          <asp:Label
            ID="lblOtpMessage"
            runat="server"
            EnableViewState="false"
          />
        </div>
        <div class="modal-footer">
          <asp:Button
            ID="btnSendOtp"
            runat="server"
            Text="Send OTP"
            CssClass="btn btn-outline-primary"
            OnClick="btnSendOtp_Click"
          />
          <asp:Button
            ID="btnConfirmAccept"
            runat="server"
            Text="Confirm Acceptance"
            CssClass="btn btn-success"
            OnClick="btnConfirmAccept_Click"
          />
        </div>
      </div>
    </div>
  </div>
</asp:Content>
