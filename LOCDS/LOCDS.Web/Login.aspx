<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Login.aspx.cs"
Inherits="LOCDS.Web.Login" %>

<!DOCTYPE html>
<html lang="en">
  <head runat="server">
    <meta charset="utf-8" />
    <meta
      name="viewport"
      content="width=device-width, initial-scale=1, shrink-to-fit=no"
    />
    <title>LOCDS Secure Login</title>
    <link
      rel="stylesheet"
      href="https://cdn.jsdelivr.net/npm/bootstrap@4.6.2/dist/css/bootstrap.min.css"
    />
    <style type="text/css">
      :root {
        --locds-navy: #0b1f3a;
        --locds-gold: #c9a227;
        --locds-bg: #f3f6fb;
      }

      body {
        min-height: 100vh;
        background: radial-gradient(
          circle at 20% 20%,
          #e7edf8,
          var(--locds-bg)
        );
        display: flex;
        align-items: center;
        justify-content: center;
        color: #11223c;
      }

      .login-card {
        width: 100%;
        max-width: 460px;
        border: none;
        border-radius: 12px;
        box-shadow: 0 12px 30px rgba(12, 23, 42, 0.16);
        overflow: hidden;
      }

      .login-header {
        background: linear-gradient(120deg, #09172c, var(--locds-navy));
        color: #ffffff;
        padding: 1rem 1.25rem;
        border-bottom: 3px solid var(--locds-gold);
      }

      .brand-title {
        margin: 0;
        font-size: 1.1rem;
        font-weight: 700;
        letter-spacing: 0.8px;
        text-transform: uppercase;
      }

      .btn-gold {
        background-color: var(--locds-gold);
        border-color: var(--locds-gold);
        color: #1f1f1f;
        font-weight: 600;
      }

      .btn-gold:hover {
        background-color: #b8931f;
        border-color: #b8931f;
        color: #111;
      }

      .small-note {
        font-size: 0.85rem;
        color: #576b86;
      }
    </style>
  </head>
  <body>
    <form id="formLogin" runat="server">
      <input
        type="hidden"
        id="__RequestVerificationToken"
        name="__RequestVerificationToken"
        runat="server"
      />

      <div class="card login-card">
        <div class="login-header">
          <h1 class="brand-title">LOCDS Enterprise Access</h1>
          <small>Secure authentication portal</small>
        </div>

        <div class="card-body p-4">
          <div class="form-group">
            <label for="txtUsername">Username</label>
            <asp:TextBox
              ID="txtUsername"
              runat="server"
              CssClass="form-control"
              MaxLength="100"
            />
            <asp:RequiredFieldValidator
              ID="rfvUsername"
              runat="server"
              ControlToValidate="txtUsername"
              ErrorMessage="Username is required."
              CssClass="text-danger small"
              Display="Dynamic"
            />
          </div>

          <div class="form-group">
            <label for="txtPassword">Password</label>
            <asp:TextBox
              ID="txtPassword"
              runat="server"
              CssClass="form-control"
              TextMode="Password"
              MaxLength="200"
            />
            <asp:RequiredFieldValidator
              ID="rfvPassword"
              runat="server"
              ControlToValidate="txtPassword"
              ErrorMessage="Password is required."
              CssClass="text-danger small"
              Display="Dynamic"
            />
          </div>

          <asp:Panel
            ID="pnlCaptcha"
            runat="server"
            Visible="false"
            CssClass="border rounded p-3 mb-3 bg-light"
          >
            <div class="font-weight-bold mb-2">Security Check</div>
            <label class="small mb-1"
              >Enter result for:
              <asp:Label
                ID="lblCaptchaQuestion"
                runat="server"
                CssClass="font-weight-bold"
            /></label>
            <asp:TextBox
              ID="txtCaptcha"
              runat="server"
              CssClass="form-control"
              MaxLength="10"
            />
            <asp:Label
              ID="lblCaptchaHint"
              runat="server"
              CssClass="small text-muted"
              Text="CAPTCHA is enabled after multiple failed attempts."
            />
          </asp:Panel>

          <div class="custom-control custom-checkbox mb-3">
            <asp:CheckBox
              ID="chkRememberMe"
              runat="server"
              CssClass="custom-control-input"
            />
            <label
              class="custom-control-label"
              for="<%= chkRememberMe.ClientID %>"
              >Remember me for 7 days</label
            >
          </div>

          <asp:Button
            ID="btnLogin"
            runat="server"
            Text="Sign In"
            CssClass="btn btn-gold btn-block"
            UseSubmitBehavior="true"
            CausesValidation="false"
            OnClick="btnLogin_Click"
          />

          <div class="mt-3 d-flex justify-content-between align-items-center">
            <asp:LinkButton
              ID="lnkForgotPassword"
              runat="server"
              CausesValidation="false"
              OnClick="lnkForgotPassword_Click"
              >Forgot Password?</asp:LinkButton
            >
            <span class="small-note">OTP will be sent to registered email</span>
          </div>

          <asp:Label
            ID="lblFeedback"
            runat="server"
            CssClass="d-block mt-3 text-danger"
            EnableViewState="false"
          />
          <asp:ValidationSummary
            ID="vsLogin"
            runat="server"
            CssClass="small text-danger mt-2"
            DisplayMode="List"
          />
        </div>
      </div>
    </form>

    <script src="https://code.jquery.com/jquery-3.7.1.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@4.6.2/dist/js/bootstrap.bundle.min.js"></script>
  </body>
</html>
