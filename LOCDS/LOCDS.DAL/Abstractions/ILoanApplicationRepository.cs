using System.Threading;
using System.Threading.Tasks;
using LOCDS.DAL.Models;
using LOCDS.Entities;

namespace LOCDS.DAL.Abstractions
{
    public interface ILoanApplicationRepository : IRepository<LoanApplication>
    {
        Task<PagedResult<LoanApplication>> GetPagedApplications(LoanApplicationFilters filters, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<LoanApplicationDetail?> GetApplicationWithFullDetail(long applicationId, CancellationToken cancellationToken = default);
        Task<bool> UpdateApplicationStatus(long applicationId, LoanApplicationStatus status, string modifiedBy, CancellationToken cancellationToken = default);
    }
}
