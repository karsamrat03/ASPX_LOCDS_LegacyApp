using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LOCDS.DAL.Abstractions;
using LOCDS.Entities;

namespace LOCDS.BLL
{
    public class LoanOfferService : ILoanOfferService
    {
        private readonly IUnitOfWork _unitOfWork;
        private static readonly ConcurrentDictionary<long, LoanOffer> OfferStore = new ConcurrentDictionary<long, LoanOffer>();
        private static long _offerSequence;

        public LoanOfferService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<LoanOffer> GenerateOffer(long applicationId, CancellationToken cancellationToken = default)
        {
            var detail = await _unitOfWork.LoanApplications.GetApplicationWithFullDetail(applicationId, cancellationToken).ConfigureAwait(false);
            if (detail == null)
            {
                throw new InvalidOperationException("Application not found.");
            }

            var principal = detail.LoanApplication.LoanAmount;
            var tenure = detail.LoanApplication.Tenure;
            var annualRate = 11.5m;
            var emi = CalculateEMI(principal, annualRate, tenure);

            var offer = new LoanOffer
            {
                OfferId = Interlocked.Increment(ref _offerSequence),
                ApplicationId = applicationId,
                EMI = emi,
                ProcessingFee = Math.Round(principal * 0.01m, 2),
                TotalCost = Math.Round((emi * tenure) + (principal * 0.01m), 2),
                ValidUntil = DateTime.UtcNow.AddDays(15),
                IsAccepted = false,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            OfferStore[offer.OfferId] = offer;
            return offer;
        }

        public decimal CalculateEMI(decimal principal, decimal ratePerAnnum, int tenureMonths)
        {
            if (principal <= 0)
            {
                throw new ArgumentException("principal must be greater than zero.", nameof(principal));
            }

            if (ratePerAnnum < 0)
            {
                throw new ArgumentException("ratePerAnnum cannot be negative.", nameof(ratePerAnnum));
            }

            if (tenureMonths <= 0)
            {
                throw new ArgumentException("tenureMonths must be greater than zero.", nameof(tenureMonths));
            }

            if (ratePerAnnum == 0)
            {
                return Math.Round(principal / tenureMonths, 2);
            }

            var monthlyRate = (double)(ratePerAnnum / 1200m);
            var factor = Math.Pow(1 + monthlyRate, tenureMonths);
            var emi = (double)principal * monthlyRate * factor / (factor - 1);
            return Math.Round((decimal)emi, 2);
        }

        public async Task AcceptOffer(long offerId, long applicantId, CancellationToken cancellationToken = default)
        {
            if (!OfferStore.TryGetValue(offerId, out var offer))
            {
                throw new InvalidOperationException("Offer not found.");
            }

            var detail = await _unitOfWork.LoanApplications.GetApplicationWithFullDetail(offer.ApplicationId, cancellationToken).ConfigureAwait(false);
            if (detail == null || detail.LoanApplication.ApplicantId != applicantId)
            {
                throw new InvalidOperationException("Applicant is not authorized for this offer.");
            }

            offer.IsAccepted = true;
            offer.LastModifiedDate = DateTime.UtcNow;
            OfferStore[offerId] = offer;

            await _unitOfWork.LoanApplications.UpdateApplicationStatus(
                offer.ApplicationId,
                LoanApplicationStatus.Approved,
                applicantId.ToString(),
                cancellationToken).ConfigureAwait(false);

            await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
