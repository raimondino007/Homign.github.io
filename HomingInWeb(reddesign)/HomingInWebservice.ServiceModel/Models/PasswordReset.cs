using System;
using ServiceStack.DataAnnotations;


namespace HomingInWebservice.ServiceModel.Models
{
    public class PasswordReset
    {
        [AutoIncrement]
        public ulong Id { get; set; }

        [Index(Unique = true)]
        public Guid UniqueId { get; set; }
        
        public UInt64 UserId { get; set; }
        public string RequestingIpAddress { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
