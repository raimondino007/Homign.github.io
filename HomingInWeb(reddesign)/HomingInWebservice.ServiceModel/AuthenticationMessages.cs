using System;
using System.Collections.Generic;
using ServiceStack;
using HomingInWebservice.ServiceModel.Models;

namespace HomingInWebservice.ServiceModel
{
    [Route("/user/")]
    public class CreateUserRequest : IReturn<NewSessionResponse>
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public bool IsAgent { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Company { get; set; }
        public string AgentLicense { get; set; }
        public string PhoneNumber { get; set; }
        public IEnumerable<string> PostalCodes { get; set; }
        public string AffiliateId { get; set; }
    }

    [Route("/user/{Email}/{DeviceId}")]
    [Route("/user/login")]
    public class AuthenticatedSessionRequest : IReturn<NewSessionResponse>
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string DeviceId { get; set; }
    }


    [Route("/user/anonymous/{DeviceId}")]
    public class AnonymousSessionRequest : IReturn<NewSessionResponse>
    {
        public string DeviceId { get; set; }
    }

    [Route("/user/reset_password")]
    public class GeneratePasswordResetLinkRequest : IReturn<PasswordResponse>
    {
        public string Email { get; set; }
    }

    [Route("/user/{UserId}/complete_password_reset")]
    public class CompletePasswordReset : IReturn<PasswordResponse>
    {
        public UInt64 UserId { get; set; }
        public Guid UniqueId { get; set; }
        public string NewPassword { get; set; }
    }

    [Route("/guid")]
    public class GuidRequest : IReturn<GuidResponse>
    {
    }

    public class NewSessionResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
        public Guid SessionId { get; set; }
        public User User { get; set; }
        public long EstimateRequestCount { get; set; }
    }

    public class UserResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
        public User User { get; set; }
    }

    public class PasswordResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
        public bool Success { get; set; }
    }

    public class GuidResponse
    {
        public string Guid { get; set; }
    }
}