using System;
using ServiceStack.DataAnnotations;

namespace HomingInWebservice.ServiceModel.Models
{
    public class EstimatePicture
    {

        [AutoIncrement]
        public ulong Id { get; set; }

        [Index]
        public UInt64 EstimateId { get; set; }
        public Guid PictureId { get; set; }
        public PictureType Type { get; set; }
    }
}

