using System;
using System.Web;
using System.Web.Compilation;
using System.Web.UI;

namespace LOCDS.Web.Infrastructure
{
    public class UnityPageFactory : PageHandlerFactory
    {
        public override IHttpHandler GetHandler(HttpContext context, string requestType, string virtualPath, string path)
        {
            var pageType = BuildManager.GetCompiledType(virtualPath);
            if (pageType == null || !typeof(Page).IsAssignableFrom(pageType))
            {
                return base.GetHandler(context, requestType, virtualPath, path);
            }

            try
            {
                return (IHttpHandler)App_Start.UnityConfig.CurrentContainer.Resolve(pageType);
            }
            catch
            {
                // Fall back to the default page factory when the page type has unresolved dependencies.
                return base.GetHandler(context, requestType, virtualPath, path);
            }
        }
    }
}