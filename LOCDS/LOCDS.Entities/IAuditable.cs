using System;

namespace LOCDS.Entities
{
    public interface IAuditable
    {
        DateTime CreatedDate { get; set; }
        DateTime LastModifiedDate { get; set; }
    }
}
