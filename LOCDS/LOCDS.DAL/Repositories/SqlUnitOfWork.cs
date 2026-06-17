using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using LOCDS.DAL.Abstractions;
using LOCDS.DAL.Connection;

namespace LOCDS.DAL.Repositories
{
    public class SqlUnitOfWork : IUnitOfWork
    {
        private readonly SqlConnection _connection;
        private SqlTransaction _transaction;
        private bool _disposed;

        public SqlUnitOfWork(IDbConnectionFactory connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            _connection = (SqlConnection)connectionFactory.CreateConnection();
            _connection.Open();
            _transaction = _connection.BeginTransaction();

            LoanApplications = new LoanApplicationRepository(_connection, _transaction);
            CreditBureaus = new CreditBureauRepository(_connection, _transaction);
            Underwriting = new UnderwritingRepository(_connection, _transaction);
        }

        public ILoanApplicationRepository LoanApplications { get; }
        public ICreditBureauRepository CreditBureaus { get; }
        public IUnderwritingRepository Underwriting { get; }

        public void Commit()
        {
            EnsureNotDisposed();
            _transaction.Commit();
            _transaction.Dispose();
            _transaction = _connection.BeginTransaction();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commit();
            return Task.CompletedTask;
        }

        public void Rollback()
        {
            EnsureNotDisposed();
            _transaction.Rollback();
            _transaction.Dispose();
            _transaction = _connection.BeginTransaction();
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Rollback();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                _transaction.Dispose();
            }
            finally
            {
                _connection.Dispose();
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SqlUnitOfWork));
            }
        }
    }
}