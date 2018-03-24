using ServiceStack;
using ServiceStack.OrmLite;
using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.S3;
using System.IO;
using Amazon;
using System.Diagnostics;
using Amazon.S3.Model;
using HomingInWebservice.ServiceModel;
using System.Net;
using System.Text.RegularExpressions;
using HomingInWebservice.ServiceModel.Models;
using System.Threading.Tasks;
using System.Text;
using System.Web.Script.Serialization;

namespace HomingInWebservice.ServiceInterface
{
    public abstract class HomingInService : Service
    {
        protected const string MandrillApiKey = "bCEiuVZmBB84SOiU_K-7nQ";
        protected const string AwsAccessKey = "AKIAJ7LNMM7UBCWVZMGQ";
		protected const string AwsSecretKey = "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";
		protected const string AwsBucket = "homing-in-development";
		//protected const string AwsBucket = "homing-in";
		protected const string AwsBaseUrl = "https://s3.amazonaws.com/";
		protected readonly RegionEndpoint Region = RegionEndpoint.USEast1;

        //TODO: this is a dumb way to do this
        protected const string BaseEndpoint = "https://www.homingin.co/";

        protected User ValidateAndGetCurrentUser()
        {
			var sessionString = base.Request.GetHeader ("HI_SESSION");

            if( string.IsNullOrEmpty(sessionString))
                sessionString = base.Request.Cookies["hi-session"].Value;

			if( string.IsNullOrEmpty(sessionString) )
				throw new AuthenticationException("A valid session is required.");

			var sessionId = Guid.Empty;
            if (!Guid.TryParse(sessionString, out sessionId))
                throw new AuthenticationException("A valid session is required.");

            var user = Db.Single<User>(u => u.SessionId == sessionId);
            if (user == null || user.SessionId != sessionId)
                throw new AuthenticationException("A valid session is required.");

            return user;
        }

        protected void UpdateUserSession(User user)
        {
            user.SessionId = Guid.NewGuid();
            user.LastLogin = DateTime.UtcNow;
            Db.Save(user);
        }

        protected IEnumerable<string> PostalCodesForUser(UInt64 id)
        {
            return Db.Column<string>(Db.From<AgentPostalCode>().Select(p => p.PostalCode).Where(p => p.UserId == id)).Distinct();
        }

        protected IList<EstimateResponse> SortResponses(IEnumerable<EstimateResponse> responsesToSort)
        {
            //sort the responses
            var responses = responsesToSort.ToList();
            responses.Sort((r1, r2) =>
            {
                if (r1.User.IsPremium && !r2.User.IsPremium)
                    return -1;

                if (!r1.User.IsPremium && r2.User.IsPremium)
                    return 1;

                if (!string.IsNullOrWhiteSpace(r1.CloudCMALink) && string.IsNullOrWhiteSpace(r2.CloudCMALink))
                    return -1;

                if (string.IsNullOrWhiteSpace(r1.CloudCMALink) && !string.IsNullOrWhiteSpace(r2.CloudCMALink))
                    return 1;

                return r1.CreatedOn.CompareTo(r2.CreatedOn);
            });

            return responses;
        }

        protected User AgentWithPostalCodes(UInt64 id)
        {
            var user = Db.SingleById<User>(id);
            if (user != null) user.PostalCodes = PostalCodesForUser(id);
            return user;
        }

