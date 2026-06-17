using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LOCDS.Entities;

namespace LOCDS.DAL.Abstractions
{
    public interface IUnderwritingRepository
    {
        Task<long> SaveDecision(UnderwritingDecision decision, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UnderwritingDecision>> GetDecisionHistory(long applicationId, CancellationToken cancellationToken = default);
    }
}
