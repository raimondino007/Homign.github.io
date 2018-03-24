using System;
using ServiceStack.DataAnnotations;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HomingInWebservice.ServiceModel.Models
{
    public class ConversationMessage
    {
        [AutoIncrement]
        public UInt64 Id { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public UInt64 FromUserId { get; set; }
        public UInt64 ToUserId { get; set; }
        public UInt64 PropertyId { get; set; }
        public DateTime Date { get; set; }
        public bool IsActive { get; set; }
        public bool IsRead { get; set; }
    }
}
