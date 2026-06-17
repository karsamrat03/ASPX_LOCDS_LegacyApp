<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html>
<html lang="en">
  <head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>LOCDS - 403</title>
    <style>
      :root {
        --navy: #0b1f3a;
        --gold: #c9a227;
        --bg: #f4f7fb;
      }
      body {
        margin: 0;
        font-family:
          Segoe UI,
          Arial,
          sans-serif;
        background: linear-gradient(180deg, #eef3fa, var(--bg));
        color: #12243f;
      }
      .wrap {
        max-width: 760px;
        margin: 80px auto;
        background: #fff;
        border: 1px solid #d8e2ef;
        border-radius: 12px;
        overflow: hidden;
        box-shadow: 0 10px 24px rgba(10, 26, 53, 0.1);
      }
      .head {
        background: var(--navy);
        color: #fff;
        padding: 16px 20px;
        border-bottom: 3px solid var(--gold);
      }
      .body {
        padding: 22px;
      }
      .code {
        font-size: 32px;
        font-weight: 700;
        color: var(--navy);
        margin: 0 0 8px;
      }
      a {
        color: #0b5ed7;
        text-decoration: none;
        font-weight: 600;
      }
    </style>
  </head>
  <body>
    <form id="form403" runat="server">
      <div class="wrap">
        <div class="head">LOCDS Enterprise Banking</div>
        <div class="body">
          <p class="code">403 - Access Denied</p>
          <p>You do not have permission to access this resource.</p>
          <p>
            <a href="~/Login.aspx" runat="server">Return to Secure Login</a>
          </p>
        </div>
      </div>
    </form>
  </body>
</html>
