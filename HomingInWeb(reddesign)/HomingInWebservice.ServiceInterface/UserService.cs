using System;
using System.Linq;
using ServiceStack;
using HomingInWebservice.ServiceModel;
using ServiceStack.OrmLite;
using PasswordHashTool;
using HomingInWebservice.ServiceModel.Models;

namespace HomingInWebservice.ServiceInterface
{
    public class UserService : HomingInService
    {
        //Login as normal user
        public object Post(UpdateUserRequest request)
        {
            var user = ValidateAndGetCurrentUser();
            if (user.Id != request.UserId)
                throw new UnauthorizedAccessException("'UserId' must be the same as the authenticated user.");

            if (!string.IsNullOrWhiteSpace(request.Company))
                user.Company = request.Company;

            if (!string.IsNullOrWhiteSpace(request.AgentLicense))
                user.AgentLicense = request.AgentLicense;

            if (!string.IsNullOrWhiteSpace(request.Email))
                user.Email = request.Email;

            if (!string.IsNullOrWhiteSpace(request.FirstName))
                user.FirstName = request.FirstName;

            if (!string.IsNullOrWhiteSpace(request.LastName))
                user.LastName = request.LastName;

            if (!string.IsNullOrWhiteSpace(request.Password))
                user.Password = PasswordHashManager.CreateHash(request.Password);

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                user.PhoneNumber = request.PhoneNumber;

            user.CanReceiveEmail = request.CanReceiveEmail;
            user.PremiumInfo = request.PremiumInfo;

            using (var transaction = Db.BeginTransaction())
            {
                Db.Save<User>(user);
                if (request.PostalCodes != null)
                {
                    user.PostalCodes = request.PostalCodes;
                    Db.Delete<AgentPostalCode>(p => p.UserId == user.Id);
                    foreach (var code in request.PostalCodes.Where(p => p != null))
                    {
                        Db.Save<AgentPostalCode>(new AgentPostalCode { PostalCode = code, UserId = user.Id });
                    }
                }

                if (user.IsPremium)
                {
                    Db.Save<PremiumInfo>(request.PremiumInfo);
                }

                transaction.Commit();
            }

            return new UserResponse { User = user };
        }

        public object Post(AttachImageToUserRequest request)
        {
            //verify authenticated user
            var requestingUser = ValidateAndGetCurrentUser();
            if (requestingUser.Id != request.UserId)
                throw new UnauthorizedAccessException("'UserId' must be the same as the authenticated user.");

            if (string.IsNullOrWhiteSpace(request.Base64Picture))
                throw new ArgumentException("Missing parameter 'Base64Picture'");

            //if this request came from the web, then there is some nonsense before the base64 actually starts
            var imageString = request.Base64Picture.Substring(request.Base64Picture.IndexOf(",") + 1);

            //decode the image
            byte[] imageBytes = Convert.FromBase64String(imageString);
            if (imageBytes == null || imageBytes.Length < 1)
                throw new ArgumentException("Missing parameter 'Base64Picture'");

            //upload image to s3
            string url = UploadImage(imageBytes, $"users/{request.UserId}/profile");
            if (string.IsNullOrWhiteSpace(url))
                throw new Exception("Failed to save picture.");

            requestingUser.Picture = new PictureData { URL = url };
            Db.Save<User>(requestingUser);

            //return url
            return new AttachImageResponse { PictureURL = url };
        }

        public object Post(RegisterPushDeviceRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DeviceId))
                throw new ArgumentException("Missing parameter 'DeviceId'");

            if (request.DeviceType == DeviceType.Unknown)
                throw new ArgumentException("Missing parameter 'DeviceType'");

            //verify authenticated user
            var requestingUser = ValidateAndGetCurrentUser();
            if (requestingUser.Id != request.UserId)
                throw new UnauthorizedAccessException("'UserId' must be the same as the authenticated user.");

            //see if this device is already registered
            var deviceId = request.DeviceId.Trim();
            var device = Db.Single<PushDevice>(d => d.DeviceId == deviceId && d.UserId == requestingUser.Id);

            if (device == null)
            {
                device = new PushDevice
                {
                    UserId = requestingUser.Id,
                    DeviceId = deviceId,
                    DeviceType = request.DeviceType
                };
            }

            Db.Save<PushDevice>(device);
            return new RegisterPushDeviceResponse { Success = device.Id > 0 };
        }


        public object Put(PostalCodeChangeRequest request)
        {
            //verify authenticated user
            var requestingUser = ValidateAndGetCurrentUser();
            if (requestingUser.Id != request.UserId)
                throw new UnauthorizedAccessException("'UserId' must be the same as the authenticated user.");

            if (string.IsNullOrWhiteSpace(request.PostalCode))
                throw new ArgumentException("Must provide a postal code");

            //do they already have this one?
            var codes = PostalCodesForUser(requestingUser.Id).ToList();
            if (codes.FirstOrDefault(p => p == request.PostalCode) == null )
            {
                Db.Save<AgentPostalCode>(new AgentPostalCode { PostalCode = request.PostalCode, UserId = requestingUser.Id });
                codes.Add(request.PostalCode);
            }

            return new PostalCodeChangeResponse { PostalCodes = codes };
        }

        public object Delete(PostalCodeChangeRequest request)
        {
            //verify authenticated user
            var requestingUser = ValidateAndGetCurrentUser();
            if (requestingUser.Id != request.UserId)
                throw new UnauthorizedAccessException("'UserId' must be the same as the authenticated user.");

            if (string.IsNullOrWhiteSpace(request.PostalCode))
                throw new ArgumentException("Must provide a postal code");

            //do they already have this one?
            var codes = PostalCodesForUser(requestingUser.Id).ToList();
            if (codes.FirstOrDefault(p => p == request.PostalCode) != null)
            {
                Db.Delete<AgentPostalCode>(p => p.UserId == requestingUser.Id && p.PostalCode == request.PostalCode);
                codes.RemoveAll(p => p == request.PostalCode);
            }

            return new PostalCodeChangeResponse { PostalCodes = codes };
        }

        public object Put(PremiumAgentIdChangeRequest request)
        {
            var user = ValidateAndGetCurrentUser();
            if (user.Id != request.UserId)
                throw new UnauthorizedAccessException("'UserId' must be the same as the authenticated user.");

            if (string.IsNullOrWhiteSpace(request.PremiumAgentId))
                throw new ArgumentException("Must provide a PremiumAgentId");

            var usersCount = Db.Select<User>(u => u.PremiumAgentId == request.PremiumAgentId).Count();

            if (usersCount == 0)
            {
                user.PremiumAgentId = request.PremiumAgentId;
            }
            else
            {
                throw new ArgumentException("This URL is already in use.");
            }

            using (var transaction = Db.BeginTransaction())
            {
                Db.Save<User>(user);
                transaction.Commit();
            }

            return new PremiumAgentIdChangeResponse();

        }

    }
}
