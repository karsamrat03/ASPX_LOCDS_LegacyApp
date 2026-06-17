namespace LOCDS.Entities
{
    public enum LoanApplicationStatus
    {
        Draft = 0,
        Submitted = 1,
        InReview = 2,
        Approved = 3,
        Rejected = 4,
        Disbursed = 5,
        Closed = 6
    }

    public enum EmploymentType
    {
        Salaried = 0,
        SelfEmployed = 1,
        BusinessOwner = 2,
        Retired = 3,
        Unemployed = 4
    }

    public enum RiskTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
        VeryHigh = 3
    }

    public enum CreditBureauType
    {
        CIBIL = 0,
        Experian = 1,
        Equifax = 2
    }

    public enum RecommendedAction
    {
        Approve = 0,
        ConditionalApprove = 1,
        Review = 2,
        Reject = 3
    }
}
