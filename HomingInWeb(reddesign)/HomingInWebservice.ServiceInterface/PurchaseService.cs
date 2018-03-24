using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HomingInWebservice.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Braintree;
using HomingInWebservice.ServiceModel.Models;

namespace HomingInWebservice.ServiceInterface
{

    public class BraintreeConfiguration : IBraintreeConfiguration
    {
        public string Environment { get; set; }
        public string MerchantId { get; set; }
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }
        private IBraintreeGateway BraintreeGateway { get; set; }

        public IBraintreeGateway CreateGateway()
        {
            // PayPal updated their endpoints on 19/20 January 2016 to use TSL 1.2 and HTTP 1.1.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Environment = System.Environment.GetEnvironmentVariable("BraintreeEnvironment");
            MerchantId = System.Environment.GetEnvironmentVariable("BraintreeMerchantId");
            PublicKey = System.Environment.GetEnvironmentVariable("BraintreePublicKey");
            PrivateKey = System.Environment.GetEnvironmentVariable("BraintreePrivateKey");

            if (MerchantId == null || PublicKey == null || PrivateKey == null)
            {
                Environment = GetConfigurationSetting("BraintreeEnvironment");
                MerchantId = GetConfigurationSetting("BraintreeMerchantId");
                PublicKey = GetConfigurationSetting("BraintreePublicKey");
                PrivateKey = GetConfigurationSetting("BraintreePrivateKey");
            }

            return new BraintreeGateway(Environment, MerchantId, PublicKey, PrivateKey);
        }

        public string GetConfigurationSetting(string setting)
        {
            return ConfigurationManager.AppSettings[setting];
        }

