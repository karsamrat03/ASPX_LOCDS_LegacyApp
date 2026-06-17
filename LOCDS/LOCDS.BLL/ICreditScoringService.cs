using System.Threading;
using System.Threading.Tasks;
using LOCDS.Common.Enums;
using LOCDS.Entities;
using CommonRiskTier = LOCDS.Common.Enums.RiskTier;

namespace LOCDS.BLL
{
    public interface ICreditScoringService
    {
        Task<CreditBureauReport> PullBureauReport(long applicationId, string applicantPAN, CancellationToken cancellationToken = default);
        decimal CalculateFoir(decimal existingEmi, decimal proposedEmi, decimal netMonthlyIncome);
        CommonRiskTier CalculateRiskTier(int creditScore, decimal foir, decimal existingEMI);
        bool IsAutoRejected(int creditScore, decimal foir, int activeDefaults);
        decimal GetLtvLimit(LoanPurpose loanPurpose, CommonRiskTier riskTier);
    }
}