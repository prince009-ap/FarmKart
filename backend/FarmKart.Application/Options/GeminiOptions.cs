namespace FarmKart.Application.Options;

public class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.6-flash";
    public int TimeoutSeconds { get; set; } = 30;
}
