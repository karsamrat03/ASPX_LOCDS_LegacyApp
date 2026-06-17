using System.Threading;
using System.Threading.Tasks;
using LOCDS.Entities;

namespace LOCDS.DAL
{
    public interface IBureauApiClient
    {
        Task<BureauReport> GetReport(string socialSecurityNumber, CancellationToken cancellationToken = default);
    }
}