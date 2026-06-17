using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LOCDS.BLL;
using LOCDS.DAL.Abstractions;
using LOCDS.DAL.Models;
using LOCDS.Entities;
using Moq;
using NUnit.Framework;

namespace LOCDS.Tests;

public class LoanOfferServiceTests
{
    [Test]
    public void Test_EMI_Calculation_WithKnownValues()
    {
        var service = CreateService(
            out _,
            out _,
            out _,
            out _);

        decimal emi = service.CalculateEMI(100_000m, 12m, 12);

        Assert.That(emi, Is.EqualTo(8884.88m).Within(0.05m));
    }

    [Test]
    public async Task Test_OfferExpiry_After48Hours()
    {
        var service = CreateService(
            out _,
            out var loanRepository,
            out _,
            out _);

        long applicationId = 9100;
        loanRepository
            .Setup(x => x.GetApplicationWithFullDetail(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoanApplicationDetail
            {
                LoanApplication = new LoanApplication
                {
                    ApplicationId = applicationId,
                    ApplicantId = 11,
                    LoanAmount = 500_000m,
                    Tenure = 60,
                    Purpose = "PersonalLoan",
                    Status = LoanApplicationStatus.Approved,
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                },
                BureauReports = new List<CreditBureauReport>()
            });

        LoanOffer offer = await service.GenerateOffer(applicationId, CancellationToken.None);

        // Business requirement: offer should expire in 48 hours.
        TimeSpan validity = offer.ValidUntil - offer.CreatedDate;
        Assert.That(validity.TotalHours, Is.EqualTo(48).Within(0.1));
    }

    private static LoanOfferService CreateService(
        out Mock<IUnitOfWork> unitOfWork,
        out Mock<ILoanApplicationRepository> loanRepository,
        out Mock<ICreditBureauRepository> creditBureauRepository,
        out Mock<IUnderwritingRepository> underwritingRepository)
    {
        loanRepository = new Mock<ILoanApplicationRepository>(MockBehavior.Strict);
        creditBureauRepository = new Mock<ICreditBureauRepository>(MockBehavior.Strict);
        underwritingRepository = new Mock<IUnderwritingRepository>(MockBehavior.Strict);

        unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        unitOfWork.SetupGet(x => x.LoanApplications).Returns(loanRepository.Object);
        unitOfWork.SetupGet(x => x.CreditBureaus).Returns(creditBureauRepository.Object);
        unitOfWork.SetupGet(x => x.Underwriting).Returns(underwritingRepository.Object);

        return new LoanOfferService(unitOfWork.Object);
    }
}
