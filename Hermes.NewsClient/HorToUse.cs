//using Hermes.NewsClient;
//using Hermes.NewsClient.DTOs;
//using System.Globalization;

//var culture = new CultureInfo("de-DE");

//var urlParts = new ApiUrlParts
//{
//    ApiKey = "KEY",
//    Countries = ["ch"],
//    Languages = ["de"],
//    Categories = ["politics"],
//    Timezone = "europe/berlin",
//    Image = 1,
//    RemoveDuplicate = 1,
//    Sort = "pubdateasc",
//    ExcludeField = "video_url,content,keywords,source_id,sentiment,sentiment_stats",
//};

//var newsClient = new NewsDataIoClient(new HttpClient());
//var result = await newsClient.GetLatestAsync(urlParts);

//var articles = result?.Results?.Take(10).ToList() ?? [];
//if (articles.Count == 0)
//{
//    Console.WriteLine("Keine Artikel von der API erhalten.");
//    return;
//}

//var composer = new NewsletterHtmlComposer();

//var dateDisplay = DateTime.Now.ToString("dddd, dd. MMMM yyyy", culture);
//var header = new NewsletterHeaderContent(
//    Header: "HERMES",
//    Header2: "Dein täglicher News-Überblick",
//    DateDisplay: dateDisplay,
//    Intro: "Guten Morgen! Hier sind die wichtigsten Nachrichten.");

//var itemModels = articles.Select(a => new NewsletterItemContent(
//    Category: a.Category?.FirstOrDefault() ?? "News",
//    Title: a.Title ?? string.Empty,
//    Content: a.Description ?? string.Empty,
//    Url: a.Link ?? "#",
//    ImageUrl: a.ImageUrl ?? string.Empty)).ToList();

//var footer = new NewsletterFooterContent(
//    InfoFooter: "Du erhältst diese E-Mail, weil du den Hermes Newsletter abonniert hast.",
//    DeaboUrl: "#",
//    SettingsUrl: "#");

//var htmlBody = await composer.BuildAsync(header, itemModels, footer);

//var emailSettings = new EmailSettings(
//    Host: "localhost",
//    Port: 1025,
//    EnableSsl: false,
//    Username: null,
//    Password: null,
//    DefaultFromAddress: "hermes@test.com",
//    DefaultFromName: "Hermes Newsletter",
//    DefaultReplyToAddress: "noreply@test.com",
//    DefaultReplyToName: "Hermes",
//    XMailer: "Hermes/1.0");

//IEmailSender sender = new SmtpEmailSender(emailSettings);

//var subject = $"Hermes Newsletter — {DateTime.Now.ToString("d", culture)}";

//var message = new EmailMessage(
//    To: new EmailRecipient(Address: "inbox@test.com", DisplayName: "Inbox"),
//    Subject: subject,
//    Body: htmlBody);

//await sender.SendAsync(message);

//Console.WriteLine($"Newsletter mit {articles.Count} Artikel(n) per SMTP gesendet (z. B. MailHog localhost:1025).");
