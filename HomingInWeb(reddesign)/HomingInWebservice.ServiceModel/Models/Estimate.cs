using System;
using System.Collections.Generic;
using ServiceStack.DataAnnotations;
using System.Runtime.Serialization;

namespace HomingInWebservice.ServiceModel.Models
{
    public class Estimate
    {
        [AutoIncrement]
        public ulong Id { get; set; }


        public DateTime CreatedOn { get; set; }

        [Reference]
        public User User { get; set; }
        [IgnoreDataMember]
        public UInt64 UserId { get; set; }

        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string UnitNumber { get; set; }
        public string Notes { get; set; }
        public bool Active { get; set; }
        public string PremiumAgentId { get; set; }
        public bool IsPremiumResponded { get; set; }

        [Ignore]
        public IList<PictureData> Pictures { get; set; }
        [Ignore]
        public IList<EstimateResponse> Responses { get; set; }
        [Ignore]
        public int? ResponsesCount { get; set; }

        [Index(Unique = true), IgnoreDataMember]
        public Guid? AccessToken { get; set; }

        [IgnoreDataMember]
        public string AssociatedEmail { get; set; }

        public Estimate()
        {
            Pictures = new List<PictureData>();
            Responses = new List<EstimateResponse>();
        }
    }
}
