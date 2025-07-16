namespace GameServer.Utilities;

public static class TimestampUtility
{
    // Returns the current UTC timestamp in ISO 8601 format
    public static string GetTimestamp()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    // If you prefer local time instead of UTC
    public static string GetLocalTimestamp()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}