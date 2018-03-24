using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ServiceStack.DataAnnotations;

namespace HomingInWebservice.ServiceModel.Models
{
    public class PremiumInfo
    {
        [AutoIncrement]
        [Index]
        public ulong Id { get; set; }

        public ulong UserId { get; set; }

        public string WorkAddress { get; set; }
        public bool WorkAddressIsPublic { get; set; }

        public string MailingAddress { get; set; }
        public bool MailingAddressIsPublic { get; set; }

        public string MobilePhone { get; set; }
        public bool MobilePhoneIsPublic { get; set; }

        public string OfficePhone { get; set; }
        public bool OfficePhoneIsPublic { get; set; }

        public string Email { get; set; }
        public bool EmailIsPublic { get; set; }

        public string AltEmail { get; set; }
        public bool AltEmailIsPublic { get; set; }

        public string DateOfBirth { get; set; }
        public bool DateOfBirthIsPublic { get; set; }

        public string YearsLicensed { get; set; }
        public bool YearsLicensedIsPublic { get; set; }

        public string Bio { get; set; }
        public bool BioIsPublic { get; set; }

        public string Video { get; set; }
        public bool VideoIsPublic { get; set; }

        public string Website { get; set; }
        public bool WebsiteIsPublic { get; set; }

        public string LinkedIn { get; set; }
        public bool LinkedInIsPublic { get; set; }

        public string YelpReviews { get; set; }
        public bool YelpReviewsIsPublic { get; set; }

        public string ZillowReviews { get; set; }
        public bool ZillowReviewsIsPublic { get; set; }

        public string OtherReviews { get; set; }
        public bool OtherReviewsIsPublic { get; set; }

        public string Facebook { get; set; }
        public bool FacebookIsPublic { get; set; }

        public string Youtube { get; set; }
        public bool YoutubeIsPublic { get; set; }

        public string Twitter { get; set; }
        public bool TwitterIsPublic { get; set; }

        public string Instagram { get; set; }
        public bool InstagramIsPublic { get; set; }

        public string Snapchat { get; set; }
        public bool SnapchatIsPublic { get; set; }
    }
}
