using System.Collections.Generic;
using LOCDS.Entities;

namespace LOCDS.DAL.Models
{
    public class LoanApplicationDetail
    {
        public LoanApplication LoanApplication { get; set; } = new LoanApplication();
        public Applicant? Applicant { get; set; }
        public IReadOnlyList<CreditBureauReport> BureauReports { get; set; } = new List<CreditBureauReport>();
        public IReadOnlyList<UnderwritingDecision> UnderwritingDecisions { get; set; } = new List<UnderwritingDecision>();
        public IReadOnlyList<LoanOffer> LoanOffers { get; set; } = new List<LoanOffer>();
        public IReadOnlyList<AuditLog> AuditTrail { get; set; } = new List<AuditLog>();
    }
}
