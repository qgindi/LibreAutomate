/// Library info: <link https://github.com/jstedfast/MailKit>MailKit<>, <link https://github.com/jstedfast/MailKit/blob/master/FAQ.md>FAQ<>. NuGet: <+nuget>MailKit<>.
/// 
/// This recipe shows how to connect to Gmail and send/receive. See also Gmail setup in recipe <+recipe>send email<>.

/*/ nuget -\MailKit; /*/

using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MailKit;
using MailKit.Net.Imap;

/// Send message.

var message = new MimeMessage();
message.From.Add(new MailboxAddress("Cat", "from@email.com"));
message.To.Add(new MailboxAddress("Mouse", "to@email.com"));
message.Subject = "MailKit";
message.Body = new TextPart("plain") { Text = @"message text" };

using (var client = new SmtpClient()) {
	client.CheckCertificateRevocation = false;
	client.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
	client.Authenticate("username", "password");
	client.Send(message);
	client.Disconnect(true);
}

/// Receive messages.

using (var client = new ImapClient()) {
	client.CheckCertificateRevocation = false;
	client.Connect("imap.googlemail.com", 993, true);
	client.Authenticate("username", "password");

	var inbox = client.Inbox;
	inbox.Open(FolderAccess.ReadOnly);
	//inbox.Open(FolderAccess.ReadWrite);

	print.it($"Count {inbox.Count}");

	foreach (var m in inbox.Fetch(0, -1, MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.UniqueId | MessageSummaryItems.PreviewText)) {
		//if(m.Flags.Value.Has(MessageFlags.Seen)) continue;
		print.it($"<><Z #C0C0ff>{m.Index}. {m.Envelope.Subject}   {m.Envelope.From}<>");
		print.it(m.PreviewText);
		//if (!m.Flags.Value.Has(MessageFlags.Seen)) {
		//	var M = inbox.GetMessage(m.UniqueId);
		//	print.it(M.TextBody);
			
		//	//switch (m.Envelope.Subject) {
		//	//case "Spam845209394645200026":
		//	//	print.it("spam");
		//	//	inbox.MoveTo(m.UniqueId, client.GetFolder(SpecialFolder.Junk));
		//	//	continue;
		//	//}
		//}
	}

	client.Disconnect(true);
}
