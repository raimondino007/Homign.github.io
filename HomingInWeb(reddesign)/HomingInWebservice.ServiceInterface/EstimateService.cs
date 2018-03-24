using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HomingInWebservice.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;
using System.Diagnostics;
using HomingInWebservice.ServiceModel.Models;

namespace HomingInWebservice.ServiceInterface
{
    public class EstimateService : HomingInService
    {
        const int MaximumEstimates = 25;
        const int AgentSampleId = 36;

        IEnumerable<Estimate> GetEstimates(IEnumerable<string> postalCodes = null, int count = MaximumEstimates, int offset = 0)
        {
            IEnumerable<Estimate> estimates;
            count = count.Clamp(1, 25);
            offset = offset > 0 ? offset : 0;
            postalCodes = postalCodes?.Where(p => p != null);

            var user = ValidateAndGetCurrentUser();

            //TODO Lazy loading by page. //Limit(offset, count)
            var exp = Db.From<Estimate>().Where(e => e.Active).OrderByDescending<Estimate>(p => p.CreatedOn);

            //filter to zip codes if needed
            if (postalCodes != null)
                exp = exp.Where(e => Sql.In(e.PostalCode, postalCodes));

            //filter Premium
            var compDate = DateTime.Now.AddDays(-3);

            exp = exp.Where(e =>
                e.IsPremiumResponded == false
                || (e.IsPremiumResponded == true && e.PremiumAgentId == user.PremiumAgentId)
            );

            exp = exp.Where(e => 
                e.PremiumAgentId == null
                || (e.PremiumAgentId != null && e.CreatedOn < compDate)
                || (e.PremiumAgentId == user.PremiumAgentId && user.PremiumExpirationDate > DateTime.Now)
            );

            //we cannot use loadselect here because it uses subqueries and we are using limits
            estimates = Db.Select<Estimate>(exp);
            //Debug.WriteLine(Db.GetLastSql());

            //get a distinct list of users and fill in our estimates
            if (estimates.Count() > 0)
            {
                var distinctUserIds = estimates.Select(e => e.UserId).Distinct().ToList();
                var users = Db.Select<User>(u => Sql.In(u.Id, distinctUserIds));
                //Debug.WriteLine(Db.GetLastSql());
                Parallel.ForEach(estimates, e => e.User = users.FirstOrDefault(u => u.Id == e.UserId));
            }

            return estimates;
        }

        //create an estimate
        public object Post(CreateEstimateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Address))
                throw new ArgumentException("Missing parameter 'Address'");

            if (string.IsNullOrWhiteSpace(request.City))
                throw new ArgumentException("Missing parameter 'City'");

            if (string.IsNullOrWhiteSpace(request.State))
                throw new ArgumentException("Missing parameter 'State'");

            if (string.IsNullOrWhiteSpace(request.PostalCode))
                throw new ArgumentException("Missing parameter 'PostalCode'");

            //Validate logged in user
            var user = ValidateAndGetCurrentUser();
            User premiumUser = new User();

            //try to pull the email from the user, and if not pull the anonymous from the request
            var email = user.Email;

            if (!string.IsNullOrEmpty(request.AnonymousEmail))
                email = request.AnonymousEmail;

            //if we didnt get an email from either, null it out so it doesnt hit the db
            if (!email.Contains("@"))
                email = null;

            // If Premium User has this postal Code
            if (request.PremiumAgentId != null)
            {
                premiumUser = Db.Select<User>(u => u.PremiumAgentId == request.PremiumAgentId).FirstOrDefault();
                if (premiumUser != null)
                {
                    var postalCodes = PostalCodesForUser(premiumUser.Id);

                    if (!postalCodes.Contains(request.PostalCode))
                    {
                        request.PremiumAgentId = null;
                    }
                }
            }

            //setup the new estimate
            var estimate = new Estimate
            {
                Address = request.Address,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode,
                UnitNumber = request.UnitNumber,
                Notes = request.Notes,
                CreatedOn = DateTime.UtcNow,
                User = user,
                AssociatedEmail = email,
                AccessToken = Guid.NewGuid(),
                Active = true,
                PremiumAgentId = request.PremiumAgentId
            };

