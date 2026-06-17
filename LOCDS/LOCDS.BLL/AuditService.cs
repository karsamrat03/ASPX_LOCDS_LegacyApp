using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using LOCDS.BLL.DTOs;
using log4net;

namespace LOCDS.BLL
{
    public class AuditService : IAuditService
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(AuditService));
        private readonly IValidator<AuditLogRequestDto> _validator;

        public AuditService(IValidator<AuditLogRequestDto> validator)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public async Task Log(string entityType, string entityId, string action, string oldVal, string newVal, string userId, string ip, CancellationToken cancellationToken = default)
        {
            var request = new AuditLogRequestDto
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                OldValue = oldVal,
                NewValue = newVal,
                UserId = userId,
                IPAddress = ip
            };

            ValidationResult result = await _validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }

            Logger.Info($"AUDIT | Entity={entityType} | Id={entityId} | Action={action} | User={userId} | IP={ip} | Old={oldVal} | New={newVal}");
        }
    }
}
