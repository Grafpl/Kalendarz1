using System;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

public class SmsSender
{
    //private const string accountSid = "";
    //private const string authToken = "";
    //private static readonly PhoneNumber from = new PhoneNumber("+");

    public static void SendSms(string destinationPhoneNumber, string messageBody)
    {
        TwilioClient.Init(accountSid, authToken);

        var to = new PhoneNumber(destinationPhoneNumber);

        var message = MessageResource.Create(
            body: messageBody,
            from: from,
            to: to
        );

        Console.WriteLine($"Message sent: {message.Sid}");
    }
}
