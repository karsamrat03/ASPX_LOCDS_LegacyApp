using System.Collections.Generic;
using LOCDS.Common.Enums;

namespace LOCDS.Common.Constants
{
    public static class LoanPolicyConstants
    {
        public static readonly IReadOnlyDictionary<LoanPurpose, decimal> MinLoanAmountByPurpose =
            new Dictionary<LoanPurpose, decimal>
            {
                [LoanPurpose.HomeLoan] = 500000m,
                [LoanPurpose.PersonalLoan] = 50000m,
                [LoanPurpose.AutoLoan] = 100000m,
                [LoanPurpose.Education] = 75000m,
                [LoanPurpose.Business] = 200000m
            };

        public static readonly IReadOnlyDictionary<LoanPurpose, decimal> MaxLoanAmountByPurpose =
            new Dictionary<LoanPurpose, decimal>
            {
                [LoanPurpose.HomeLoan] = 15000000m,
                [LoanPurpose.PersonalLoan] = 4000000m,
                [LoanPurpose.AutoLoan] = 3000000m,
                [LoanPurpose.Education] = 5000000m,
                [LoanPurpose.Business] = 10000000m
            };

        public const decimal MaxFoirPrime = 0.50m;
        public const decimal MaxFoirNearPrime = 0.45m;
        public const decimal MaxFoirSubprime = 0.40m;
        public const decimal MaxFoirHighRisk = 0.35m;

        public static readonly IReadOnlyDictionary<RiskTier, int> MinCreditScoreByTier =
            new Dictionary<RiskTier, int>
            {
                [RiskTier.Prime] = 750,
                [RiskTier.NearPrime] = 700,
                [RiskTier.Subprime] = 620,
                [RiskTier.HighRisk] = 300
            };

        public static readonly IReadOnlyDictionary<RiskTier, int> MaxCreditScoreByTier =
            new Dictionary<RiskTier, int>
            {
                [RiskTier.Prime] = 900,
                [RiskTier.NearPrime] = 749,
                [RiskTier.Subprime] = 699,
                [RiskTier.HighRisk] = 619
            };
    }
}
