using LOCDS.Entities;

namespace LOCDS.BLL.DTOs
{
    public class ManualDecisionDto
    {
        public long ApplicationId { get; set; }
        public RecommendedAction RecommendedAction { get; set; }
        public decimal ApprovedAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int Tenure { get; set; }
        public decimal RiskScore { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }
}
