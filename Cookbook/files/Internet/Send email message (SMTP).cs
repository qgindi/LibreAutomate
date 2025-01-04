/// Use class <see cref="SmtpClient"/>.

using System.Net.Mail;
using System.Net;

/// You need an email account, for example Gmail.
/// Gmail account setup: in account settings create an app. Then in code use username/password of that app. Or enable "less secure app access".

using var client = new SmtpClient("smtp.gmail.com", 587) {
	Credentials = new NetworkCredential("username", "password"),
	EnableSsl = true,
	Timeout = 5_000, //use 5 s for testing; default 100 s
};

/// Send simple message.

string from = "my@email.com";
string to = "recipient@email.com";

client.Send(from, to, "Test1", "message text");

/// Send message with HTML text and attachments.

var m = new MailMessage(from, to, "Test2", "message <b>text</b>") { IsBodyHtml = true };
m.Attachments.Add(new(@"C:\Test\test.txt"));
client.Send(m);

/// Look for more info/examples on the Internet. See also recipe <+recipe>Send and receive email<>.
