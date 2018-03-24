using System;
using ServiceStack.DataAnnotations;
using System.Runtime.Serialization;

namespace HomingInWebservice.ServiceModel.Models
{
    public class EstimateResponse
    {
        [AutoIncrement]
        public ulong Id { get; set; }
        public Boolean Active { get; set; }

        public DateTime CreatedOn { get; set; }
        [Index]
        public UInt64 EstimateId { get; set; }

        [Reference]
        public User User { get; set; }
        [IgnoreDataMember]
        public UInt64 UserId { get; set; }

        public double EstimateValue { get; set; }
        public string Notes { get; set; }
        public string CloudCMALink { get; set; }
    }
}
