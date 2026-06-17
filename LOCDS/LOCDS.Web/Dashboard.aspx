<%@ Page Title="Credit Officer Dashboard" Language="C#"
MasterPageFile="~/Site.Master" AutoEventWireup="true"
CodeFile="Dashboard.aspx.cs" Inherits="LOCDS.Web.Dashboard" %>

<asp:Content
  ID="DashboardHead"
  ContentPlaceHolderID="HeadContent"
  runat="server"
>
  <style type="text/css">
    .dash-card {
      background: #fff;
      border: 1px solid #d9e2ef;
      border-radius: 8px;
    }
    .summary-box {
      border-left: 4px solid #0b1f3a;
      background: #f8fbff;
    }
    .status-badge {
      padding: 0.3rem 0.55rem;
      border-radius: 12px;
      font-size: 0.75rem;
      font-weight: 600;
      display: inline-block;
    }
    .status-draft {
      background: #e9ecef;
      color: #343a40;
    }
    .status-pending {
      background: #fff3cd;
      color: #664d03;
    }
    .status-review {
      background: #cfe2ff;
      color: #084298;
    }
    .status-approved {
      background: #d1e7dd;
      color: #0f5132;
    }
    .status-rejected {
      background: #f8d7da;
      color: #842029;
    }
    .grid-actions .btn {
      margin-right: 0.35rem;
      margin-bottom: 0.2rem;
    }
  </style>
</asp:Content>

<asp:Content
  ID="DashboardMain"
  ContentPlaceHolderID="MainContent"
  runat="server"
