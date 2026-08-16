namespace SmsWorkbench
{
    public static class AccessTokenState
    {
        public static string Display(bool hasAccessToken, string probeStatusCode)
        {
            if (!hasAccessToken) return "Tidak Diperoleh";
            return string.Equals((probeStatusCode ?? "").Trim(), "401", System.StringComparison.OrdinalIgnoreCase)
                ? "401 tidak berlaku"
                : "Telah Diperoleh";
        }
    }
}
