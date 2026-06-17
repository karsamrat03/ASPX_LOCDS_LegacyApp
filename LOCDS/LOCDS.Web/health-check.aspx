<%@ Page Language="C#" %> <%@ Import Namespace="System" %> <%@ Import
Namespace="System.Collections.Generic" %> <%@ Import
Namespace="System.Configuration" %> <%@ Import Namespace="System.Data.SqlClient"
%> <%@ Import Namespace="System.Reflection" %> <%@ Import
Namespace="System.Web.Script.Serialization" %>
<script runat="server">
  protected void Page_Load(object sender, EventArgs e)
  {
      Response.ContentType = "application/json";

      bool dbConnectivity = false;
      string connectionString = ConfigurationManager.ConnectionStrings["LOCDSConnection"]?.ConnectionString;

      if (!string.IsNullOrWhiteSpace(connectionString))
      {
          try
          {
              using (var connection = new SqlConnection(connectionString))
              {
                  connection.Open();
                  dbConnectivity = connection.State == System.Data.ConnectionState.Open;
              }
          }
          catch
          {
              dbConnectivity = false;
          }
      }

      string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
      var payload = new Dictionary<string, object>
      {
          ["status"] = dbConnectivity ? "ok" : "degraded",
          ["db-connectivity"] = dbConnectivity,
          ["version"] = version
      };

      var serializer = new JavaScriptSerializer();
      Response.Write(serializer.Serialize(payload));
      Response.End();
  }
</script>
