namespace LOCDS.Entities
{
    public class BureauReport
    {
        public int CreditScore { get; set; }
        public int LatePaymentsLast12Months { get; set; }
        public bool HasRecentBankruptcy { get; set; }
    }
}