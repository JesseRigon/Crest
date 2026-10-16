namespace Grove.Integrations.Twilio.Models;

/// <summary>The part of Twilio's message resource (or error) the provider reads.</summary>
public class TwilioMessageResponse
{
    public string Sid { get; set; }

    public string Status { get; set; }

    public string ErrorMessage { get; set; }

    public int? ErrorCode { get; set; }

    /// <summary>An error answer's code.</summary>
    public int? Code { get; set; }

    /// <summary>An error answer's message.</summary>
    public string Message { get; set; }
}
