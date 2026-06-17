using FluentValidation;
using LOCDS.BLL.DTOs;

namespace LOCDS.BLL.Validators
{
    public class ManualDecisionDtoValidator : AbstractValidator<ManualDecisionDto>
    {
        public ManualDecisionDtoValidator()
        {
            RuleFor(x => x.ApplicationId).GreaterThan(0);
            RuleFor(x => x.ApprovedAmount).GreaterThanOrEqualTo(0);
            RuleFor(x => x.InterestRate).InclusiveBetween(0, 100);
            RuleFor(x => x.Tenure).InclusiveBetween(1, 480);
            RuleFor(x => x.RiskScore).InclusiveBetween(0, 1000);
            RuleFor(x => x.Remarks).MaximumLength(1000);
        }
    }
}
