using System;

namespace Pitech.XR.ContentDelivery
{
    public static class Timestamp
    {
        public static string UtcNowIso8601()
        {
            return DateTime.UtcNow.ToString("o");
        }
    }
}
