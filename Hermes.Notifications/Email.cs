using System.Net;
using System.Net.Mail;
using System.Text;

namespace Hermes.Notifications
{
    public class Email
    {

        public static SmtpClient GetClient()
        {
            return new SmtpClient("localhost", 1025) // Konfiguration für MailHog, das auf localhost Port 1025 läuft
            {
                EnableSsl = false, // MailHog unterstützt kein SSL, daher auf false setzen
                DeliveryMethod = SmtpDeliveryMethod.Network, // E-Mails über das Netzwerk senden
                UseDefaultCredentials = false, // keine Standardanmeldeinformationen verwenden
                Credentials = null // MailHog benötigt keine Authentifizierung, daher leere Anmeldeinformationen
            };
        }

        public static MailMessage SetMail(string mailAdress, string mailName, string subject, string body)
        {
            return new MailMessage
            {
                From = new MailAddress(mailAdress, mailName), // Absenderadresse und Name
                Subject = subject, // Betreff der E-Mail
                Body = body, // HTML-Format für die E-Mail
                IsBodyHtml = true, // wichtig, damit der HTML-Inhalt korrekt dargestellt wird
                Priority = MailPriority.Normal,
                BodyEncoding = Encoding.UTF8, // wichtig für die korrekte Darstellung von Sonderzeichen
                SubjectEncoding = Encoding.UTF8, // wichtig für die korrekte Darstellung von Sonderzeichen im Betreff
                HeadersEncoding = Encoding.UTF8, // wichtig für die korrekte Darstellung von Sonderzeichen in den Headern
                Headers = { ["X-Mailer"] = "Hermes/1.0" } // benutzerdefinierter Header, z.B. um die E-Mail als von Hermes gesendet zu kennzeichnen
            };
        }


        public static async Task CreateTestMessage(CancellationToken cancellationToken = default)
        {
            using var client = GetClient();

            using var mail = SetMail(mailAdress: "hermes@test.com", mailName: "Hermes Newsletter", subject: "Test Newsletter", body: "<h1>Hallo!</h1>");

            mail.To.Add(new MailAddress("empfaenger@test.com", "Max Mustermann")); // Empfängeradresse und Name
            mail.ReplyToList.Add(new MailAddress("noreply@hermes.com", "Nicht antworten")); // wenn die Antworten mail an eine andere Adresse gehen sollen

            await client.SendMailAsync(mail, cancellationToken); // E-Mail asynchron senden
        }
        public static async Task CreateTestMessageBody(String mailbody, CancellationToken cancellationToken = default)
        {
            using var client = new SmtpClient("localhost", 1025) // Konfiguration für MailHog, das auf localhost Port 1025 läuft
            {
                EnableSsl = false, // MailHog unterstützt kein SSL, daher auf false setzen
                DeliveryMethod = SmtpDeliveryMethod.Network, // E-Mails über das Netzwerk senden
                UseDefaultCredentials = false, // keine Standardanmeldeinformationen verwenden
                Credentials = null // MailHog benötigt keine Authentifizierung, daher leere Anmeldeinformationen
            };

            using var mail = new MailMessage
            {
                From = new MailAddress("hermes@test.com", "Hermes Newsletter"), // Absenderadresse und Name
                Subject = "Test Newsletter", // Betreff der E-Mail
                Body = mailbody, // HTML-Format für die E-Mail
                IsBodyHtml = true, // wichtig, damit der HTML-Inhalt korrekt dargestellt wird
                Priority = MailPriority.Normal,
                BodyEncoding = Encoding.UTF8, // wichtig für die korrekte Darstellung von Sonderzeichen
                SubjectEncoding = Encoding.UTF8, // wichtig für die korrekte Darstellung von Sonderzeichen im Betreff
                HeadersEncoding = Encoding.UTF8, // wichtig für die korrekte Darstellung von Sonderzeichen in den Headern
                Headers = { ["X-Mailer"] = "Hermes/1.0" } // benutzerdefinierter Header, z.B. um die E-Mail als von Hermes gesendet zu kennzeichnen
            };

            mail.To.Add(new MailAddress("empfaenger@test.com", "Max Mustermann")); // Empfängeradresse und Name
            mail.ReplyToList.Add(new MailAddress("noreply@hermes.com", "Nicht antworten")); // wenn die Antworten mail an eine andere Adresse gehen sollen

            await client.SendMailAsync(mail, cancellationToken); // E-Mail asynchron senden
        }
    }
}
