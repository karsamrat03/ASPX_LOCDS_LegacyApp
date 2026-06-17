using System.Threading;
using System.Threading.Tasks;
using LOCDS.Entities;

namespace LOCDS.DAL
{
    public interface ICreditApplicationRepository
    {
        Task SaveDecision(CreditApplication application, CreditDecision decision, CancellationToken cancellationToken = default);
    }
}