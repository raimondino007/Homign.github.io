using System;
using System.Linq;
using ServiceStack;
using HomingInWebservice.ServiceModel;
using ServiceStack.OrmLite;
using PasswordHashTool;
using HomingInWebservice.ServiceModel.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HomingInWebservice.ServiceInterface
{
    public class ConversationService : HomingInService
    {

        public MessageResponse Post(MessageRequest request)
        {
            var user = ValidateAndGetCurrentUser();

            if (user == null)
                throw new UnauthorizedAccessException("Must be authenticated user.");

            var message = new ConversationMessage()
            {
                Body = request.Body,
                Subject = request.Subject,
                Date = request.Date,
                FromUserId = user.Id,
                ToUserId = request.ToUserId,
                PropertyId = request.PropertyId
            };

            using (var transaction = Db.BeginTransaction())
            {
                Db.Save<ConversationMessage>(message);
                transaction.Commit();
            }

            NotificationMessage(message);

            return new MessageResponse { };
        }

        public async void NotificationMessage(ConversationMessage message)
        {
            var messages = await Db.SelectAsync<ConversationMessage>(m => (m.ToUserId == message.ToUserId || m.FromUserId == message.ToUserId) && m.PropertyId == message.PropertyId);
            var user = await Db.SelectAsync<User>(u => u.Id == message.ToUserId);
            var estimate = await Db.SelectAsync<Estimate>(e => e.Id == message.PropertyId);

            var fromUser = ValidateAndGetCurrentUser();
            var toUser = user.FirstOrDefault();
            var property = estimate.FirstOrDefault();

            if (toUser != null && toUser.CanReceiveEmail == true)
            {

                if (fromUser.IsAgent == true)
                {
                    var fromUserMessagesCount = messages.Count(m => m.FromUserId == fromUser.Id);
                    if (fromUserMessagesCount == 1)
                    {
                        // Notify Homeowner if this is the first Agent response
                        SendNotificationMessageEmail(message.Body, property, fromUser, toUser);
                    }

                    if (messages.Count > 1)
                    {
                        var messageRepliedTo = messages[messages.Count - 2];

                        if (messageRepliedTo.FromUserId == message.ToUserId)
                        {
                            // Notify Homeowner if this is the Agent response on your message
                            SendNotificationMessageEmail(message.Body, property, fromUser, toUser);
                        }
                    }
                }


                if (toUser.IsAgent == true)
                {
                    // Notify Agent about every message
                    SendNotificationMessageEmail(message.Body, property, fromUser, toUser);
                }
            }

        }

        public void SendNotificationMessageEmail(string message, Estimate property, User fromUser, User toUser)
        {
            var messagesUrl = $"{BaseEndpoint}dashboard/messages";
            var subject = $"New message for zip code {property.PostalCode} ({property.City}, {property.State})";
            var body = $"You have new message for property at:\r\n{property.Address}\r\n{property.City}, {property.State}, {property.PostalCode}\r\n" +
                               $"\r\n" +
                               $"From {fromUser.FirstName} {fromUser.LastName}:\r\n" +
                               $"\"{message}\"\r\n" +
                               $"\r\n" +
                               $"Go to messages: {messagesUrl}";
            SendEmailFromSupport(toUser.Email, subject, body);
        }

        public async Task<MessageResponse> Get(MessageRequest request)
        {
            var user = ValidateAndGetCurrentUser();

            if (user == null)
                throw new UnauthorizedAccessException("Must be authenticated user.");

            var messages = await Db.SelectAsync<ConversationMessage>(m => (m.ToUserId == user.Id && m.FromUserId == request.ToUserId) || (m.ToUserId == request.ToUserId && m.FromUserId == user.Id));
            var propertyMessages = messages.Where(m => m.PropertyId == request.PropertyId).ToList();

            return new MessageResponse { Message = propertyMessages };
        }

        

        public async Task<MessageResponse> Get(MessageUsersRequest request)
        {
            var user = ValidateAndGetCurrentUser();

            if (user == null)
                throw new UnauthorizedAccessException("Must be authenticated user.");



            var messages = await Db.SelectAsync<ConversationMessage>(m => m.ToUserId == user.Id || m.FromUserId == user.Id);

            var messagesChats = messages.Select(m => new ConversationChat { UserId = m.FromUserId != user.Id ? m.FromUserId : m.ToUserId , EstimateId = m.PropertyId}).GroupBy(m => new { m.EstimateId, m.UserId }).Select(m => m.First());

            var userIds = messages.Select(m => m.FromUserId).Concat(messages.Select(m => m.ToUserId)).Distinct().Where(m => m != user.Id).AsEnumerable();
            var users = await Db.SelectAsync<User>(u => userIds.Contains(u.Id));
            var propIds = messages.Select(m => m.PropertyId).Distinct().AsEnumerable();
            var properties = await Db.SelectAsync<Estimate>(e => propIds.Contains(e.Id));
            var estimateIds = properties.Select(p => p.Id).Distinct().AsEnumerable();

            var pictures = await Db.SelectAsync<EstimatePicture>(p => estimateIds.Contains(p.EstimateId));


            messagesChats = messagesChats.Select(c =>
            {
                c.User = users.FirstOrDefault(u => u.Id == c.UserId);
                c.Estimate = properties.FirstOrDefault(e => e.Id == c.EstimateId);
                var pic = pictures.FirstOrDefault(x => x.EstimateId == c.EstimateId);

                if (pic != null)
                {
                    c.Estimate?.Pictures.Add(new PictureData
                    {
                        URL = $"{AwsBaseUrl}{AwsBucket}/estimates/{c.EstimateId}/{pic.PictureId:N}.jpg",
                        PictureType = pic.Type
                    });
                }

                return c;
            });

            return new MessageResponse { Chats = messagesChats.ToList() };
        }



    }
}
