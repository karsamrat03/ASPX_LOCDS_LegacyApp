using System.Threading;
using System.Threading.Tasks;
using LOCDS.BLL.DTOs;
using LOCDS.DAL.Models;

namespace LOCDS.BLL
{
    public interface ILoanApplicationService
    {
        Task<long> SubmitApplication(SubmitApplicationDto applicantDto, CancellationToken cancellationToken = default);
        Task<PagedResult<LOCDS.Entities.LoanApplication>> GetDashboard(ApplicationDashboardFilterDto filters, CancellationToken cancellationToken = default);
        Task<LoanApplicationDetail?> GetApplicationDetail(long applicationId, CancellationToken cancellationToken = default);
    }
}
