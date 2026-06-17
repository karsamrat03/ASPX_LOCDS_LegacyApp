using System;
using LOCDS.Entities;

namespace LOCDS.BLL.DTOs
{
    public class ApplicationDashboardFilterDto
    {
        public long? ApplicantId { get; set; }
        public LoanApplicationStatus? Status { get; set; }
        public DateTime? FromCreatedDate { get; set; }
        public DateTime? ToCreatedDate { get; set; }
        public string? SearchText { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }
}
