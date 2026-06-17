using FluentValidation;
using LOCDS.BLL.DTOs;

namespace LOCDS.BLL.Validators
{
    public class SubmitApplicationDtoValidator : AbstractValidator<SubmitApplicationDto>
    {
        public SubmitApplicationDtoValidator()
        {
            RuleFor(x => x.ApplicantId).GreaterThan(0);
            RuleFor(x => x.LoanAmount).GreaterThan(0);
            RuleFor(x => x.Tenure).InclusiveBetween(1, 480);
            RuleFor(x => x.Purpose).NotEmpty().MaximumLength(100);
        }
    }
}
