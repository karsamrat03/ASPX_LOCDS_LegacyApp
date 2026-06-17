using System.Threading;
using System.Threading.Tasks;

namespace LOCDS.BLL
{
    public interface IAuditService
    {
        Task Log(string entityType, string entityId, string action, string oldVal, string newVal, string userId, string ip, CancellationToken cancellationToken = default);
    }
}