>
  <asp:UpdatePanel ID="upDashboard" runat="server" UpdateMode="Conditional">
    <ContentTemplate>
      <asp:Timer
        ID="tmrAutoRefresh"
        runat="server"
        Interval="60000"
        OnTick="tmrAutoRefresh_Tick"
      />

      <asp:Label ID="lblMessage" runat="server" EnableViewState="false" />

      <div class="dash-card p-3 mb-3">
        <h4 class="mb-3">Credit Officer Dashboard</h4>
        <div class="row">
          <div class="col-md-2 mb-2">
            <div class="summary-box p-2">
              <small class="text-muted">Total Apps</small><br />
              <strong><asp:Literal ID="litTotalApps" runat="server" /></strong>
            </div>
          </div>
          <div class="col-md-2 mb-2">
            <div class="summary-box p-2">
              <small class="text-muted">Pending Bureau</small><br />
              <strong
                ><asp:Literal ID="litPendingBureau" runat="server"
              /></strong>
            </div>
          </div>
          <div class="col-md-2 mb-2">
            <div class="summary-box p-2">
              <small class="text-muted">Under Review</small><br />
              <strong
                ><asp:Literal ID="litUnderReview" runat="server"
              /></strong>
            </div>
          </div>
          <div class="col-md-3 mb-2">
            <div class="summary-box p-2">
              <small class="text-muted">Approved Today</small><br />
              <strong
                ><asp:Literal ID="litApprovedToday" runat="server"
              /></strong>
            </div>
          </div>
          <div class="col-md-3 mb-2">
            <div class="summary-box p-2">
              <small class="text-muted">Rejected Today</small><br />
              <strong
                ><asp:Literal ID="litRejectedToday" runat="server"
              /></strong>
            </div>
          </div>
        </div>
      </div>

      <div class="dash-card p-3 mb-3">
        <div class="form-row">
          <div class="form-group col-md-3">
            <label>Status</label>
            <asp:DropDownList
              ID="ddlStatus"
              runat="server"
              CssClass="form-control"
            >
              <asp:ListItem Text="All" Value="" />
              <asp:ListItem Text="Draft" Value="0" />
              <asp:ListItem Text="Submitted" Value="1" />
              <asp:ListItem Text="Bureau Pending" Value="2" />
              <asp:ListItem Text="Under Review" Value="3" />
              <asp:ListItem Text="Approved" Value="4" />
              <asp:ListItem Text="Rejected" Value="5" />
              <asp:ListItem Text="Offer Sent" Value="6" />
              <asp:ListItem Text="Accepted" Value="7" />
              <asp:ListItem Text="Disbursed" Value="8" />
            </asp:DropDownList>
          </div>
          <div class="form-group col-md-2">
            <label>From Date</label>
            <asp:TextBox
              ID="txtFromDate"
              runat="server"
              CssClass="form-control"
              TextMode="Date"
            />
          </div>
          <div class="form-group col-md-2">
            <label>To Date</label>
            <asp:TextBox
              ID="txtToDate"
              runat="server"
              CssClass="form-control"
              TextMode="Date"
            />
          </div>
          <div class="form-group col-md-3">
            <label>Loan Type</label>
            <asp:DropDownList
              ID="ddlLoanType"
              runat="server"
              CssClass="form-control"
            >
              <asp:ListItem Text="All" Value="" />
              <asp:ListItem Text="Home" Value="Home" />
              <asp:ListItem Text="Personal" Value="Personal" />
              <asp:ListItem Text="Education" Value="Education" />
              <asp:ListItem Text="Vehicle" Value="Vehicle" />
              <asp:ListItem Text="Business" Value="Business" />
            </asp:DropDownList>
          </div>
          <div class="form-group col-md-2 d-flex align-items-end">
            <asp:Button
              ID="btnFilter"
              runat="server"
              Text="Apply Filter"
              CssClass="btn btn-primary mr-2"
              OnClick="btnFilter_Click"
            />
            <asp:Button
              ID="btnClear"
              runat="server"
              Text="Clear"
              CssClass="btn btn-outline-secondary"
              OnClick="btnClear_Click"
            />
          </div>
        </div>
      </div>

      <div class="dash-card p-3">
        <div class="d-flex justify-content-between align-items-center mb-2">
          <h5 class="mb-0">Applications</h5>
          <asp:Button
            ID="btnExportExcel"
            runat="server"
            Text="Export to Excel"
            CssClass="btn btn-success"
            OnClick="btnExportExcel_Click"
          />
        </div>

        <asp:GridView
          ID="gvDashboard"
          runat="server"
          CssClass="table table-striped table-bordered"
          AutoGenerateColumns="False"
          AllowPaging="True"
          AllowSorting="True"
          PageSize="20"
          DataKeyNames="ApplicationId"
          OnPageIndexChanging="gvDashboard_PageIndexChanging"
          OnSorting="gvDashboard_Sorting"
          OnRowCommand="gvDashboard_RowCommand"
          OnRowDataBound="gvDashboard_RowDataBound"
        >
          <Columns>
            <asp:BoundField
              HeaderText="App ID"
              DataField="ApplicationId"
              SortExpression="ApplicationId"
            />
            <asp:BoundField
              HeaderText="Applicant Name"
              DataField="ApplicantName"
              SortExpression="ApplicantName"
            />
            <asp:BoundField
              HeaderText="Loan Amount"
              DataField="LoanAmount"
              DataFormatString="{0:N2}"
              SortExpression="LoanAmount"
            />
            <asp:BoundField
              HeaderText="Purpose"
              DataField="Purpose"
              SortExpression="Purpose"
            />
            <asp:TemplateField HeaderText="Status" SortExpression="Status">
              <ItemTemplate>
                <asp:Label ID="lblStatus" runat="server" />
              </ItemTemplate>
            </asp:TemplateField>
            <asp:BoundField
              HeaderText="Applied Date"
              DataField="CreatedDate"
              DataFormatString="{0:yyyy-MM-dd HH:mm}"
              SortExpression="CreatedDate"
            />
            <asp:BoundField
              HeaderText="Assigned To"
              DataField="AssignedTo"
              SortExpression="AssignedTo"
            />
            <asp:TemplateField HeaderText="Actions">
              <ItemTemplate>
                <div class="grid-actions">
                  <asp:LinkButton
                    ID="lnkView"
                    runat="server"
                    CssClass="btn btn-sm btn-outline-primary"
                    CommandName="ViewApp"
                    CommandArgument='<%# Eval("ApplicationId") %>'
                    >View</asp:LinkButton
                  >
                  <asp:LinkButton
                    ID="lnkPullBureau"
                    runat="server"
                    CssClass="btn btn-sm btn-outline-warning"
                    CommandName="PullBureau"
                    CommandArgument='<%# Eval("ApplicationId") %>'
                    >Pull Bureau Report</asp:LinkButton
                  >
                  <asp:LinkButton
                    ID="lnkAssign"
                    runat="server"
                    CssClass="btn btn-sm btn-outline-info"
                    CommandName="AssignUnderwriter"
                    CommandArgument='<%# Eval("ApplicationId") %>'
                    >Assign to Underwriter</asp:LinkButton
                  >
                </div>
              </ItemTemplate>
            </asp:TemplateField>
          </Columns>
        </asp:GridView>
      </div>
    </ContentTemplate>
    <Triggers>
      <asp:AsyncPostBackTrigger ControlID="tmrAutoRefresh" EventName="Tick" />
      <asp:AsyncPostBackTrigger ControlID="btnFilter" EventName="Click" />
      <asp:AsyncPostBackTrigger ControlID="btnClear" EventName="Click" />
      <asp:PostBackTrigger ControlID="btnExportExcel" />
    </Triggers>
  </asp:UpdatePanel>
</asp:Content>
