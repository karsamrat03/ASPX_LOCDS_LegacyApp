using System.Threading;
using System.Threading.Tasks;
using LOCDS.Entities;

namespace LOCDS.BLL
{
    public interface ILoanOfferService
    {
        Task<LoanOffer> GenerateOffer(long applicationId, CancellationToken cancellationToken = default);
        decimal CalculateEMI(decimal principal, decimal ratePerAnnum, int tenureMonths);
        Task AcceptOffer(long offerId, long applicantId, CancellationToken cancellationToken = default);
    }
}
