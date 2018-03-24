using System.Runtime.Serialization;

namespace HomingInWebservice.ServiceModel.Models
{
    public enum PictureType
    {
        Unknown,
        Profile,
        FrontOfHouse,
        RearOfHouse,
        Kitchen,
        LivingRoom,
        MasterBath,
        View,
        Neighborhood,
        Other
    }

    public class PictureData
    {
        public ulong? Id { get; set; }
        public PictureType PictureType { get; set; }
        public string URL { get; set; }
        [IgnoreDataMember]
        public byte[] Bytes { get; set; }

    }
}
