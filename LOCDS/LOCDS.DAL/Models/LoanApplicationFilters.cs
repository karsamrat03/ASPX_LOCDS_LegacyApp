using System;
using LOCDS.Entities;

namespace LOCDS.DAL.Models
{
    public class LoanApplicationFilters
    {
        public long? ApplicantId { get; set; }
        public LoanApplicationStatus? Status { get; set; }
        public DateTime? FromCreatedDate { get; set; }
        public DateTime? ToCreatedDate { get; set; }
        public string? SearchText { get; set; }
    }
}
