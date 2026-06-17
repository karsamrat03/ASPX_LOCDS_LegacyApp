namespace LOCDS.BLL.DTOs
{
    public class AuditLogRequestDto
    {
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
    }
}
