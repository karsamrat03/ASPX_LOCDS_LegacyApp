using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using LOCDS.DAL.Abstractions;
using LOCDS.DAL.Connection;
using LOCDS.DAL.Models;
using LOCDS.Entities;
using System.Data;

namespace LOCDS.DAL.Repositories
{
    public class LoanApplicationRepository : BaseRepository, ILoanApplicationRepository
    {
        public LoanApplicationRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
        {
        }

        public LoanApplicationRepository(IDbConnection connection, IDbTransaction transaction) : base(connection, transaction)
        {
        }

        public async Task<LoanApplication?> GetById(long id, CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT ApplicationId, ApplicantId, LoanAmount, Tenure, Purpose, Status, CreatedDate, LastModifiedDate, AssignedUnderwriterId
FROM locds.LoanApplication
WHERE ApplicationId = @ApplicationId;";

            return await ExecuteWithRetryAsync(conn => conn.QueryFirstOrDefaultAsync<LoanApplication>(
                CreateCommandDefinition(sql, new { ApplicationId = id }, cancellationToken))).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<LoanApplication>> GetAll(CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT ApplicationId, ApplicantId, LoanAmount, Tenure, Purpose, Status, CreatedDate, LastModifiedDate, AssignedUnderwriterId
FROM locds.LoanApplication
ORDER BY CreatedDate DESC;";

            var rows = await ExecuteWithRetryAsync(conn => conn.QueryAsync<LoanApplication>(
                CreateCommandDefinition(sql, cancellationToken: cancellationToken))).ConfigureAwait(false);
            return rows.ToList();
        }

        public async Task<long> Add(LoanApplication entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            const string sql = @"
INSERT INTO locds.LoanApplication
(ApplicantId, LoanAmount, Tenure, Purpose, Status, CreatedDate, LastModifiedDate, AssignedUnderwriterId)
VALUES
(@ApplicantId, @LoanAmount, @Tenure, @Purpose, @Status, @CreatedDate, @LastModifiedDate, @AssignedUnderwriterId);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            return await ExecuteWithRetryAsync(conn => conn.ExecuteScalarAsync<long>(
                CreateCommandDefinition(sql, entity, cancellationToken))).ConfigureAwait(false);
        }

        public async Task<bool> Update(LoanApplication entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            const string sql = @"
UPDATE locds.LoanApplication
SET ApplicantId = @ApplicantId,
    LoanAmount = @LoanAmount,
    Tenure = @Tenure,
    Purpose = @Purpose,
    Status = @Status,
    LastModifiedDate = @LastModifiedDate,
    AssignedUnderwriterId = @AssignedUnderwriterId
WHERE ApplicationId = @ApplicationId;";

            var affected = await ExecuteWithRetryAsync(conn => conn.ExecuteAsync(
                CreateCommandDefinition(sql, entity, cancellationToken))).ConfigureAwait(false);
            return affected > 0;
        }

        public async Task<bool> Delete(long id, CancellationToken cancellationToken = default)
        {
            const string sql = @"
DELETE FROM locds.LoanApplication
WHERE ApplicationId = @ApplicationId;";

            var affected = await ExecuteWithRetryAsync(conn => conn.ExecuteAsync(
                CreateCommandDefinition(sql, new { ApplicationId = id }, cancellationToken))).ConfigureAwait(false);
            return affected > 0;
        }

        public async Task<PagedResult<LoanApplication>> GetPagedApplications(LoanApplicationFilters filters, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var effectiveFilters = filters ?? new LoanApplicationFilters();
            var offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);

            const string sql = @"
SELECT ApplicationId, ApplicantId, LoanAmount, Tenure, Purpose, Status, CreatedDate, LastModifiedDate, AssignedUnderwriterId
FROM locds.LoanApplication
WHERE (@ApplicantId IS NULL OR ApplicantId = @ApplicantId)
  AND (@Status IS NULL OR Status = @Status)
  AND (@FromCreatedDate IS NULL OR CreatedDate >= @FromCreatedDate)
  AND (@ToCreatedDate IS NULL OR CreatedDate <= @ToCreatedDate)
  AND (@SearchText IS NULL OR Purpose LIKE '%' + @SearchText + '%')
ORDER BY CreatedDate DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM locds.LoanApplication
WHERE (@ApplicantId IS NULL OR ApplicantId = @ApplicantId)
  AND (@Status IS NULL OR Status = @Status)
  AND (@FromCreatedDate IS NULL OR CreatedDate >= @FromCreatedDate)
  AND (@ToCreatedDate IS NULL OR CreatedDate <= @ToCreatedDate)
  AND (@SearchText IS NULL OR Purpose LIKE '%' + @SearchText + '%');";

            return await ExecuteWithRetryAsync(async conn =>
            {
                using var multi = await conn.QueryMultipleAsync(CreateCommandDefinition(sql, new
                {
                    effectiveFilters.ApplicantId,
                    Status = effectiveFilters.Status,
                    effectiveFilters.FromCreatedDate,
                    effectiveFilters.ToCreatedDate,
                    effectiveFilters.SearchText,
                    Offset = offset,
                    PageSize = pageSize
                }, cancellationToken)).ConfigureAwait(false);

                var items = (await multi.ReadAsync<LoanApplication>().ConfigureAwait(false)).ToList();
                var totalCount = await multi.ReadFirstAsync<int>().ConfigureAwait(false);

                return new PagedResult<LoanApplication>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    Items = items
                };
            }).ConfigureAwait(false);
        }

