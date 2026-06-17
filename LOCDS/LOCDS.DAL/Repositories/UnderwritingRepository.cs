using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using LOCDS.DAL.Abstractions;
using LOCDS.DAL.Connection;
using LOCDS.Entities;
using System.Data;

namespace LOCDS.DAL.Repositories
{
    public class UnderwritingRepository : BaseRepository, IUnderwritingRepository
    {
        public UnderwritingRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
        {
        }

        public UnderwritingRepository(IDbConnection connection, IDbTransaction transaction) : base(connection, transaction)
        {
        }

        public async Task<long> SaveDecision(UnderwritingDecision decision, CancellationToken cancellationToken = default)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            const string sql = @"
INSERT INTO locds.UnderwritingDecision
(ApplicationId, RecommendedAction, ApprovedAmount, InterestRate, Tenure, RiskScore, Remarks, DecidedBy, DecidedAt, CreatedDate, LastModifiedDate)
VALUES
(@ApplicationId, @RecommendedAction, @ApprovedAmount, @InterestRate, @Tenure, @RiskScore, @Remarks, @DecidedBy, @DecidedAt, @CreatedDate, @LastModifiedDate);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            return await ExecuteWithRetryAsync(conn => conn.ExecuteScalarAsync<long>(
                CreateCommandDefinition(sql, decision, cancellationToken))).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<UnderwritingDecision>> GetDecisionHistory(long applicationId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT DecisionId, ApplicationId, RecommendedAction, ApprovedAmount, InterestRate, Tenure, RiskScore, Remarks, DecidedBy, DecidedAt, CreatedDate, LastModifiedDate
FROM locds.UnderwritingDecision
WHERE ApplicationId = @ApplicationId
ORDER BY DecidedAt DESC;";

            var rows = await ExecuteWithRetryAsync(conn => conn.QueryAsync<UnderwritingDecision>(
                CreateCommandDefinition(sql, new { ApplicationId = applicationId }, cancellationToken))).ConfigureAwait(false);

            return rows.ToList();
        }
    }
}
