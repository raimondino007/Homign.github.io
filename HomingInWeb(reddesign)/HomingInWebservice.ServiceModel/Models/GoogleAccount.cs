using ServiceStack.DataAnnotations;

namespace HomingInWebservice.ServiceModel.Models
{
    public class GoogleAccount
    {
        [AutoIncrement]
        public ulong Id { get; set; }

        public string GoogleEmail { get; set; }
        public bool IsDisabled { get; set; }
    }
}
