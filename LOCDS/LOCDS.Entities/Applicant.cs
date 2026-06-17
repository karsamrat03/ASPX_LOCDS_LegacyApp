using System;

namespace LOCDS.Entities
{
    public class Applicant : IEntity, IAuditable
    {
        public long ApplicantId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime DOB { get; set; }
        public string PAN { get; set; } = string.Empty;
        public string AadhaarHash { get; set; } = string.Empty;
        public decimal AnnualIncome { get; set; }
        public EmploymentType EmploymentType { get; set; }
        public int CreditScore { get; set; }
        public RiskTier RiskTier { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public long Id
        {
            get => ApplicantId;
            set => ApplicantId = value;
        }
    }
}
