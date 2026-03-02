namespace CandyCoat.Data;

public class GreeterBroadcast
{
    public string Label   { get; set; } = string.Empty; // "Rules", "DJ Info", "Event"
    public string Text    { get; set; } = string.Empty; // Message body
    public string Channel { get; set; } = "say";        // "say", "yell", "echo"
}
