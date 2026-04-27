//using Hermes.Notifications.Receiving;
//using Hermes.Notifications.Receiving.Models;
//using Hermes.Notifications.Sending;
//using Hermes.Notifications.Sending.Models;

//var app = new HermesMailConsole();
//await app.SendOneMailAsync().ConfigureAwait(false); // configureawait is not strictly necessary here, but it's a good practice to avoid potential deadlocks in more complex applications
////await app.LoadAllMailsAndPrintHeadersAsync().ConfigureAwait(false);

///// <summary>
///// Minimal console demo: send via MailHog SMTP, then list stored messages (header fields) from the MailHog API.
///// </summary>
//internal sealed class HermesMailConsole
//{
//    private readonly EmailSettings _smtp = new(
//        Host: "localhost",
//        Port: 1025,
//        EnableSsl: false,
//        Username: null,
//        Password: null,
//        DefaultFromAddress: "hermes@test.com",
//        DefaultFromName: "Hermes",
//        DefaultReplyToAddress: "noreply@test.com",
//        DefaultReplyToName: "No Reply",
//        XMailer: "Hermes/1.0");

//    private readonly MailHogSettings _mailHog = new() { BaseUrl = "http://localhost:8025" };

//    /// <summary>
//    /// Sends a single test message to MailHog (SMTP).
//    /// </summary>
//    public async Task SendOneMailAsync()
//    {
//        IEmailSender sender = new SmtpEmailSender(_smtp);

//        var newsletterBody = await File.ReadAllTextAsync("C:\\Users\\Melina\\source\\repos\\Hermes\\Hermes.Notifications\\Sending\\Newsletter.html");



//        var message = new EmailMessage(
//            To: new EmailRecipient(Address: "inbox@test.com", DisplayName: "Inbox"),
//            Subject: "Hermes basic send",
//            Body: newsletterBody);

//        await sender.SendAsync(message).ConfigureAwait(false);
//        Console.WriteLine("E-Mail gesendet.");
//    }

//    /// <summary>
//    /// Loads all messages from MailHog and writes the main header fields line by line to the console.
//    /// </summary>
//    public async Task LoadAllMailsAndPrintHeadersAsync()
//    {
//        using var receiver = new MailHogEmailReceiver(_mailHog);
//        var mails = await receiver.GetAllAsync().ConfigureAwait(false);

//        var index = 0;
//        foreach (var mail in mails)
//        {
//            index++;
//            Console.WriteLine($"--- Nachricht {index} ---");
//            Console.WriteLine($"Message-Id: {mail.Id}");
//            Console.WriteLine($"From: {mail.From}");
//            Console.WriteLine($"To: {mail.To}");
//            Console.WriteLine($"Subject: {mail.Subject}");
//            Console.WriteLine($"Date: {mail.ReceivedAt:O}");
//            Console.WriteLine();
//        }

//        Console.WriteLine($"Insgesamt {index} Nachricht(en).");
//    }
//}
