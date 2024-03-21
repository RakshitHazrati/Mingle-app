using System;
using System.Net.Mail;
using System.Net;

namespace MauiApp1;

public class EmailService
{
    private readonly SmtpClient smtpClient;
    private readonly string fromAddress;

    public EmailService()
	{
        smtpClient = new SmtpClient("smtp.gmail.com")
        {
            Port = 587,
            Credentials = new NetworkCredential(),
            EnableSsl = true,
        };
        fromAddress = "rakshithazrati@gmail.com";
    }

    public void SendOTPMail(string toAddress, string otp)
    {
        var mailMessage = new MailMessage
        {
            From = new MailAddress(fromAddress),
            Subject = "Your OTP",
            Body = "Your OTP is : " + otp
        };
        mailMessage.To.Add(toAddress);

        smtpClient.Send(mailMessage);
    }
}