            //save and return it
            Db.Save<Estimate>(estimate, true);

            var query = Db.From<PushDevice>();
            query.Join<PushDevice, AgentPostalCode>((push, postal) => push.UserId == postal.UserId);
            query.Where<AgentPostalCode>(p => p.PostalCode == estimate.PostalCode);
            var devicesToPush = Db.Select(query);

            //Parallel.ForEach(devicesToPush, device =>
            //{
            //    SendPush($"There is a new estimate request in {estimate.PostalCode}.", device);
            //});

            bool onlyPremium = !String.IsNullOrWhiteSpace(estimate.PremiumAgentId);
            var agentDashboardUrl = $"{BaseEndpoint}dashboard/properties/{estimate.Id}";

            if (onlyPremium && premiumUser != null)
            {
                // Notify only premium user
                Task premiumEmailTask = new Task(() =>
                {
                    var subject =
                        $"New estimate request for zip code {estimate.PostalCode} ({estimate.City}, {estimate.State})";
                    var body =
                        $"A new estimate request has been submitted for the property at:\r\n{estimate.Address}  {estimate.UnitNumber}\r\n{estimate.City}, {estimate.State}, {estimate.PostalCode}\r\n\r\nClick here to respond:\r\n{agentDashboardUrl}";
                    SendEmailFromSupport(premiumUser.Email, subject, body);
                });
                premiumEmailTask.Start();
                premiumEmailTask.Wait();
            }
            else
            {
                //find agents to notify
                var emailQuery = Db.From<User>();
                emailQuery.Select(u => u.Email);
                emailQuery.Join<User, AgentPostalCode>((u, postal) => u.Id == postal.UserId);
                emailQuery.Where<AgentPostalCode>(p => p.PostalCode == estimate.PostalCode);
                var emailsToSendTo = Db.ColumnDistinct<string>(emailQuery).Distinct();
                Console.WriteLine(Db.GetLastSql());


                //TODO: This is going to go over our email quota **easily**
                //the SendEmailFromSupport method must be re-written to store pending emails in a queue 
                //which can be background processed at a later time
                Parallel.ForEach(emailsToSendTo, agentEmail =>
                {
                    var subject =
                        $"New estimate request for zip code {request.PostalCode} ({request.City}, {request.State})";
                    var body =
                        $"A new estimate request has been submitted for the property at:\r\n{request.Address}  {estimate.UnitNumber}\r\n{request.City}, {request.State}, {request.PostalCode}\r\n\r\nClick here to respond:\r\n{agentDashboardUrl}";
                    SendEmailFromSupport(agentEmail, subject, body);
                });
            }




            //email the admins
            Task emailTask = new Task(() =>
            {
                var to = "support@homingin.co";
                //var to = "josh@hive05.com";
                var subject = $"New estimate request for zip code {request.PostalCode} ({request.City}, {request.State})";
                var body = $"{user.FirstName} {user.LastName} has requested an estimate for the property at:\r\n{request.Address}\r\n{request.City}, {request.State}, {request.PostalCode}\r\nThey included {request.PictureCount} pictures.\r\n\r\nThere are {devicesToPush.Count} agents in this service area.\r\nhttp://dashboard.homingin.co/estimates/{estimate.Id}";

                if (!AwsBucket.ToLower().Contains("devel"))
                    SendEmailFromSupport(to, subject, body);
            });
            emailTask.Start();
            emailTask.Wait();

