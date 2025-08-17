namespace SharedLibrary.Requests;

[Obsolete("PlayerPositionMessage is deprecated, use PlayerPing instead.")]
public class PlayerPositionMessage
{
    public float X { get; set; }
    public float Y { get; set; }
}
