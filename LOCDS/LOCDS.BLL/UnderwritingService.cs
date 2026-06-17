using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using LOCDS.BLL.DTOs;
using LOCDS.Common.Enums;
using LOCDS.DAL.Abstractions;
using LOCDS.Entities;
using CommonRiskTier = LOCDS.Common.Enums.RiskTier;

namespace LOCDS.BLL
{
    public class UnderwritingService : IUnderwritingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICreditScoringService _creditScoringService;
        private readonly IValidator<ManualDecisionDto> _manualDecisionValidator;
        private readonly IAuditService _auditService;

        public UnderwritingService(
            IUnitOfWork unitOfWork,
            ICreditScoringService creditScoringService,
            IValidator<ManualDecisionDto> manualDecisionValidator,
            IAuditService auditService)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _creditScoringService = creditScoringService ?? throw new ArgumentNullException(nameof(creditScoringService));
            _manualDecisionValidator = manualDecisionValidator ?? throw new ArgumentNullException(nameof(manualDecisionValidator));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public async Task<UnderwritingDecision> RunAutoDecision(long applicationId, CancellationToken cancellationToken = default)
        {
            var detail = await _unitOfWork.LoanApplications.GetApplicationWithFullDetail(applicationId, cancellationToken).ConfigureAwait(false);
            if (detail == null)
            {
                throw new InvalidOperationException("Application not found.");
            }

            var latestReport = detail.BureauReports.OrderByDescending(x => x.PulledAt).FirstOrDefault();
            if (latestReport == null)
            {
                throw new InvalidOperationException("No bureau report found for auto decision.");
            }

            decimal annualIncome = detail.Applicant?.AnnualIncome ?? 0m;
            decimal netMonthlyIncome = annualIncome > 0 ? annualIncome / 12m : 0m;
            decimal existingEmi = latestReport.ActiveLoans * 5000m;
            decimal proposedEmi = CalculateProposedEmi(detail.LoanApplication.LoanAmount, detail.LoanApplication.Tenure, 11m);
            decimal foir = _creditScoringService.CalculateFoir(existingEmi, proposedEmi, netMonthlyIncome <= 0 ? 1m : netMonthlyIncome);
            var riskTier = _creditScoringService.CalculateRiskTier(latestReport.Score, foir, existingEmi);
            var loanPurpose = ParseLoanPurpose(detail.LoanApplication.Purpose);

            var context = new UnderwritingContext(
                detail.LoanApplication,
                latestReport,
                riskTier,
                loanPurpose,
                foir,
                existingEmi,
                proposedEmi);

            var chain = BuildRuleChain();
            var outcome = chain.Evaluate(context);

            decimal interestRate = ResolveInterestRate(outcome.RiskTier);
            decimal approvedAmount = Math.Round(detail.LoanApplication.LoanAmount * outcome.ApprovalFactor, 2);
            decimal ltvLimit = _creditScoringService.GetLtvLimit(outcome.LoanPurpose, outcome.RiskTier);
            decimal cappedAmount = Math.Round(Math.Min(approvedAmount, detail.LoanApplication.LoanAmount * ltvLimit), 2);

            var decision = new UnderwritingDecision
            {
                ApplicationId = applicationId,
                RecommendedAction = outcome.Action,
                ApprovedAmount = outcome.Action == RecommendedAction.Reject ? 0m : cappedAmount,
                InterestRate = outcome.Action == RecommendedAction.Reject ? 0m : interestRate,
                Tenure = detail.LoanApplication.Tenure,
                RiskScore = latestReport.Score,
                Remarks = outcome.Reason,
                DecidedBy = "SYSTEM",
                DecidedAt = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            decision.DecisionId = await _unitOfWork.Underwriting.SaveDecision(decision, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.LoanApplications.UpdateApplicationStatus(
                applicationId,
                ResolveApplicationStatus(decision.RecommendedAction),
                "SYSTEM",
                cancellationToken).ConfigureAwait(false);

            await _auditService.Log(
                "UnderwritingDecision",
                decision.DecisionId.ToString(),
                "AutoDecision",
                string.Empty,
                $"Rule={outcome.RuleName};Action={decision.RecommendedAction};Score={latestReport.Score};FOIR={foir:P2};RiskTier={outcome.RiskTier};ApprovedAmount={decision.ApprovedAmount};Interest={decision.InterestRate}",
                "SYSTEM",
                "127.0.0.1",
                cancellationToken).ConfigureAwait(false);

            await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return decision;
        }

        public async Task SubmitManualDecision(ManualDecisionDto decisionDto, long underwriterId, CancellationToken cancellationToken = default)
        {
            await ValidateAndThrowAsync(_manualDecisionValidator, decisionDto, cancellationToken).ConfigureAwait(false);

            if (underwriterId <= 0)
            {
                throw new ValidationException("underwriterId must be greater than zero.");
            }

            var decision = new UnderwritingDecision
            {
                ApplicationId = decisionDto.ApplicationId,
                RecommendedAction = decisionDto.RecommendedAction,
                ApprovedAmount = decisionDto.ApprovedAmount,
                InterestRate = decisionDto.InterestRate,
                Tenure = decisionDto.Tenure,
                RiskScore = decisionDto.RiskScore,
                Remarks = decisionDto.Remarks,
                DecidedBy = underwriterId.ToString(),
                DecidedAt = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            await _unitOfWork.Underwriting.SaveDecision(decision, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.LoanApplications.UpdateApplicationStatus(
                decision.ApplicationId,
                decision.RecommendedAction == RecommendedAction.Reject ? LoanApplicationStatus.Rejected : LoanApplicationStatus.Approved,
                underwriterId.ToString(),
                cancellationToken).ConfigureAwait(false);

            await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<LoanApplication>> GetDecisionQueue(long underwriterId, CancellationToken cancellationToken = default)
        {
            if (underwriterId <= 0)
            {
                throw new ValidationException("underwriterId must be greater than zero.");
            }

            var filtered = await _unitOfWork.LoanApplications.GetPagedApplications(
                new DAL.Models.LoanApplicationFilters { Status = LoanApplicationStatus.InReview },
                1,
                500,
                cancellationToken).ConfigureAwait(false);

            return filtered.Items
                .Where(x => x.AssignedUnderwriterId == underwriterId)
                .ToList();
        }

        private static async Task ValidateAndThrowAsync<T>(IValidator<T> validator, T model, CancellationToken cancellationToken)
        {
            ValidationResult result = await validator.ValidateAsync(model, cancellationToken).ConfigureAwait(false);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }

        private static LoanApplicationStatus ResolveApplicationStatus(RecommendedAction action)
        {
            if (action == RecommendedAction.Reject)
            {
                return LoanApplicationStatus.Rejected;
            }

            if (action == RecommendedAction.Review)
            {
                return LoanApplicationStatus.InReview;
            }

            return LoanApplicationStatus.Approved;
        }

        private static decimal ResolveInterestRate(CommonRiskTier tier)
        {
            if (tier == CommonRiskTier.Prime)
            {
                return 8.5m;
            }

            if (tier == CommonRiskTier.NearPrime)
            {
                return 11m;
            }

            if (tier == CommonRiskTier.Subprime)
            {
                return 14.5m;
            }

            return 14.5m;
        }

        private static LoanPurpose ParseLoanPurpose(string purpose)
        {
            if (Enum.TryParse<LoanPurpose>(purpose, true, out var parsed))
            {
                return parsed;
            }

            return LoanPurpose.PersonalLoan;
        }

        private static decimal CalculateProposedEmi(decimal principal, int tenureMonths, decimal annualRate)
        {
            if (principal <= 0 || tenureMonths <= 0)
            {
                return 0m;
            }

            if (annualRate <= 0)
            {
                return Math.Round(principal / tenureMonths, 2, MidpointRounding.AwayFromZero);
            }

            var monthlyRate = (double)(annualRate / 1200m);
            var factor = Math.Pow(1 + monthlyRate, tenureMonths);
            var emi = (double)principal * monthlyRate * factor / (factor - 1);
            return Math.Round((decimal)emi, 2, MidpointRounding.AwayFromZero);
        }

        private static UnderwritingRule BuildRuleChain()
        {
            return new HardRejectCreditOrDefaultRule()
                .SetNext(new HardRejectFoirRule())
                .SetNext(new ManualReviewRule())
                .SetNext(new ConditionalApproveRule())
                .SetNext(new FullApproveRule())
                .SetNext(new FallbackReviewRule());
        }

        private sealed class UnderwritingContext
        {
            public UnderwritingContext(
                LoanApplication application,
                CreditBureauReport report,
                CommonRiskTier riskTier,
                LoanPurpose loanPurpose,
                decimal foir,
                decimal existingEmi,
                decimal proposedEmi)
            {
                Application = application;
                Report = report;
                RiskTier = riskTier;
                LoanPurpose = loanPurpose;
                Foir = foir;
                ExistingEmi = existingEmi;
                ProposedEmi = proposedEmi;
                ActiveDefaults = report.DefaultHistory ? 1 : 0;
            }

            public LoanApplication Application { get; }
            public CreditBureauReport Report { get; }
            public CommonRiskTier RiskTier { get; }
            public LoanPurpose LoanPurpose { get; }
            public decimal Foir { get; }
            public decimal ExistingEmi { get; }
            public decimal ProposedEmi { get; }
            public int ActiveDefaults { get; }
        }

        private sealed class RuleResult
        {
            public RuleResult(string ruleName, RecommendedAction action, CommonRiskTier riskTier, decimal approvalFactor, string reason, LoanPurpose loanPurpose)
            {
                RuleName = ruleName;
                Action = action;
                RiskTier = riskTier;
                ApprovalFactor = approvalFactor;
                Reason = reason;
                LoanPurpose = loanPurpose;
            }

            public string RuleName { get; }
            public RecommendedAction Action { get; }
            public CommonRiskTier RiskTier { get; }
            public decimal ApprovalFactor { get; }
            public string Reason { get; }
            public LoanPurpose LoanPurpose { get; }
        }

        private abstract class UnderwritingRule
        {
            private UnderwritingRule _next;

            public UnderwritingRule SetNext(UnderwritingRule next)
            {
                _next = next;
                return next;
            }

            public RuleResult Evaluate(UnderwritingContext context)
            {
                if (TryEvaluate(context, out var result))
                {
                    return result;
                }

                return _next?.Evaluate(context)
                       ?? new RuleResult("Fallback", RecommendedAction.Review, context.RiskTier, 0m, "No matching rule. Sent for manual review.", context.LoanPurpose);
            }

            protected abstract bool TryEvaluate(UnderwritingContext context, out RuleResult result);
        }

        private sealed class HardRejectCreditOrDefaultRule : UnderwritingRule
        {
            protected override bool TryEvaluate(UnderwritingContext context, out RuleResult result)
            {
                if (context.Report.Score < 600 || context.ActiveDefaults > 0)
                {
                    result = new RuleResult(
                        "Rule1-HardRejectCreditOrDefault",
                        RecommendedAction.Reject,
                        CommonRiskTier.HighRisk,
                        0m,
                        "Hard reject: score < 600 or active defaults present.",
                        context.LoanPurpose);
                    return true;
                }

                result = null;
                return false;
            }
        }

        private sealed class HardRejectFoirRule : UnderwritingRule
        {
            protected override bool TryEvaluate(UnderwritingContext context, out RuleResult result)
            {
                if (context.Foir > 0.65m)
                {
                    result = new RuleResult(
                        "Rule2-HardRejectFoir",
                        RecommendedAction.Reject,
                        context.RiskTier,
                        0m,
                        "Hard reject: FOIR exceeds 65% post proposed EMI.",
                        context.LoanPurpose);
                    return true;
                }

                result = null;
                return false;
            }
        }

        private sealed class ManualReviewRule : UnderwritingRule
        {
            protected override bool TryEvaluate(UnderwritingContext context, out RuleResult result)
            {
                bool scoreBand = context.Report.Score >= 600 && context.Report.Score <= 650;
                bool foirBand = context.Foir >= 0.55m && context.Foir <= 0.65m;

                if (scoreBand || foirBand)
                {
                    result = new RuleResult(
                        "Rule3-ManualReview",
                        RecommendedAction.Review,
                        context.RiskTier,
                        0m,
                        "Manual review: borderline score or FOIR in 55%-65% band.",
                        context.LoanPurpose);
                    return true;
                }

                result = null;
                return false;
            }
        }

        private sealed class ConditionalApproveRule : UnderwritingRule
        {
            protected override bool TryEvaluate(UnderwritingContext context, out RuleResult result)
            {
                if (context.Report.Score > 650 && context.Report.Score <= 700)
                {
                    result = new RuleResult(
                        "Rule4-ConditionalApprove",
                        RecommendedAction.ConditionalApprove,
                        context.RiskTier,
                        0.80m,
                        "Conditional approve: score in 651-700 range at 80% requested amount.",
                        context.LoanPurpose);
                    return true;
                }

                result = null;
                return false;
            }
        }

        private sealed class FullApproveRule : UnderwritingRule
        {
            protected override bool TryEvaluate(UnderwritingContext context, out RuleResult result)
            {
                if (context.Report.Score > 700 && context.Foir < 0.50m)
                {
                    result = new RuleResult(
                        "Rule5-FullApprove",
                        RecommendedAction.Approve,
                        context.RiskTier,
                        1.0m,
                        "Full approve: score > 700 and FOIR < 50%.",
                        context.LoanPurpose);
                    return true;
                }

                result = null;
                return false;
            }
        }

        private sealed class FallbackReviewRule : UnderwritingRule
        {
            protected override bool TryEvaluate(UnderwritingContext context, out RuleResult result)
            {
                result = new RuleResult(
                    "Fallback-ManualReview",
                    RecommendedAction.Review,
                    context.RiskTier,
                    0m,
                    "No explicit rule matched. Routed to underwriter.",
                    context.LoanPurpose);
                return true;
            }
        }
    }
}
