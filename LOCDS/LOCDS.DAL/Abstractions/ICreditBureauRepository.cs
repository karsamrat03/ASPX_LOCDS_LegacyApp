using System.Threading;
using System.Threading.Tasks;
using LOCDS.Entities;

namespace LOCDS.DAL.Abstractions
{
    public interface ICreditBureauRepository
    {
        Task<long> SaveBureauReport(CreditBureauReport report, CancellationToken cancellationToken = default);
        Task<CreditBureauReport?> GetLatestReport(long applicationId, CancellationToken cancellationToken = default);
    }
}
