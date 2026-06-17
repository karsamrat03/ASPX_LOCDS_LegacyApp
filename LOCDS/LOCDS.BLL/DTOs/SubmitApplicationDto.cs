namespace LOCDS.BLL.DTOs
{
    public class SubmitApplicationDto
    {
        public long ApplicantId { get; set; }
        public decimal LoanAmount { get; set; }
        public int Tenure { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public long? AssignedUnderwriterId { get; set; }
    }
}