            //email the user an email to view the listing
            if( !string.IsNullOrWhiteSpace(request.AnonymousEmail) )
            {
                emailTask = new Task(() =>
                {
                    var estimateLink = $"{BaseEndpoint}valuations/{estimate.AccessToken}";

                    var to = request.AnonymousEmail;
                    var subject = $"Valuation request for: {request.Address}, {request.City}, {request.State}";
                    var body = $"Congratulations!  You've submitted a request for a home value.  Nearby real estate agents are hard at work determining your home's current market value.  As they send in their responses, you'll be notified.\r\n\r\nIn order to be able to see the responses, you'll need to use this link to navigate back to your request\r\n\r\n{estimateLink}\r\n\r\nFrom your user dashboard you can create an account that will let you log in if you lose this email or if you want to display them on our mobile app.  Don't worry, we don't share your information with anyone\r\n\r\nAlso, if you're thinking of selling and want to talk to one of the agents that gave you a home value, their contact information is readily available for you.  Just give them a call anytime.\r\n\r\nThanks for using our home valuation service!\r\n\r\nTodd Miller\r\nCEO Co-Founder\r\n5580 S Ft Apache #120\r\nLas Vegas, NV 89148\r\nsupport@homingin.co";

                    SendEmailFromSupport(to, subject, body);
                });
                emailTask.Start();
                emailTask.Wait();
            }

