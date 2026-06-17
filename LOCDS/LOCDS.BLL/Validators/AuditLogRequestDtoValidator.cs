using FluentValidation;
using LOCDS.BLL.DTOs;

namespace LOCDS.BLL.Validators
{
    public class AuditLogRequestDtoValidator : AbstractValidator<AuditLogRequestDto>
    {
        public AuditLogRequestDtoValidator()
        {
            RuleFor(x => x.EntityType).NotEmpty().MaximumLength(100);
            RuleFor(x => x.EntityId).NotEmpty().MaximumLength(64);
            RuleFor(x => x.Action).NotEmpty().MaximumLength(50);
            RuleFor(x => x.UserId).NotEmpty().MaximumLength(100);
            RuleFor(x => x.IPAddress).NotEmpty().MaximumLength(45);
        }
    }
}
