using System;
using System.Threading;
using System.Threading.Tasks;

namespace LOCDS.DAL.Abstractions
{
    public interface IUnitOfWork : IDisposable
    {
        ILoanApplicationRepository LoanApplications { get; }
        ICreditBureauRepository CreditBureaus { get; }
        IUnderwritingRepository Underwriting { get; }

        void Commit();
        Task CommitAsync(CancellationToken cancellationToken = default);
        void Rollback();
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}