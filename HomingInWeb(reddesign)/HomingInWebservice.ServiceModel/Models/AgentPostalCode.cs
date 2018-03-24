using ServiceStack.DataAnnotations;

namespace HomingInWebservice.ServiceModel.Models
{
    public class AgentPostalCode
    {
        [AutoIncrement]
        public ulong Id { get; set; }

        public ulong UserId { get; set; }
        public string PostalCode { get; set; }
    }
}
