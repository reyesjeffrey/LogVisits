namespace LogVisits.Models
{
    public class VisitorLog
    {
        public string id { get; set; } = string.Empty;
        public string ipAddress { get; set; } = string.Empty;
        public DateTime date { get; set; }
        public string pageVisited { get; set; } = string.Empty;
        public string browser { get; set; } = string.Empty;
        public string referrer { get; set; } = string.Empty;
        public string device { get; set; } = string.Empty;
    }
}