        public IBraintreeGateway GetGateway()
        {
            if (BraintreeGateway == null)
            {
                BraintreeGateway = CreateGateway();
            }

            return BraintreeGateway;
        }
    }

    public interface IBraintreeConfiguration
    {
        IBraintreeGateway CreateGateway();
        string GetConfigurationSetting(string setting);
        IBraintreeGateway GetGateway();
    }


    public class PurchaseService : HomingInService
    {
        public IBraintreeConfiguration config = new BraintreeConfiguration();

        public void SetUserPremium(User user, string subscriptionId, DateTime? expDate = null)
        {
            if (user == null)
                throw new UnauthorizedAccessException("'UserId' must be the same as the authenticated user.");


            if (expDate == null)
            {
                expDate = DateTime.Now.AddMonths(1);
            }

            user.PremiumExpirationDate = expDate;
            user.BraintreeSubscriptionId = subscriptionId;

            if (user.IsAgent && user.PremiumAgentId == null)
            {
                Regex rgx = new Regex("@.*");
                user.PremiumAgentId = rgx.Replace(user.Email, "");

                var usersWithSomePremiumId = Db.Select<User>(u => u.PremiumAgentId == user.PremiumAgentId).Count();

                if (usersWithSomePremiumId > 0)
                {
                    user.PremiumAgentId = user.PremiumAgentId + (usersWithSomePremiumId + 1);
                }
            }
            using (var transaction = Db.BeginTransaction())
            {
                Db.Save<User>(user);
                transaction.Commit();
            }
        }

        public void CancelUserPremium(User user)
        {
            if (user == null)
                throw new UnauthorizedAccessException("User is required for Canceling Premium");

            user.PremiumExpirationDate = null;
            user.BraintreeSubscriptionId = null;

            using (var transaction = Db.BeginTransaction())
            {
                Db.Save<User>(user);
                transaction.Commit();
            }
        }

        public void CreateCustomer(User user)
        {
            var gateway = config.GetGateway();
            var request = new CustomerRequest
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.PhoneNumber
            };
            Result<Customer> result = gateway.Customer.Create(request);

            if (result.IsSuccess())
            {
                string customerId = result.Target.Id;
                user.BraintreeCustomerId = customerId;

                using (var transaction = Db.BeginTransaction())
                {
                    Db.Save<User>(user);
                    transaction.Commit();
                }
            }
        }

        public void CancelUserSubscription(User user)
        {
            if (user.BraintreeSubscriptionId != null)
            {
                var gateway = config.GetGateway(); 

                var result = gateway.Subscription.Cancel(user.BraintreeSubscriptionId);
                var subscriptionErrorCodesList = new List<ValidationErrorCode>();


                var subscriptionErrors = result.Errors?.ForObject("Subscription");
                if (subscriptionErrors?.Count > 0)
                {
                    foreach (ValidationError error in subscriptionErrors.All())
                    {
                        subscriptionErrorCodesList.Add(error.Code);
                    }
                }

                if (result.IsSuccess() ||
                    subscriptionErrorCodesList.Contains(ValidationErrorCode.SUBSCRIPTION_STATUS_IS_CANCELED))
                {
                    user.BraintreeSubscriptionId = null;
                    using (var transaction = Db.BeginTransaction())
                    {
                        Db.Save<User>(user);
                        transaction.Commit();
                    }
                }
                else
                {
                    base.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                }
            }
        }

        public async Task<PurchaseCheckoutResponse> Post(PurchaseCheckoutRequest request)
        {
            
            var user = ValidateAndGetCurrentUser();
            if (user == null)
                throw new UnauthorizedAccessException("You must be authenticated.");

            var billingPlanId = string.Empty; //PlanId
            decimal billingAmount = 0;

            switch (request.Target)
            {
                case "premium":
                    billingPlanId = ConfigurationManager.AppSettings["BraintreePremiumPlanId"];
                    break;
                case "elite":
                    billingPlanId = ConfigurationManager.AppSettings["BraintreeElitePlanId"];
                    break;
                default:
                    throw new ArgumentException("Missing parameter 'PurchaseTarget'");
            }
            
            
            var gateway = config.CreateGateway();

            if (user.BraintreeSubscriptionId != null)
            {
                Subscription subscription = gateway.Subscription.Find(user.BraintreeSubscriptionId);
                if (subscription.Status == SubscriptionStatus.ACTIVE && subscription.PlanId == billingPlanId)
                {
                    throw new ArgumentException("You already subscribed on this plan");
                }
            }

            if (!string.IsNullOrWhiteSpace(billingPlanId))
            {
                return CreateBraintreeSubscription(user, billingPlanId, request.Nonce);
            }
            else
            {
                return CreateBraintreeTransaction(user, billingAmount, request.Nonce);
            }
        }

        public PurchaseCheckoutResponse CreateBraintreeSubscription(User user, string planId, string nonce)
        {
            var gateway = config.GetGateway();
            var subscriptionRequest = new SubscriptionRequest()
            {
                PlanId = planId,
                PaymentMethodNonce = nonce
            };

            Result<Subscription> subscriptionResult = gateway.Subscription.Create(subscriptionRequest);

            if (subscriptionResult.IsSuccess())
            {
                var subscription = subscriptionResult.Target;
                SetUserPremium(user, subscription.Id, subscription.BillingPeriodEndDate);
                return new PurchaseCheckoutResponse { Subscription = subscription };
            }

            string errorMessages = "";
            foreach (ValidationError error in subscriptionResult.Errors.DeepAll())
            {
                errorMessages += "Error: " + (int)error.Code + " - " + error.Message + "\n";
            }
            base.Response.StatusCode = (int) HttpStatusCode.BadRequest;
            return new PurchaseCheckoutResponse { Error = errorMessages };
        }

        public PurchaseCheckoutResponse CreateBraintreeTransaction(User user, decimal amount, string nonce)
        {
            var gateway = config.GetGateway();
            var transactionRequest = new TransactionRequest
            {
                Amount = amount,
                PaymentMethodNonce = nonce,
                Options = new TransactionOptionsRequest
                {
                    SubmitForSettlement = true
                }
            };

            Result<Transaction> result = gateway.Transaction.Sale(transactionRequest);

            var validTransactionStatuses = new List<TransactionStatus>
            {
                TransactionStatus.AUTHORIZED,
                TransactionStatus.AUTHORIZING,
                TransactionStatus.SETTLED,
                TransactionStatus.SETTLEMENT_CONFIRMED,
                TransactionStatus.SETTLEMENT_PENDING,
                TransactionStatus.SETTLING,
                TransactionStatus.SUBMITTED_FOR_SETTLEMENT,
            };

            if (result.IsSuccess() || result.Transaction != null)
            {
                var transaction = result.Target;

                if (!result.IsSuccess())
                    transaction = gateway.Transaction.Find(result.Transaction.Id);

                if (transaction != null && validTransactionStatuses.Contains(transaction.Status))
                {
                    //TODO: Check right subscribtion Id!
                    SetUserPremium(user, "");
                    return new PurchaseCheckoutResponse { Transaction = transaction };
                }

            }

            string errorMessages = "";

            foreach (ValidationError error in result.Errors.DeepAll())
            {
                errorMessages += "Error: " + (int)error.Code + " - " + error.Message + "\n";
            }
            return new PurchaseCheckoutResponse { Error = errorMessages };
        }


        public PurchaseClientTokenResponse Get(PurchaseClientTokenRequest request)
        {
            var gateway = config.GetGateway();
            var user = ValidateAndGetCurrentUser();

            try
            {
                if (user.BraintreeCustomerId != null)
                {
                    Customer customer = gateway.Customer.Find(user.BraintreeCustomerId);
                }
                else
                {
                    throw new ArgumentException("Missing BraintreeCustomerId");
                }
            }
            catch (Exception ex)
            {
                CreateCustomer(user);
            }

            var customerId = user.BraintreeCustomerId;

            var clientTokenRequest = new ClientTokenRequest {CustomerId = customerId};
            var clientToken = gateway.ClientToken.generate(clientTokenRequest);

            return new PurchaseClientTokenResponse {Token = clientToken};
        }

        // *
        // Braintree
        public BraintreeWebhookResponse Get(BraintreeWebhook request)
        {
            return new BraintreeWebhookResponse { Success = true};
        }

        public BraintreeWebhookResponse Post(BraintreeWebhook request)
        {
            // https://developers.braintreepayments.com/reference/general/webhooks/subscription/dotnet

            var gateway = config.GetGateway();
            var shouldCancelPremium = new List<WebhookKind>
            {
                WebhookKind.SUBSCRIPTION_WENT_PAST_DUE,
                WebhookKind.SUBSCRIPTION_EXPIRED,
                WebhookKind.SUBSCRIPTION_TRIAL_ENDED
            };

            var shouldCancelSubscription = new List<WebhookKind>
            {
                WebhookKind.SUBSCRIPTION_CANCELED
            };

            var shouldSetPremium = new List<WebhookKind>
            {
                WebhookKind.SUBSCRIPTION_CHARGED_SUCCESSFULLY
            };

            var webhookNotification = gateway.WebhookNotification.Parse(
                request.bt_signature,
                request.bt_payload
            );

            // Log?
            // Timestamp: DateTime.Now()
            // Webhook Signature: request.bt_signature
            // Webhook Payload: request.bt_payload
            // Kind: webhookNotification.Kind
            // SubscriptionId: webhookNotification.Subscription.Id


            if (webhookNotification.Subscription != null)
            {
                var subscription = webhookNotification.Subscription;
                var user = Db.Select<User>(u => u.BraintreeSubscriptionId == subscription.Id).FirstOrDefault();

                if (user != null)
                {
                    if (shouldCancelPremium.Contains(webhookNotification.Kind))
                    {
                        CancelUserPremium(user);
                    }
                    else if (shouldSetPremium.Contains(webhookNotification.Kind))
                    {
                        SetUserPremium(user, subscription.Id, subscription.BillingPeriodEndDate);
                    }
                    else if (shouldCancelSubscription.Contains(webhookNotification.Kind))
                    {
                        CancelUserSubscription(user);
                    }
                }
            }


            return new BraintreeWebhookResponse { Success = true };
        }

        public BraintreeCancelSubscriptionResponse Post(BraintreeCancelSubscriptionRequest request)
        {
            var user = ValidateAndGetCurrentUser();
            if (user == null)
                throw new UnauthorizedAccessException("You must be authenticated.");

            CancelUserSubscription(user);

            return new BraintreeCancelSubscriptionResponse();


        }

    }
}
