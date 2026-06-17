using System;

namespace LOCDS.Entities
{
    public class UnderwritingDecision : IEntity, IAuditable
    {
        public long DecisionId { get; set; }
        public long ApplicationId { get; set; }
        public RecommendedAction RecommendedAction { get; set; }
        public decimal ApprovedAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int Tenure { get; set; }
        public decimal RiskScore { get; set; }
        public string Remarks { get; set; } = string.Empty;
        public string DecidedBy { get; set; } = string.Empty;
        public DateTime DecidedAt { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public long Id
        {
            get => DecisionId;
            set => DecisionId = value;
        }
    }
}
