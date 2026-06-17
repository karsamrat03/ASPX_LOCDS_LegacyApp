using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LOCDS.BLL.DTOs;
using LOCDS.Entities;

namespace LOCDS.BLL
{
    public interface IUnderwritingService
    {
        Task<UnderwritingDecision> RunAutoDecision(long applicationId, CancellationToken cancellationToken = default);
        Task SubmitManualDecision(ManualDecisionDto decisionDto, long underwriterId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<LoanApplication>> GetDecisionQueue(long underwriterId, CancellationToken cancellationToken = default);
    }
}
