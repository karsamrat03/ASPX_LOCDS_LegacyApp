using System;

namespace LOCDS.Entities
{
    public class CreditApplication
    {
        public Guid ApplicationId { get; set; } = Guid.NewGuid();
        public string ApplicantName { get; set; } = string.Empty;
        public string SocialSecurityNumber { get; set; } = string.Empty;
        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyDebtPayments { get; set; }
        public decimal LoanAmountRequested { get; set; }
    }
}