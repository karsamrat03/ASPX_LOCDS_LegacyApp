using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using LOCDS.DAL.Abstractions;
using LOCDS.DAL.Connection;
using LOCDS.Entities;
using System.Data;

namespace LOCDS.DAL.Repositories
{
    public class CreditBureauRepository : BaseRepository, ICreditBureauRepository
    {
        public CreditBureauRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
        {
        }

        public CreditBureauRepository(IDbConnection connection, IDbTransaction transaction) : base(connection, transaction)
        {
        }

        public async Task<long> SaveBureauReport(CreditBureauReport report, CancellationToken cancellationToken = default)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            const string sql = @"
INSERT INTO locds.CreditBureauReport
(ApplicationId, Bureau, Score, Enquiries, ActiveLoans, DefaultHistory, PulledAt, CreatedDate, LastModifiedDate)
VALUES
(@ApplicationId, @Bureau, @Score, @Enquiries, @ActiveLoans, @DefaultHistory, @PulledAt, @CreatedDate, @LastModifiedDate);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            return await ExecuteWithRetryAsync(conn => conn.ExecuteScalarAsync<long>(
                CreateCommandDefinition(sql, report, cancellationToken))).ConfigureAwait(false);
        }

        public async Task<CreditBureauReport?> GetLatestReport(long applicationId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT TOP 1 ReportId, ApplicationId, Bureau, Score, Enquiries, ActiveLoans, DefaultHistory, PulledAt, CreatedDate, LastModifiedDate
FROM locds.CreditBureauReport
WHERE ApplicationId = @ApplicationId
ORDER BY PulledAt DESC;";

            return await ExecuteWithRetryAsync(conn => conn.QueryFirstOrDefaultAsync<CreditBureauReport>(
                CreateCommandDefinition(sql, new { ApplicationId = applicationId }, cancellationToken))).ConfigureAwait(false);
        }
    }
}
