using System;
using System.Linq;
using ServiceStack;
using HomingInWebservice.ServiceModel;
using ServiceStack.OrmLite;
using PasswordHashTool;
using System.Threading.Tasks;
using HomingInWebservice.ServiceModel.Models;
using System.Collections.Generic;

namespace HomingInWebservice.ServiceInterface
{
    public class AuthenticationService : HomingInService
    {
        public async Task<GuidResponse> Get(GuidRequest request)
        {
            /*
            var recipiants = new List<string>
            {
                "josh@hive05.com"
            };

            await SendMandrillEmail(recipiants, "test email", "new-home-valuation-consumer", new Dictionary<string, string>
            {
                ["anonymous_home_url"] = "http://www.someurl.com"
            });
            */

            return new GuidResponse { Guid = Guid.NewGuid().ToString() };

        }

        //Login as normal user
        public object Post(AuthenticatedSessionRequest request)
        {
            long estimateCount = 0;

            //param validation

            if (request == null || string.IsNullOrWhiteSpace(request.DeviceId))
                throw new ArgumentException("Missing parameter 'DeviceId'");

            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Missing parameter 'Email'");

            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Missing parameter 'Password'");

            //find the user and validate the password
            var user = Db.Single<User>(u => u.Email == request.Email && u.Active == true);
            if (user != null && PasswordHashManager.ValidatePassword(request.Password, user.Password))
            {
                //its good... give em a new session and carry on
                UpdateUserSession(user);

                //if the user is an agent, add in their zip codes
                if (user.IsAgent)
                {
                    user.PostalCodes = PostalCodesForUser(user.Id);
                }
                else
                {
                    estimateCount = Db.Count<Estimate>(e => e.UserId == user.Id);
                }

                return new NewSessionResponse { SessionId = user.SessionId.Value, User = user, EstimateRequestCount = estimateCount };
            }

            //if we get here we either could not find the user or the pw was bad
            throw new AuthenticationException("Invalid username or password.");
        }

        //Anonymous login
        public object Get(AnonymousSessionRequest request)
        {
            long estimateCount = 0;

            //param validation

            if (request == null || string.IsNullOrWhiteSpace(request.DeviceId))
                throw new ArgumentException("Missing parameter 'DeviceId'");

            //see if a user with this device has signed in before
            var user = Db.Single<User>(u => u.Email == request.DeviceId);
            if (user == null)
            {
                //we didnt find one...
                user = new User
                {
                    Email = request.DeviceId,
                    IsAgent = false,
                    FirstName = "Anonymous",
                    LastName = "Anonymous",
                    CreatedOn = DateTime.UtcNow,
                    IsPremium = false,
                };
            }
            else
            {
                //figure out if they have requested any estimates
                estimateCount = Db.Count<Estimate>(e => e.UserId == user.Id);
            }

            //update the session and save
            UpdateUserSession(user);

            //return the new session id
            return new NewSessionResponse { SessionId = user.SessionId.Value, User = user, EstimateRequestCount = estimateCount };
        }

        //Create a user
        public object Post(CreateUserRequest request)
        {
            //param validation

            if (request == null || string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Missing parameter 'Email'");

            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Missing parameter 'Password'");

            if (string.IsNullOrWhiteSpace(request.FirstName))
                throw new ArgumentException("Missing parameter 'FirstName'");

            if (string.IsNullOrWhiteSpace(request.LastName))
                throw new ArgumentException("Missing parameter 'LastName'");

            if (string.IsNullOrWhiteSpace(request.Company) && request.IsAgent)
                throw new ArgumentException("Missing parameter 'Company'");

            if (string.IsNullOrWhiteSpace(request.PhoneNumber) && request.IsAgent)
                throw new ArgumentException("Missing parameter 'PhoneNumber'");


            //make sure there is not already a user with this email
            var user = Db.Single<User>(u => u.Email == request.Email);
            if (user != null)
                throw new ArgumentException("A user with that email address already exists.");

            using (var transaction = Db.BeginTransaction())
            {
                //setup the new account
                user = new User
                {
                    SessionId = Guid.NewGuid(),
                    Email = request.Email,
                    IsAgent = request.IsAgent,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Company = request.Company,
                    PhoneNumber = request.PhoneNumber,
                    CreatedOn = DateTime.UtcNow,
                    Password = PasswordHashManager.CreateHash(request.Password),
                    AffiliateId = request.AffiliateId,
                    IsPremium = false,
                    IsAdmin = false,
                    CanReceiveEmail = true,
                    Active = true
                };
                Db.Save<User>(user);


                if (request.PostalCodes != null)
                {
                    foreach (var code in request.PostalCodes.Where(p => p != null))
                    {
                        Db.Save<AgentPostalCode>(new AgentPostalCode { PostalCode = code, UserId = user.Id });
                    }
                }

                transaction.Commit();

                //if (request.IsAgent)
                //{
                //    Task emailTask = new Task(() =>
                //    {
                //        var to = "support@homingin.co";
                //        //var to = "josh@hive05.com";
                //        var subject = $"New Agent Signup {user.FirstName} {user.LastName} - {user.Company}";
                //        var body = $"{user.FirstName} {user.LastName}\r\n{user.Company}\r\n{user.Email}\r\n\r\nHas signed up for an account with the following service area:\r\n";
                //        if (request.PostalCodes != null)
                //        {
                //            foreach (var code in request.PostalCodes)
                //            {
                //                body += $"{code}\r\n";
                //            }
                //        }

                //        if (!AwsBucket.ToLower().Contains("devel"))
                //            SendEmailFromSupport(to, subject, body);
                //    });
                //    emailTask.Start();
                //}
            }

            return new NewSessionResponse { User = AgentWithPostalCodes(user.Id), SessionId = user.SessionId.Value, EstimateRequestCount = 0 };
        }