        public async Task<LoanApplicationDetail?> GetApplicationWithFullDetail(long applicationId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
SELECT ApplicationId, ApplicantId, LoanAmount, Tenure, Purpose, Status, CreatedDate, LastModifiedDate, AssignedUnderwriterId
FROM locds.LoanApplication
WHERE ApplicationId = @ApplicationId;

SELECT ApplicantId, FirstName, LastName, DOB, PAN, AadhaarHash, AnnualIncome, EmploymentType, CreditScore, RiskTier, CreatedDate, LastModifiedDate
FROM locds.Applicant
WHERE ApplicantId = (SELECT ApplicantId FROM locds.LoanApplication WHERE ApplicationId = @ApplicationId);

SELECT ReportId, ApplicationId, Bureau, Score, Enquiries, ActiveLoans, DefaultHistory, PulledAt, CreatedDate, LastModifiedDate
FROM locds.CreditBureauReport
WHERE ApplicationId = @ApplicationId
ORDER BY PulledAt DESC;

SELECT DecisionId, ApplicationId, RecommendedAction, ApprovedAmount, InterestRate, Tenure, RiskScore, Remarks, DecidedBy, DecidedAt, CreatedDate, LastModifiedDate
FROM locds.UnderwritingDecision
WHERE ApplicationId = @ApplicationId
ORDER BY DecidedAt DESC;

SELECT OfferId, ApplicationId, EMI, ProcessingFee, TotalCost, ValidUntil, IsAccepted, CreatedDate, LastModifiedDate
FROM locds.LoanOffer
WHERE ApplicationId = @ApplicationId
ORDER BY CreatedDate DESC;

SELECT LogId, EntityType, EntityId, Action, OldValue, NewValue, PerformedBy, PerformedAt, IPAddress, CreatedDate, LastModifiedDate
FROM locds.AuditLog
WHERE EntityType = 'LoanApplication' AND EntityId = CAST(@ApplicationId AS NVARCHAR(64))
ORDER BY PerformedAt DESC;";

            return await ExecuteWithRetryAsync(async conn =>
            {
                using var multi = await conn.QueryMultipleAsync(CreateCommandDefinition(
                    sql,
                    new { ApplicationId = applicationId },
                    cancellationToken)).ConfigureAwait(false);

                var loanApplication = await multi.ReadFirstOrDefaultAsync<LoanApplication>().ConfigureAwait(false);
                if (loanApplication == null)
                {
                    return null;
                }

                var applicant = await multi.ReadFirstOrDefaultAsync<Applicant>().ConfigureAwait(false);
                var bureauReports = (await multi.ReadAsync<CreditBureauReport>().ConfigureAwait(false)).ToList();
                var decisions = (await multi.ReadAsync<UnderwritingDecision>().ConfigureAwait(false)).ToList();
                var offers = (await multi.ReadAsync<LoanOffer>().ConfigureAwait(false)).ToList();
                var auditTrail = (await multi.ReadAsync<AuditLog>().ConfigureAwait(false)).ToList();

                return new LoanApplicationDetail
                {
                    LoanApplication = loanApplication,
                    Applicant = applicant,
                    BureauReports = bureauReports,
                    UnderwritingDecisions = decisions,
                    LoanOffers = offers,
                    AuditTrail = auditTrail
                };
            }).ConfigureAwait(false);
        }

        public async Task<bool> UpdateApplicationStatus(long applicationId, LoanApplicationStatus status, string modifiedBy, CancellationToken cancellationToken = default)
        {
            const string sql = @"
UPDATE locds.LoanApplication
SET Status = @Status,
    LastModifiedDate = SYSUTCDATETIME()
WHERE ApplicationId = @ApplicationId;";

            var affected = await ExecuteWithRetryAsync(conn => conn.ExecuteAsync(CreateCommandDefinition(
                sql,
                new { ApplicationId = applicationId, Status = status, ModifiedBy = modifiedBy },
                cancellationToken))).ConfigureAwait(false);

            return affected > 0;
        }
    }
}
