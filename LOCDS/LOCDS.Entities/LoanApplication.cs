using System;

namespace LOCDS.Entities
{
    public class LoanApplication : IEntity, IAuditable
    {
        public long ApplicationId { get; set; }
        public long ApplicantId { get; set; }
        public decimal LoanAmount { get; set; }
        public int Tenure { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public LoanApplicationStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public long? AssignedUnderwriterId { get; set; }

        public long Id
        {
            get => ApplicationId;
            set => ApplicationId = value;
        }
    }
}
