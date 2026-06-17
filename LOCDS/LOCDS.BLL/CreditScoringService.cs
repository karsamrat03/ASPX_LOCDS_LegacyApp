using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LOCDS.Common.Enums;
using LOCDS.DAL.Abstractions;
using LOCDS.Entities;
using Polly;
using CommonRiskTier = LOCDS.Common.Enums.RiskTier;

namespace LOCDS.BLL
{
    public class CreditScoringService : ICreditScoringService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _auditService;
        private static readonly ConcurrentDictionary<string, CachedBureauReport> BureauCache = new ConcurrentDictionary<string, CachedBureauReport>();

        public CreditScoringService(IUnitOfWork unitOfWork, IAuditService auditService)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public async Task<CreditBureauReport> PullBureauReport(long applicationId, string applicantPAN, CancellationToken cancellationToken = default)
        {
            if (applicationId <= 0)
            {
                throw new ArgumentException("applicationId must be greater than zero.", nameof(applicationId));
            }

            if (string.IsNullOrWhiteSpace(applicantPAN))
            {
                throw new ArgumentException("applicantPAN is required.", nameof(applicantPAN));
            }

            var cacheKey = $"{applicationId}:{applicantPAN.ToUpperInvariant()}";
            if (BureauCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow)
            {
                await _auditService.Log(
                    "CreditScoring",
                    applicationId.ToString(),
                    "BureauPullCached",
                    string.Empty,
                    $"Score={cached.Report.Score}",
                    "SYSTEM",
                    "127.0.0.1",
                    cancellationToken).ConfigureAwait(false);

                return cached.Report;
            }

            var existing = await _unitOfWork.CreditBureaus.GetLatestReport(applicationId, cancellationToken).ConfigureAwait(false);
            if (existing != null && existing.PulledAt >= DateTime.UtcNow.AddHours(-24))
            {
                BureauCache[cacheKey] = new CachedBureauReport(existing, DateTime.UtcNow.AddHours(24));

                await _auditService.Log(
                    "CreditScoring",
                    applicationId.ToString(),
                    "BureauPullDb",
                    string.Empty,
                    $"Score={existing.Score}",
                    "SYSTEM",
                    "127.0.0.1",
                    cancellationToken).ConfigureAwait(false);

                return existing;
            }

            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(200 * retryAttempt));

            var report = await retryPolicy.ExecuteAsync(
                token => SimulateBureauApiCallAsync(applicationId, applicantPAN, token),
                cancellationToken).ConfigureAwait(false);

            report.ReportId = await _unitOfWork.CreditBureaus.SaveBureauReport(report, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            BureauCache[cacheKey] = new CachedBureauReport(report, DateTime.UtcNow.AddHours(24));

            await _auditService.Log(
                "CreditScoring",
                applicationId.ToString(),
                "BureauPullApi",
                string.Empty,
                $"Score={report.Score};Defaults={(report.DefaultHistory ? 1 : 0)}",
                "SYSTEM",
                "127.0.0.1",
                cancellationToken).ConfigureAwait(false);

            return report;
        }

        public decimal CalculateFoir(decimal existingEmi, decimal proposedEmi, decimal netMonthlyIncome)
        {
            if (existingEmi < 0)
            {
                throw new ArgumentException("existingEmi cannot be negative.", nameof(existingEmi));
            }

            if (proposedEmi < 0)
            {
                throw new ArgumentException("proposedEmi cannot be negative.", nameof(proposedEmi));
            }

            if (netMonthlyIncome <= 0)
            {
                throw new ArgumentException("netMonthlyIncome must be greater than zero.", nameof(netMonthlyIncome));
            }

            return Math.Round((existingEmi + proposedEmi) / netMonthlyIncome, 4, MidpointRounding.AwayFromZero);
        }

        public CommonRiskTier CalculateRiskTier(int creditScore, decimal foir, decimal existingEMI)
        {
            if (creditScore < 300 || creditScore > 900)
            {
                throw new ArgumentException("creditScore must be between 300 and 900.", nameof(creditScore));
            }

            if (foir < 0 || foir > 1)
            {
                throw new ArgumentException("foir must be between 0 and 1.", nameof(foir));
            }

            if (existingEMI < 0)
            {
                throw new ArgumentException("existingEMI cannot be negative.", nameof(existingEMI));
            }

            if (creditScore >= 750)
            {
                return CommonRiskTier.Prime;
            }

            if (creditScore >= 700)
            {
                return CommonRiskTier.NearPrime;
            }

            if (creditScore >= 650)
            {
                return CommonRiskTier.Subprime;
            }

            return CommonRiskTier.HighRisk;
        }

        public bool IsAutoRejected(int creditScore, decimal foir, int activeDefaults)
        {
            if (activeDefaults < 0)
            {
                throw new ArgumentException("activeDefaults cannot be negative.", nameof(activeDefaults));
            }

            return creditScore < 600 || foir > 0.65m || activeDefaults > 0;
        }

        public decimal GetLtvLimit(LoanPurpose loanPurpose, CommonRiskTier riskTier)
        {
            switch (loanPurpose)
            {
                case LoanPurpose.HomeLoan:
                    return riskTier == CommonRiskTier.Prime ? 0.90m : riskTier == CommonRiskTier.NearPrime ? 0.85m : riskTier == CommonRiskTier.Subprime ? 0.80m : 0.70m;
                case LoanPurpose.AutoLoan:
                    return riskTier == CommonRiskTier.Prime ? 0.85m : riskTier == CommonRiskTier.NearPrime ? 0.80m : riskTier == CommonRiskTier.Subprime ? 0.75m : 0.65m;
                case LoanPurpose.Education:
                    return riskTier == CommonRiskTier.Prime ? 0.90m : riskTier == CommonRiskTier.NearPrime ? 0.85m : riskTier == CommonRiskTier.Subprime ? 0.75m : 0.60m;
                case LoanPurpose.Business:
                    return riskTier == CommonRiskTier.Prime ? 0.80m : riskTier == CommonRiskTier.NearPrime ? 0.75m : riskTier == CommonRiskTier.Subprime ? 0.65m : 0.55m;
                case LoanPurpose.PersonalLoan:
                default:
                    return riskTier == CommonRiskTier.Prime ? 0.75m : riskTier == CommonRiskTier.NearPrime ? 0.70m : riskTier == CommonRiskTier.Subprime ? 0.60m : 0.50m;
            }
        }

        private static Task<CreditBureauReport> SimulateBureauApiCallAsync(long applicationId, string applicantPAN, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int hash = Math.Abs(applicantPAN.GetHashCode());
            int score = 550 + (hash % 301);

            var report = new CreditBureauReport
            {
                ApplicationId = applicationId,
                Bureau = (CreditBureauType)(hash % 3),
                Score = score,
                Enquiries = hash % 8,
                ActiveLoans = hash % 5,
                DefaultHistory = (hash % 9) == 0,
                PulledAt = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            return Task.FromResult(report);
        }

        private sealed class CachedBureauReport
        {
            public CachedBureauReport(CreditBureauReport report, DateTime expiresAtUtc)
            {
                Report = report;
                ExpiresAtUtc = expiresAtUtc;
            }

            public CreditBureauReport Report { get; }
            public DateTime ExpiresAtUtc { get; }
        }
    }
}