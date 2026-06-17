namespace LOCDS.Entities
{
    public enum RiskBand
    {
        Low,
        Medium,
        High
    }

    public class CreditDecision
    {
        public bool IsApproved { get; set; }
        public decimal DebtToIncomeRatio { get; set; }
        public RiskBand RiskBand { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}