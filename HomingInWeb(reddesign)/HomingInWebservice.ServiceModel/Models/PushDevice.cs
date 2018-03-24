using System;
using ServiceStack.DataAnnotations;

namespace HomingInWebservice.ServiceModel.Models
{
    public enum DeviceType
    {
        Unknown = 0,
        iOS,
        Android
    }

    public class PushDevice
    {
        [AutoIncrement]
        public ulong Id { get; set; }

        [Index]
        public UInt64 UserId { get; set; }
        [Index]
        public string DeviceId { get; set; }
        public DeviceType DeviceType { get; set; }
    }
}

