using System;
using ServiceStack;
using System.Collections.Generic;
using HomingInWebservice.ServiceModel.Models;

namespace HomingInWebservice.ServiceModel
{
    [Route("/user/{UserId}")]
    public class UpdateUserRequest : IReturn<UserResponse>
    {
        
        public UInt64 UserId { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Company { get; set; }
        public string AgentLicense { get; set; }
        public string PhoneNumber { get; set; }
        public bool CanReceiveEmail { get; set; }
        public IEnumerable<string> PostalCodes { get; set; }
        public PremiumInfo PremiumInfo { get; set; }
    }

    [Route("/user/{UserId}/picture")]
    public class AttachImageToUserRequest : IReturn<AttachImageResponse>
    {
        public UInt64 UserId { get; set; }
        public PictureType PictureType { get; set; }
        public string Base64Picture { get; set; }
    }

    [Route("/user/{UserId}/push_device")]
    public class RegisterPushDeviceRequest : IReturn<RegisterPushDeviceResponse>
    {
        public UInt64 UserId { get; set; }
        public string DeviceId { get; set; }
        public DeviceType DeviceType { get; set; }
    }

    [Route("/user/{UserId}/postal_code")]
    public class PostalCodeChangeRequest : IReturn<PostalCodeChangeResponse>
    {
        public UInt64 UserId { get; set; }
        public string PostalCode { get; set; }
    }

    [Route("/user/{UserId}/premium_agent_id")]
    public class PremiumAgentIdChangeRequest : IReturn<PremiumAgentIdChangeResponse>
    {
        public UInt64 UserId { get; set; }
        public string PremiumAgentId { get; set; }
    }

    [Route("/user/{UserId}/set_premium")]
    public class UserSetPremiumRequest : IReturn<UserSetPremiumResponse>
    {
        public UInt64 UserId { get; set; }
    }

    public class RegisterPushDeviceResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
        public bool Success { get; set; }
    }

    public class PostalCodeChangeResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
        public IEnumerable<string> PostalCodes { get; set; }
    }

    public class PremiumAgentIdChangeResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
    }

    public class UserSetPremiumResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
    }
}

