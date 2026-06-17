using System.Data;

namespace LOCDS.DAL.Connection
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}
