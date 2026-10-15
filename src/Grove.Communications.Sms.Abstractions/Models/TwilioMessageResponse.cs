namespace Grove.Communications.Sms.Models;

public class TwilioMessageResponse
{
    public string Status { get; set; }

    public string ErrorMessage { get; set; }

    public string ErrorCode { get; set; }
}
