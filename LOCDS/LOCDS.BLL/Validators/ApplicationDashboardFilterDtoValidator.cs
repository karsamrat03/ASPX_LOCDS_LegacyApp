using FluentValidation;
using LOCDS.BLL.DTOs;

namespace LOCDS.BLL.Validators
{
    public class ApplicationDashboardFilterDtoValidator : AbstractValidator<ApplicationDashboardFilterDto>
    {
        public ApplicationDashboardFilterDtoValidator()
        {
            RuleFor(x => x.Page).GreaterThan(0);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
            RuleFor(x => x.ToCreatedDate)
                .GreaterThanOrEqualTo(x => x.FromCreatedDate.Value)
                .When(x => x.FromCreatedDate.HasValue && x.ToCreatedDate.HasValue);
        }
    }
}
