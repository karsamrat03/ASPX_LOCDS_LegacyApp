using System;

namespace LOCDS.Entities
{
    public class CreditBureauReport : IEntity, IAuditable
    {
        public long ReportId { get; set; }
        public long ApplicationId { get; set; }
        public CreditBureauType Bureau { get; set; }
        public int Score { get; set; }
        public int Enquiries { get; set; }
        public int ActiveLoans { get; set; }
        public bool DefaultHistory { get; set; }
        public DateTime PulledAt { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public long Id
        {
            get => ReportId;
            set => ReportId = value;
        }
    }
}
