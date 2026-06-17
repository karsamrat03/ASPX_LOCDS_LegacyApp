using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using LOCDS.BLL.DTOs;
using LOCDS.DAL.Abstractions;
using LOCDS.DAL.Models;
using LOCDS.Entities;

namespace LOCDS.BLL
{
    public class LoanApplicationService : ILoanApplicationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IValidator<SubmitApplicationDto> _submitValidator;
        private readonly IValidator<ApplicationDashboardFilterDto> _dashboardValidator;

        public LoanApplicationService(
            IUnitOfWork unitOfWork,
            IValidator<SubmitApplicationDto> submitValidator,
            IValidator<ApplicationDashboardFilterDto> dashboardValidator)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _submitValidator = submitValidator ?? throw new ArgumentNullException(nameof(submitValidator));
            _dashboardValidator = dashboardValidator ?? throw new ArgumentNullException(nameof(dashboardValidator));
        }

        public async Task<long> SubmitApplication(SubmitApplicationDto applicantDto, CancellationToken cancellationToken = default)
        {
            await ValidateAndThrowAsync(_submitValidator, applicantDto, cancellationToken).ConfigureAwait(false);

            var entity = new LoanApplication
            {
                ApplicantId = applicantDto.ApplicantId,
                LoanAmount = applicantDto.LoanAmount,
                Tenure = applicantDto.Tenure,
                Purpose = applicantDto.Purpose,
                Status = LoanApplicationStatus.Submitted,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow,
                AssignedUnderwriterId = applicantDto.AssignedUnderwriterId
            };

            var appId = await _unitOfWork.LoanApplications.Add(entity, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return appId;
        }

        public async Task<PagedResult<LoanApplication>> GetDashboard(ApplicationDashboardFilterDto filters, CancellationToken cancellationToken = default)
        {
            var effectiveFilters = filters ?? new ApplicationDashboardFilterDto();
            await ValidateAndThrowAsync(_dashboardValidator, effectiveFilters, cancellationToken).ConfigureAwait(false);

            var repoFilters = new LoanApplicationFilters
            {
                ApplicantId = effectiveFilters.ApplicantId,
                Status = effectiveFilters.Status,
                FromCreatedDate = effectiveFilters.FromCreatedDate,
                ToCreatedDate = effectiveFilters.ToCreatedDate,
                SearchText = effectiveFilters.SearchText
            };

            return await _unitOfWork.LoanApplications
                .GetPagedApplications(repoFilters, effectiveFilters.Page, effectiveFilters.PageSize, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<LoanApplicationDetail?> GetApplicationDetail(long applicationId, CancellationToken cancellationToken = default)
        {
            if (applicationId <= 0)
            {
                throw new ValidationException("applicationId must be greater than zero.");
            }

            return await _unitOfWork.LoanApplications
                .GetApplicationWithFullDetail(applicationId, cancellationToken)
                .ConfigureAwait(false);
        }

        private static async Task ValidateAndThrowAsync<T>(IValidator<T> validator, T model, CancellationToken cancellationToken)
        {
            ValidationResult result = await validator.ValidateAsync(model, cancellationToken).ConfigureAwait(false);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }
    }
}
