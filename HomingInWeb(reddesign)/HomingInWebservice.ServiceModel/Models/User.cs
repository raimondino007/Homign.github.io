using System;
using ServiceStack.DataAnnotations;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HomingInWebservice.ServiceModel.Models
{
    public class User
    {
        [AutoIncrement]
        public ulong Id { get; set; }

        [Index(Unique = true)]
        public string Email { get; set; }
        [IgnoreDataMember]
        public string Password { get; set; }
        public bool Active { get; set; }
        public bool IsAgent { get; set; }
        public PictureData Picture { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Company { get; set; }
        public string AgentLicense { get; set; }
        public string PhoneNumber { get; set; }
        [Ignore]
        public IEnumerable<string> PostalCodes { get; set; }
        [Index(Unique = true), IgnoreDataMember]
        public Guid? SessionId { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? LastLogin { get; set; }
        public string AffiliateId { get; set; }
        public string PremiumAgentId { get; set; }
        public DateTime? PremiumExpirationDate { get; set; }
        [Ignore]
        public bool IsPremium { get { return PremiumExpirationDate > DateTime.Now; } set { } }
        public bool IsAdmin { get; set; }
        public bool CanReceiveEmail { get; set; }
        [Ignore]
        public int? ResponsesCount { get; set; }

        [Ignore]
        public PremiumInfo PremiumInfo { get; set; }
        //[Ignore]
        //public string BrainteeSubcriptionId { get; set; }
        [IgnoreDataMember]
        public string BraintreeCustomerId { get; set; }
        [IgnoreDataMember]
        public string BraintreeSubscriptionId { get; set; }
        [Ignore]
        public bool IsHasSubscription { get { return !String.IsNullOrWhiteSpace(BraintreeSubscriptionId); } set { } }
    }
}
