<%@ Page Title="Decision Sheet" Language="C#" AutoEventWireup="true"
CodeFile="DecisionSheet.aspx.cs" Inherits="LOCDS.Web.DecisionSheet" %>

<!DOCTYPE html>
<html lang="en">
  <head runat="server">
    <meta charset="utf-8" />
    <title>Underwriting Decision Sheet</title>
    <style type="text/css">
      body {
        font-family:
          Segoe UI,
          Arial,
          sans-serif;
        margin: 24px;
        color: #12243f;
      }
      h2 {
        margin-bottom: 8px;
      }
      .sheet {
        border: 1px solid #d0d8e8;
        border-radius: 8px;
        padding: 16px;
      }
      .row {
        margin-bottom: 8px;
      }
      .label {
        font-weight: 600;
        min-width: 210px;
        display: inline-block;
      }
      @media print {
        .no-print {
          display: none;
        }
        body {
          margin: 0;
        }
      }
    </style>
  </head>
  <body>
    <form id="formSheet" runat="server">
      <button type="button" class="no-print" onclick="window.print()">
        Print
      </button>
      <div class="sheet">
        <h2>LOCDS Underwriting Decision Sheet</h2>
        <div class="row">
          <span class="label">Application ID:</span
          ><asp:Label ID="lblApplicationId" runat="server" />
        </div>
        <div class="row">
          <span class="label">Applicant Name:</span
          ><asp:Label ID="lblApplicantName" runat="server" />
        </div>
        <div class="row">
          <span class="label">Requested Amount:</span
          ><asp:Label ID="lblRequestedAmount" runat="server" />
        </div>
        <div class="row">
          <span class="label">Recommended Decision:</span
          ><asp:Label ID="lblRecommendedDecision" runat="server" />
        </div>
        <div class="row">
          <span class="label">Manual Decision:</span
          ><asp:Label ID="lblManualDecision" runat="server" />
        </div>
        <div class="row">
          <span class="label">Approved Amount:</span
          ><asp:Label ID="lblApprovedAmount" runat="server" />
        </div>
        <div class="row">
          <span class="label">Interest Rate:</span
          ><asp:Label ID="lblInterestRate" runat="server" />
        </div>
        <div class="row">
          <span class="label">Remarks:</span
          ><asp:Label ID="lblRemarks" runat="server" />
        </div>
        <div class="row">
          <span class="label">Decided By:</span
          ><asp:Label ID="lblDecidedBy" runat="server" />
        </div>
        <div class="row">
          <span class="label">Decided At:</span
          ><asp:Label ID="lblDecidedAt" runat="server" />
        </div>
      </div>
    </form>
  </body>
</html>