        public NewSessionResponse Get(CreateUserRequest request)
        {
            var user = ValidateAndGetCurrentUser();
            var premiumInfo = Db.Select<PremiumInfo>(i => i.UserId == user.Id).FirstOrDefault();

            if (user.IsPremium && premiumInfo == null)
            {
                premiumInfo = new PremiumInfo()
                {
                    UserId = user.Id
                };

                using (var transaction = Db.BeginTransaction())
                {
                    Db.Save<PremiumInfo>(premiumInfo);
                    transaction.Commit();
                }
            }

            user.PremiumInfo = premiumInfo;
            user.PostalCodes = PostalCodesForUser(user.Id);

            return new NewSessionResponse { User = user, SessionId = user.SessionId.Value, EstimateRequestCount = 0 };
        }


        //Generate a reset password email
        public object Post(GeneratePasswordResetLinkRequest request)
        {
            //validate the parameters
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Missing parameter 'Email'");

            //validate the email address exists
            var user = Db.Single<User>(u => u.Email == request.Email);
            if (user == null)
                throw new ArgumentException("A reset email could not be sent.");

            //generate a guid to use for the reset request
            var resetRecord = new PasswordReset
            {
                UniqueId = Guid.NewGuid(),
                RequestingIpAddress = Request.RemoteIp,
                UserId = user.Id,
                CreatedOn = DateTime.UtcNow
            };

            //create the db record
            Db.Save<PasswordReset>(resetRecord);

            //build the reset url
            var resetUrl = BaseEndpoint + $"ResetPassword/{resetRecord.UserId}/{resetRecord.UniqueId}";

            //email the user
            Task emailTask = new Task(() =>
            {
                var to = user.Email;
                var subject = $"Reset your Homing In Password";
                var body = $"A password reset has been requested for the Homing In account: {user.Email}\r\nThe request originated from the ip: {resetRecord.RequestingIpAddress}\r\n\r\nIf you did not request a password reset please contact support by replying to this email address.\r\n\r\nClick the link below to complete the password reset process.\r\n{resetUrl}";

                SendEmailFromSupport(to, subject, body);
            });
            emailTask.Start();

            return new PasswordResponse { Success = true };
        }

        public object Post(CompletePasswordReset request)
        {
            //validate request params
            if (request == null || request.UserId < 1)
                throw new ArgumentException("Missing parameter 'UserId'");

            if (request.UniqueId == null || request.UniqueId == Guid.Empty )
                throw new ArgumentException("Missing parameter 'UniqueId'");

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                throw new ArgumentException("Missing parameter 'NewPassword'");

            //find the user
            //validate the email address exists
            var user = Db.SingleById<User>(request.UserId);
            if (user == null)
                throw new ArgumentException("Invalid user");

            //find the reseet record
            var resetRecord = Db.Single<PasswordReset>(r => r.UniqueId == request.UniqueId);
            if( resetRecord == null )
                throw new ArgumentException("Invalid reset id");

            //validate the user matches the id from the URL
            if( resetRecord.UserId != request.UserId || resetRecord.UserId != user.Id )
                throw new ArgumentException("Invalid reset id");

            var resetAge = DateTime.UtcNow - resetRecord.CreatedOn;
            if( resetAge > new TimeSpan(24, 0, 0) )
                throw new ArgumentException("Invalid reset id");

            //update the user record
            user.Password = PasswordHashManager.CreateHash(request.NewPassword);
            Db.Save<User>(user);

            //delete the password reset record
            Db.DeleteById<PasswordReset>(resetRecord.Id);

            return new PasswordResponse { Success = true };
        }

        public object Any(FallbackForClientRoutes request)
        {
            //Return default.cshtml for unmatched requests so routing is handled on the client
            return new HttpResult
            {
                View = "/default.cshtml"
            };
        }
    }


    //TODO: move this and the above "Any" method which uses it into its own service called AngularSupportService
    [FallbackRoute("/{PathInfo*}")]
    public class FallbackForClientRoutes
    {
        public string PathInfo { get; set; }
    }
}
