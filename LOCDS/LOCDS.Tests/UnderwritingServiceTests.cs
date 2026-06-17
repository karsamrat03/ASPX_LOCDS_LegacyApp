using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using LOCDS.BLL;
using LOCDS.BLL.DTOs;
using LOCDS.Common.Enums;
using LOCDS.DAL.Abstractions;
using LOCDS.DAL.Models;
using LOCDS.Entities;
using Moq;
using NUnit.Framework;

namespace LOCDS.Tests;

public class UnderwritingServiceTests
{
    [Test]
    public async Task Test_ChainOfResponsibility_HardReject_StopsChain()
    {
        var service = CreateService(
            out var unitOfWork,
            out var loanRepository,
            out _,
            out var underwritingRepository,
            out var scoringService,
            out _,
            out var auditService,
            out var savedDecision);

        long applicationId = 9001;
        loanRepository
            .Setup(x => x.GetApplicationWithFullDetail(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildDetail(
                applicationId,
                score: 580,
                hasDefault: false,
                loanAmount: 1_000_000m,
                tenureMonths: 120,
                annualIncome: 120_000m,
                purpose: "HomeLoan"));

        scoringService
            .Setup(x => x.CalculateFoir(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns(0.90m);
        scoringService
            .Setup(x => x.CalculateRiskTier(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns(RiskTier.HighRisk);
        scoringService
            .Setup(x => x.GetLtvLimit(It.IsAny<LoanPurpose>(), It.IsAny<RiskTier>()))
            .Returns(0.50m);

        underwritingRepository
            .Setup(x => x.SaveDecision(It.IsAny<UnderwritingDecision>(), It.IsAny<CancellationToken>()))
            .Callback<UnderwritingDecision, CancellationToken>((decision, _) => savedDecision = decision)
            .ReturnsAsync(7001);

        loanRepository
            .Setup(x => x.UpdateApplicationStatus(
                applicationId,
                LoanApplicationStatus.Rejected,
                "SYSTEM",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        unitOfWork.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UnderwritingDecision decision = await service.RunAutoDecision(applicationId, CancellationToken.None);

        Assert.That(decision.RecommendedAction, Is.EqualTo(RecommendedAction.Reject));
        Assert.That(decision.ApprovedAmount, Is.EqualTo(0m));
        Assert.That(decision.Remarks, Is.EqualTo("Hard reject: score < 600 or active defaults present."));
        Assert.That(savedDecision, Is.Not.Null);

        loanRepository.Verify(x => x.UpdateApplicationStatus(
            applicationId,
            LoanApplicationStatus.Rejected,
            "SYSTEM",
            It.IsAny<CancellationToken>()), Times.Once);

        auditService.Verify(x => x.Log(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Test_ConditionalApprove_Returns80PercentAmount()
    {
        var service = CreateService(
            out var unitOfWork,
            out var loanRepository,
            out _,
            out var underwritingRepository,
            out var scoringService,
            out _,
            out _,
            out _);

        long applicationId = 9002;
        loanRepository
            .Setup(x => x.GetApplicationWithFullDetail(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildDetail(
                applicationId,
                score: 680,
                hasDefault: false,
                loanAmount: 1_000_000m,
                tenureMonths: 120,
                annualIncome: 2_400_000m,
                purpose: "HomeLoan"));

        scoringService
            .Setup(x => x.CalculateFoir(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns(0.40m);
        scoringService
            .Setup(x => x.CalculateRiskTier(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns(RiskTier.Subprime);
        scoringService
            .Setup(x => x.GetLtvLimit(LoanPurpose.HomeLoan, RiskTier.Subprime))
            .Returns(0.80m);

        underwritingRepository
            .Setup(x => x.SaveDecision(It.IsAny<UnderwritingDecision>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7002);

        loanRepository
            .Setup(x => x.UpdateApplicationStatus(
                applicationId,
                LoanApplicationStatus.Approved,
                "SYSTEM",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        unitOfWork.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UnderwritingDecision decision = await service.RunAutoDecision(applicationId, CancellationToken.None);

        Assert.That(decision.RecommendedAction, Is.EqualTo(RecommendedAction.ConditionalApprove));
        Assert.That(decision.ApprovedAmount, Is.EqualTo(800_000m));
    }

    private static UnderwritingService CreateService(
        out Mock<IUnitOfWork> unitOfWork,
        out Mock<ILoanApplicationRepository> loanRepository,
        out Mock<ICreditBureauRepository> creditBureauRepository,
        out Mock<IUnderwritingRepository> underwritingRepository,
        out Mock<ICreditScoringService> scoringService,
        out Mock<IValidator<ManualDecisionDto>> validator,
        out Mock<IAuditService> auditService,
        out UnderwritingDecision? savedDecision)
    {
        savedDecision = null;

        loanRepository = new Mock<ILoanApplicationRepository>(MockBehavior.Strict);
        creditBureauRepository = new Mock<ICreditBureauRepository>(MockBehavior.Strict);
        underwritingRepository = new Mock<IUnderwritingRepository>(MockBehavior.Strict);
        scoringService = new Mock<ICreditScoringService>(MockBehavior.Strict);
        validator = new Mock<IValidator<ManualDecisionDto>>(MockBehavior.Strict);
        auditService = new Mock<IAuditService>(MockBehavior.Strict);

        auditService
            .Setup(x => x.Log(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        unitOfWork.SetupGet(x => x.LoanApplications).Returns(loanRepository.Object);
        unitOfWork.SetupGet(x => x.CreditBureaus).Returns(creditBureauRepository.Object);
        unitOfWork.SetupGet(x => x.Underwriting).Returns(underwritingRepository.Object);

        return new UnderwritingService(
            unitOfWork.Object,
            scoringService.Object,
            validator.Object,
            auditService.Object);
    }

    private static LoanApplicationDetail BuildDetail(
        long applicationId,
        int score,
        bool hasDefault,
        decimal loanAmount,
        int tenureMonths,
        decimal annualIncome,
        string purpose)
    {
        return new LoanApplicationDetail
        {
            LoanApplication = new LoanApplication
            {
                ApplicationId = applicationId,
                ApplicantId = 101,
                LoanAmount = loanAmount,
                Tenure = tenureMonths,
                Purpose = purpose,
                Status = LoanApplicationStatus.InReview,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            },
            Applicant = new Applicant
            {
                ApplicantId = 101,
                FirstName = "Test",
                LastName = "Applicant",
                AnnualIncome = annualIncome,
                PAN = "ABCDE1234F",
                EmploymentType = Entities.EmploymentType.Salaried,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            },
            BureauReports = new List<CreditBureauReport>
            {
                new CreditBureauReport
                {
                    ReportId = 501,
                    ApplicationId = applicationId,
                    Score = score,
                    DefaultHistory = hasDefault,
                    ActiveLoans = 1,
                    PulledAt = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                }
            }
        };
    }
}
