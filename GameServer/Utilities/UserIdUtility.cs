namespace GameServer.Utilities;

public static class UserIdUtility
{
    // Standard UUID format
    public static string GenerateGuidUserId()
    {
        return Guid.NewGuid().ToString(); // e.g. "f47ac10b-58cc-4372-a567-0e02b2c3d479"
    }

    // Shorter base64 format (22 characters)
    public static string GenerateBase64UserId()
    {
        var guidBytes = Guid.NewGuid().ToByteArray();
        return Convert.ToBase64String(guidBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('='); // URL-safe
    }
}