using System;
using ServiceStack;
using System.Collections.Generic;
using HomingInWebservice.ServiceModel.Models;

namespace HomingInWebservice.ServiceModel
{
    [Route("/message/{PropertyId}/{ToUserId}")]
    public class MessageRequest : IReturn<MessageResponse>
    {
        public UInt64 ToUserId { get; set; }
        public UInt64 PropertyId { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public DateTime Date { get; set; }
    }

    [Route("/messageChats")]
    public class MessageUsersRequest : IReturn<MessageResponse>
    {
        public DateTime? Date { get; set; }
    }

    public class MessageResponse
    {
        public List<ConversationMessage> Message { get; set; }
        public List<User> Users { get; set; }
        public List<Estimate> Estimate { get; set; }
        public List<ConversationChat> Chats { get; set; }
        public ResponseStatus ResponseStatus { get; set; }
    }

    public class ConversationChat
    {
        public UInt64 UserId { get; set; }
        public User User { get; set; }
        public UInt64 EstimateId { get; set; }
        public Estimate Estimate { get; set; }
    }

}

