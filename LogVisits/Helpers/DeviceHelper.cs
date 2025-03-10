namespace LogVisits.Helpers
{
    public static class DeviceHelper
    {
        public static string GetDeviceType(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
                return "Desktop";

            userAgent = userAgent.ToLower();

            if (userAgent.Contains("mobile"))
                return "Mobile";

            if (userAgent.Contains("tablet") || userAgent.Contains("ipad"))
                return "Tablet";

            return "Desktop";
        }
    }
}