		protected string UploadImage(byte[] bytes, string path)
		{
			try
			{
				using (var client = new AmazonS3Client(AwsAccessKey, AwsSecretKey, Region))
				{
					using (var stream = new MemoryStream(bytes))
					{
						PutObjectRequest s3Request = new PutObjectRequest();
						s3Request.BucketName = AwsBucket;
						s3Request.CannedACL = S3CannedACL.PublicRead;
						s3Request.Key = path;
						s3Request.InputStream = stream;

						client.PutObject(s3Request);
						return $"{AwsBaseUrl}{AwsBucket}/{s3Request.Key}";
					}
				}
			}
			catch (AmazonS3Exception amazonS3Exception)
			{
				//TODO: log this to something
				if (amazonS3Exception.ErrorCode != null && (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") || amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
				{
					Debug.WriteLine("Check the provided AWS Credentials.");
					Debug.WriteLine("For service sign up go to https://aws.amazon.com/s3");
				}
				else
				{
					Debug.WriteLine( $"Error occurred. Message:'{amazonS3Exception.Message}' when writing an object" );
				}
			}
			catch (Exception exc)
			{
				//TODO: log this to something
				Debug.WriteLine(exc.ToString());
			}

			return string.Empty;
		}

        protected async Task SendMandrillEmail(List<string> toAddress, string subject, string templateName, Dictionary<string, string> globalMergeVariables = null)
        {
            var email = new Mandrill.Models.EmailMessage();
            email.To = toAddress.Select(s => new Mandrill.Models.EmailAddress(s));
            email.Subject = subject;
            
            if( globalMergeVariables != null )
            {
                email.Merge = true;
                email.MergeLanguage = "mailchimp";
                foreach( var kvp in globalMergeVariables )
                {
                    email.AddGlobalVariable(kvp.Key, kvp.Value);
                }
            }

            var request = new Mandrill.Requests.Messages.SendMessageTemplateRequest(email, templateName);

            var api = new Mandrill.MandrillApi(MandrillApiKey);
            var result = await api.SendMessageTemplate(request);
        }

        protected void SendEmailFromSupport(string toAddress, string subject, string body) {
            SendEmail("support@homingin.co", toAddress, subject, body);
        }
        protected void SendEmail(string fromAddress, string toAddress, string subject, string body)
        {
            const string SMTP_USERNAME = "Homing In, LLC";
            const string SMTP_PASSWORD = MandrillApiKey;
            const string HOST = "smtp.mandrillapp.com";
            const int PORT = 587;

            using (System.Net.Mail.SmtpClient client = new System.Net.Mail.SmtpClient(HOST, PORT))
            {
                client.Credentials = new System.Net.NetworkCredential(SMTP_USERNAME, SMTP_PASSWORD);
                client.EnableSsl = true;
                try { client.Send(fromAddress, toAddress, subject, body); }
                catch (Exception) { }
            }
        }

        //protected void SendPush(string message, PushDevice device, string badge = "+1", string sound = "Default", Dictionary<string, string> metadata = null)
        //{
        //    //TODO: This is a stupid dumb hack until i can replace this garbage with a json object hirarchy

        //    var baseAddress = new Uri("https://android.googleapis.com/gcm/send");

        //    var client = new WebClient();
        //    client.Headers[HttpRequestHeader.ContentType] = "application/json";
        //    client.Headers["Authorization"] = "key=AIzaSyCgCPIjtdwNauzxLxFYRrCFiX4A29-X4sM";

        //    var template = @"{
        //                        ""to"": ""**TOKEN**"",
        //                        ""notification"": 
        //                        {
        //                            ""title"": ""Homing In"",
        //                            ""body"": ""**MESSAGE**"",
        //                            ""sound"": ""**SOUND**""
        //                        }
        //                    }";

        //    Console.Out.WriteLine($"PUSHING TO: {device.DeviceId}");

        //    var postbody = template.Replace("**MESSAGE**", message);
        //    postbody = postbody.Replace("**BADGE**", badge);
        //    postbody = postbody.Replace("**SOUND**", sound);
        //    postbody = postbody.Replace("**TOKEN**", device.DeviceId);

        //    try
        //    {
        //        client.UploadStringTaskAsync(baseAddress, "POST", postbody).ContinueWith((t) => {
        //            var response = t.Result;
        //            if (t.Exception != null)
        //            {
        //                Debug.WriteLine($"Failed to push to device: {device.DeviceId}");
        //                Debug.WriteLine(t.Exception.ToString());
        //            }
        //            else
        //            {
        //                Debug.WriteLine($"Pushed to token: {device.DeviceId}");
        //                Debug.WriteLine(response);
        //            }
        //        });
        //    }
        //    catch (Exception exc)
        //    {
        //        Debug.WriteLine($"Failed to push to device: {device.DeviceId}");
        //        Debug.WriteLine(exc.ToString());
        //    }
        //}

        protected void SendPush(string message, PushDevice device, string badge = "+1", string sound = "Default", Dictionary<string, string> metadata = null)
        {
            //if (device.DeviceType == DeviceType.Android) {
            //    SendPushToAndroid(message, device);
            //}
            SendPushToFCM(message, device);
        }
        
        protected void SendPushToAndroid(string message, PushDevice device, string badge = "+1", string sound = "Default", Dictionary<string, string> metadata = null)
        {
            SendPushToFCM(message, device);
            string androidPushNotificationHostName = "https://android.googleapis.com/gcm/send";
            string googleAppId = "AIzaSyCgCPIjtdwNauzxLxFYRrCFiX4A29-X4sM";
            var senderId = "2464547879";
            string deviceToken = device.DeviceId;

            string responseFromServer = "";
            try
            {
                WebRequest tRequest;
                tRequest = WebRequest.Create(androidPushNotificationHostName);
                tRequest.Method = "post";
                tRequest.ContentType = " application/x-www-form-urlencoded;charset=UTF-8";
                tRequest.Headers.Add(string.Format("Authorization: key={0}", googleAppId));
                tRequest.Headers.Add(string.Format("Sender: id={0}", senderId));

                string postData = "collapse_key=score_update&time_to_live=108&delay_while_idle=1" +
                                  "&notification.title=Homing In" +
                                  "&notification.body=" + message +
                                  "&data.time=" + System.DateTime.Now.ToString() +
                                  "&registration_id=" + deviceToken + "";
                Byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                tRequest.ContentLength = byteArray.Length;
                Stream dataStream = tRequest.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                WebResponse tResponse = tRequest.GetResponse();
                dataStream = tResponse.GetResponseStream();
                if (dataStream != null)
                {
                    StreamReader tReader = new StreamReader(dataStream);
                    responseFromServer = tReader.ReadToEnd();
                    tReader.Close();
                }
                dataStream.Close();
                tResponse.Close();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to push to device: {device.DeviceId}");
                Debug.WriteLine(e.ToString());
            }
        }

        protected void SendPushToFCM(string message, PushDevice device, string badge = "+1", string sound = "Default", Dictionary<string, string> metadata = null) {
            string fcmAuthKey = "AIzaSyCqx-iSqA8RSLq9anp0oWhaZRJulGp0GJQ";
            string fcmSenderKey = "439392935545";
            WebRequest tRequest = WebRequest.Create("https://fcm.googleapis.com/fcm/send");
            tRequest.Method = "post";
            tRequest.ContentType = "application/json";
            var objNotification = new
            {
                to= device.DeviceId,
                notification = new
                {
                    body = "Homing In",
                    title = message
                }
            };

            var serializer = new JavaScriptSerializer();
            string jsonNotificationFormat = serializer.Serialize(objNotification);

            Byte[] byteArray = Encoding.UTF8.GetBytes(jsonNotificationFormat);
            tRequest.Headers.Add(string.Format("Authorization: key={0}", fcmAuthKey));
            tRequest.Headers.Add(string.Format("Sender: id={0}", fcmSenderKey));
            tRequest.ContentLength = byteArray.Length;
            tRequest.ContentType = "application/json";
            try { 
                using (Stream dataStream = tRequest.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);

                    using (WebResponse tResponse = tRequest.GetResponse())
                    {
                        using (Stream dataStreamResponse = tResponse.GetResponseStream())
                        {
                            using (StreamReader tReader = new StreamReader(dataStreamResponse))
                            {
                                String responseFromFirebaseServer = tReader.ReadToEnd();
                                FCMResponse response = serializer.Deserialize<FCMResponse>(responseFromFirebaseServer);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to push to device: {device.DeviceId}");
                Debug.WriteLine(e.ToString());
            }
        }

    }

    public class FCMResponse
    {
        public long multicast_id { get; set; }
        public int success { get; set; }
        public int failure { get; set; }
        public int canonical_ids { get; set; }
        public List<FCMResult> results { get; set; }
    }
    public class FCMResult
    {
        public string message_id { get; set; }
    }
}
