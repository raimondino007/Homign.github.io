using System;
using System.Collections.Generic;
using ServiceStack;
using HomingInWebservice.ServiceModel.Models;

namespace HomingInWebservice.ServiceModel
{
    [Route("/estimate/")]
    public class CreateEstimateRequest : IReturn<CreateEstimateResponse>
    {
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string UnitNumber { get; set; }
        public string Notes { get; set; } //optional
        public int PictureCount { get; set; }
        public string AnonymousEmail { get; set; }
        public string PremiumAgentId { get; set; }
    }

    [Route("/user/{UserId}/estimates")]
    public class EstimatesByUserRequest : IReturn<EstimateListResponse>
    {
        public UInt64 UserId { get; set; }
    }

    [Route("/estimates/")]
    public class RecentEstimatesRequest : IReturn<EstimateListResponse>
    {
        public int Count { get; set; }
        public int Offset { get; set; }
        public IEnumerable<string> PostalCodes { get; set; }
    }

    [Route("/estimates/{EstimateId}")]
    public class EstimateDetailsRequest : IReturn<EstimateDetailsResponse>
    {
        public UInt64 EstimateId { get; set; }
    }

    [Route("/estimates/anonymous/{AccessToken}")]
    public class EstimateFromAccessToken : IReturn<EstimateDetailsResponse>
    {
        public Guid AccessToken { get; set; }
    }

    [Route("/estimates/claim/{AccessToken}")]
    public class ClaimEstimateFromAccessToken : IReturn<EstimateDetailsResponse>
    {
        public Guid AccessToken { get; set; }
    }

    [Route("/estimates/isanonymous/{AccessToken}")]
    public class EstimateFromAccessTokenIsAnonymous : IReturn<EstimateIsAnanymousResponse>
    {
        public Guid AccessToken { get; set; }
    }

    [Route("/estimates/anonymous/{AccessToken}/unsub")]
    public class UnsubFromAccessToken
    {
        public Guid AccessToken { get; set; }
    }

    [Route("/estimates/{EstimateId}/responses")]
    public class RespondToEstimateRequest : IReturn<RespondToEstimateResponse>
    {
        public UInt64 EstimateId { get; set; }
        public double EstimateValue { get; set; }
        public string Notes { get; set; }
        public string ResponseId { get; set; }
        public string CloudCMALink { get; set; }
    }

    [Route("/estimates/{EstimateId}/images")]
    public class AttachImageToEstimateRequest : IReturn<AttachImageResponse>
    {
        public UInt64 EstimateId { get; set; }
        public PictureType PictureType { get; set; }
        public string Base64Picture { get; set; }
    }

    [Route("/estimates/pending/{AgentId}")]
    public class PendingEstimatesForAgentRequest : IReturn<EstimateListResponse>
    {
        public UInt64 AgentId { get; set; }
    }

    public class CreateEstimateResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
        public UInt64 EstimateId { get; set; }
    }

    public class RespondToEstimateResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
        public UInt64 ResponseId { get; set; }
    }

    public class AttachImageResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
        public string PictureURL { get; set; }
    }

    public class EstimateListResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
        public IEnumerable<Estimate> Estimates { get; set; }
    }

    public class EstimateDetailsResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
        public Estimate Estimate { get; set; }
    }

    public class EstimateIsAnanymousResponse
    {
        public ResponseStatus ResponseStatus { get; set; }
        public bool IsAnonymous  { get; set; }
    }
}
