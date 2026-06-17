using System.Web;

[assembly: PreApplicationStartMethod(typeof(LOCDS.Web.App_Start.UnityWebActivator), "Start")]

namespace LOCDS.Web.App_Start
{
    public static class UnityWebActivator
    {
        public static void Start()
        {
            UnityConfig.RegisterComponents();
        }
    }
}
