using System;

namespace LOCDS.Entities
{
    public class LoanOffer : IEntity, IAuditable
    {
        public long OfferId { get; set; }
        public long ApplicationId { get; set; }
        public decimal EMI { get; set; }
        public decimal ProcessingFee { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime ValidUntil { get; set; }
        public bool IsAccepted { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public long Id
        {
            get => OfferId;
            set => OfferId = value;
        }
    }
}