            return new CreateEstimateResponse { EstimateId = estimate.Id };
        }

        public async Task<EstimateDetailsResponse> Get(EstimateDetailsRequest request)
        {
            //Validate logged in user
            var user = ValidateAndGetCurrentUser();

            var estimate = await Db.LoadSingleByIdAsync<Estimate>(request.EstimateId);
            if (estimate == null)
                return new EstimateDetailsResponse();

            AddPicturesToEstimate(estimate);

            if (user.Id == estimate.UserId)
                AddResponsesToEstimate(estimate);
            else
                AddResponsesToEstimate(estimate, user.Id);

            return new EstimateDetailsResponse { Estimate = estimate };
        }

        public async Task<EstimateDetailsResponse> Post(ClaimEstimateFromAccessToken request)
        {
            var user = ValidateAndGetCurrentUser();
            var estimates = await Db.SelectAsync<Estimate>(e => e.AccessToken == request.AccessToken);
            var estimate = estimates.FirstOrDefault();

            estimate.UserId = user.Id;

            Db.Update<Estimate>(estimate);

            return new EstimateDetailsResponse { Estimate = estimate };
        }

        public async Task<EstimateIsAnanymousResponse> Get(EstimateFromAccessTokenIsAnonymous request)
        {
            var estimatesCollection = await Db.SelectAsync<Estimate>(e => e.AccessToken == request.AccessToken);
            var estimate = estimatesCollection.FirstOrDefault();
            var anonymousCollection = await Db.SelectAsync<User>(u => u.Id == estimate.UserId);
            var anonymous = anonymousCollection.FirstOrDefault();

            //var owner = await Db.Select<User>(u => u.Email == user.Email && u.Id != user.Id).FirstOrDefault();


            return new EstimateIsAnanymousResponse { IsAnonymous = anonymous.FirstName == "Anonymous" && anonymous.LastName == "Anonymous" };
        }

        public async Task<EstimateDetailsResponse> Get(EstimateFromAccessToken request)
        {
            var estimates = await Db.SelectAsync<Estimate>(e => e.AccessToken == request.AccessToken);
            if( estimates == null || estimates.Count < 1 )
                return new EstimateDetailsResponse();

            //pull the first one (there should only be one...)
            var estimate = estimates.FirstOrDefault();
            if (estimate == null)
                return new EstimateDetailsResponse();

            //pull pics
            var pictures = await Db.SelectAsync<EstimatePicture>(p => p.EstimateId == estimate.Id);
            estimate.Pictures = new List<PictureData>();
            pictures.ForEach(ep => estimate.Pictures.Add(new PictureData { URL = $"{AwsBaseUrl}{AwsBucket}/estimates/{ep.EstimateId}/{ep.PictureId:N}.jpg", PictureType = ep.Type }));

            //pull responses
            AddResponsesToEstimate(estimate);

            return new EstimateDetailsResponse { Estimate = estimate };
        }

        public async void Get(UnsubFromAccessToken request)
        {
            var estimates = await Db.SelectAsync<Estimate>(e => e.AccessToken == request.AccessToken);
            if (estimates == null || estimates.Count < 1)
                return;

            //pull the first one (there should only be one...)
            var estimate = estimates.FirstOrDefault();
            if (estimate == null)
                return;

            estimate.AssociatedEmail = null;
            Db.Save(estimate);
        }

        private void AddPicturesToEstimate(Estimate estimate)
        {
            if (estimate == null) return;

            foreach (var pictureInfo in Db.Select<EstimatePicture>(p => p.EstimateId == estimate.Id))
            {
                estimate.Pictures.Add(new PictureData { URL = $"{AwsBaseUrl}{AwsBucket}/estimates/{estimate.Id}/{pictureInfo.PictureId:N}.jpg", PictureType = pictureInfo.Type });
            }
        }

        private void AddResponsesToEstimate(Estimate estimate, ulong? restrictToUserId = null)
        {
            if (estimate == null) return;

            var query = Db.From<EstimateResponse>().Where(r => r.Active == true && r.EstimateId == estimate.Id);
            if (restrictToUserId != null)
                query = query.Where(r => r.UserId == restrictToUserId.Value);

            var responses = Db.Select<EstimateResponse>(query);
            foreach (var response in responses)
            {
                response.User = Db.SingleById<User>(response.UserId);

                if (response.User.IsPremium)
                {
                    var premiumInfo = Db.Select<PremiumInfo>(p => p.UserId == response.User.Id).FirstOrDefault();
                    response.User.PremiumInfo = premiumInfo;
                }
                estimate.Responses.Add(response);
            }

            estimate.Responses = SortResponses(estimate.Responses);

            //Debug.WriteLine(Db.GetLastSql());
        }

        public object Post(RespondToEstimateRequest request)
        {
            var user = ValidateAndGetCurrentUser();
            var estimate = Db.SingleById<Estimate>(request.EstimateId);

            if (estimate == null)
                throw new ArgumentException("The estimate provided does not exist.");

            EstimateResponse estimateResponse = null;

            if ( request.ResponseId != null )
            {
                estimateResponse = Db.SingleById<EstimateResponse>(request.ResponseId);
                if (estimateResponse == null)
                    throw new ArgumentException("Invalid response id.");

                estimateResponse.Notes = request.Notes;
                estimateResponse.EstimateValue = request.EstimateValue;
                estimateResponse.CloudCMALink = request.CloudCMALink;
            }

            if( estimateResponse == null )
            {
                estimateResponse = new EstimateResponse
                {
                    CreatedOn = DateTime.Now,
                    EstimateId = request.EstimateId,
                    EstimateValue = request.EstimateValue,
                    Notes = request.Notes,
                    User = user,
                    UserId = user.Id,
                    CloudCMALink = request.CloudCMALink,
                    Active = true
                };
            }

            Db.Save<EstimateResponse>(estimateResponse);
            //Debug.WriteLine($"{Db.GetLastSql()}");

            // If Premium Agent Responded before premium is expired
            var compDate = DateTime.Now.AddDays(-3);
            if (estimate.PremiumAgentId != null && estimate.PremiumAgentId == user.PremiumAgentId && estimate.CreatedOn > compDate)
            {
                estimate.IsPremiumResponded = true;
                Db.Save<Estimate>(estimate);
            }

            var device = Db.Single<PushDevice>(d => d.UserId == estimate.UserId);
            if (device != null && request.EstimateId != AgentSampleId)
            {
                var message = $"{user.FirstName} {user.LastName} has responded to your estimate request.";
                Task.Run(() => SendPush(message, device));
            }

            //email the user to let them know there has been a response
            if (!string.IsNullOrWhiteSpace(estimate.AssociatedEmail))
            {
                var emailTask = new Task(() =>
                {
                    var estimateLink = $"{BaseEndpoint}valuations/{estimate.AccessToken}";
                    var unsubLink = $"{BaseEndpoint}unsub/{estimate.AccessToken}";
                    var cloudCmaInsert = "";
                    if (!string.IsNullOrWhiteSpace(request.CloudCMALink))
                        cloudCmaInsert = $"\r\n\r\nAdditionally the agent has provided a CMA for your property\r\nIt can be found here:\r\n{request.CloudCMALink}";

                    var to = estimate.AssociatedEmail;
                    var subject = $"New Valuation from {user.FirstName} {user.LastName}";
                    var body = $"A local real estate professional has responded to your home value request.\r\n{estimateLink}\r\nWe will notify you as more agents respond.{cloudCmaInsert}\r\n\r\nTo stop receiving these notifications click this link:\r\n{unsubLink}";

                    SendEmailFromSupport(to, subject, body);
                });
                emailTask.Start();
            }

            return new RespondToEstimateResponse { ResponseId = estimateResponse.Id };
        }

        //get a list of estimates a user has made
        public object Get(EstimatesByUserRequest request)
        {
            var user = ValidateAndGetCurrentUser();
            if (user.Id != request.UserId)
                throw new UnauthorizedAccessException("'UserId' must be the same as the authenticated user.");

            //pull a list of the estimates and set the user appropriately
            var estimates = Db.Select<Estimate>(e => e.UserId == request.UserId);
            foreach (var estimate in estimates)
            {
                AddResponsesToEstimate(estimate);
                AddPicturesToEstimate(estimate);
                //Debug.WriteLine($"{Db.GetLastSql()}");
            }

            return new EstimateListResponse { Estimates = estimates };
        }

        //get a list of recent estimates
        public object Post(RecentEstimatesRequest request)
        {
            return new EstimateListResponse { Estimates = GetEstimates(request.PostalCodes, request.Count, request.Offset) };
        }

        //get a list of recent estimates
        public object Get(PendingEstimatesForAgentRequest request)
        {
            //Validate logged in user
            var requestingUser = ValidateAndGetCurrentUser();
            if (request.AgentId != requestingUser.Id)
                throw new UnauthorizedAccessException("'AgentId' must be the same as the authenticated user.");

            var estimates = GetEstimates(PostalCodesForUser(request.AgentId));


            foreach (var estimate in estimates)
            {
                AddResponsesToEstimate(estimate, requestingUser.Id);
                AddPicturesToEstimate(estimate);
                //Debug.WriteLine($"{Db.GetLastSql()}");
            }

            if (estimates.Count() < 1)
            {
                var sampleEstimate = Db.SingleById<Estimate>(AgentSampleId);
                AddResponsesToEstimate(sampleEstimate, requestingUser.Id);
                AddPicturesToEstimate(sampleEstimate);

                if( sampleEstimate != null )
                    estimates = new List<Estimate> { sampleEstimate };
            }

            return new EstimateListResponse { Estimates = estimates };
        }

        public object Post(AttachImageToEstimateRequest request)
        {
            //verify authenticated user
            var requestingUser = ValidateAndGetCurrentUser();

            //verify user made estimate
            var estimate = Db.SingleById<Estimate>(request.EstimateId);

            if (estimate == null)
                throw new ArgumentException("Invalid estimate id");

            if (estimate.UserId != requestingUser.Id)
                throw new UnauthorizedAccessException("You do not have permission to update this estimate.");

            if (string.IsNullOrWhiteSpace(request.Base64Picture))
                throw new ArgumentException("Missing parameter 'Base64Picture'");

            //if this request came from the web, then there is some nonsense before the base64 actually starts
            var imageString = request.Base64Picture.Substring(request.Base64Picture.IndexOf(",") + 1);

            //decode the image
            byte[] imageBytes = Convert.FromBase64String(imageString);
            if (imageBytes == null || imageBytes.Length < 1)
                throw new ArgumentException("Missing parameter 'Base64Picture'");

            var photoId = Guid.NewGuid();

            //upload image to s3
            string url = UploadImage(imageBytes, $"estimates/{request.EstimateId}/{photoId:N}.jpg");
            if (string.IsNullOrWhiteSpace(url))
                throw new Exception("Failed to save picture.");

            //update database with info from s3
            var picture = new EstimatePicture
            {
                EstimateId = request.EstimateId,
                PictureId = photoId,
                Type = request.PictureType
            };
            Db.Save<EstimatePicture>(picture);

            //return url
            return new AttachImageResponse { PictureURL = url };
        }
    }
}
