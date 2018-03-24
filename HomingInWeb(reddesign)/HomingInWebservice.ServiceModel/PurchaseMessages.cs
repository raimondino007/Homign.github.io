using System;
using ServiceStack;
using System.Collections.Generic;
using HomingInWebservice.ServiceModel.Models;
using Braintree;

namespace HomingInWebservice.ServiceModel
{
    [Route("/purchase/checkout")]
    public class PurchaseCheckoutRequest : IReturn<PurchaseCheckoutResponse>
    {
        public string Target { get; set; }
        public string Nonce { get; set; }
    }

    public class PurchaseCheckoutResponse
    {
        public string Error { get; set; }
        public Transaction Transaction { get; set; }
        public Subscription Subscription { get; set; }
    }



    [Route("/purchase/token")]
    public class PurchaseClientTokenRequest : IReturn<PurchaseClientTokenResponse>
    {

    }

    public class PurchaseClientTokenResponse
    {
        public string Token { get; set; }
    }

    // **
    // Braintree
    // This endpoint should take webhook with all subscription events

    [Route("/purchase/subscription")]
    public class BraintreeWebhook : IReturn<BraintreeWebhookResponse>
    {
        public string bt_signature { get; set; }
        public string bt_payload { get; set; }
    }

    public class BraintreeWebhookResponse
    {
        public bool Success { get; set; }
    }



    [Route("/purchase/cancelsubscription")]
    public class BraintreeCancelSubscriptionRequest : IReturn<BraintreeCancelSubscriptionResponse>
    {
    }

    public class BraintreeCancelSubscriptionResponse
    {
    }

}

