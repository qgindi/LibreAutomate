// file copied from Other\BuildEvents 2/23/25

/*/ nuget -\SSH.NET; /*/
using Microsoft.Win32;
using Renci.SshNet;
using System.Text.Json;
using System.Windows.Controls;


static class Sftp {
	public static SftpConnectionInfo GetConnectionInfo(string where = "libreautomate.com") {
		string rk = @"HKEY_CURRENT_USER\Software\Au";
		var j = JsonSerializer.Deserialize<SftpConnectionInfo>(Registry.GetValue(rk, where, "{}") as string);
		g1:
		if (j.host.NE() || j.port == 0 || j.user.NE() || j.pass.NE()) {
			var b = new wpfBuilder("SSH connection").WinSize(400);
			b.R.Add("IP", out TextBox tIp, j.host).Focus(); //note: use IP. With hostname fails when using CloudFlare CDN.
			b.R.Add("Port", out TextBox tPort, j.port.ToS());
			b.R.Add("User", out TextBox tUser, j.user);
			b.R.Add("Password", out TextBox tPass, j.pass.NE() ? null : Convert2.AesDecryptS(j.pass, "8470"));
			b.R.AddOkCancel();
			b.End();
			if (!b.ShowDialog()) return null;
			j.host = tIp.Text;
			j.port = tPort.Text.ToInt();
			j.user = tUser.Text;
			j.pass = Convert2.AesEncryptS(tPass.Text, "8470");
			Registry.SetValue(rk, where, JsonSerializer.Serialize(j));
			goto g1;
		}
		j.pass = Convert2.AesDecryptS(j.pass, "8470");
		return j;
	}
	
	public static void ConnectAndUpload(this SftpClient ftp, string ftpDir, string localFile) {
		ftp.Connect();
		ftp.ChangeDirectory(ftpDir);
		using var stream = File.OpenRead(localFile);
		ftp.UploadFile(stream, pathname.getName(localFile));
	}
	
	/// <summary>
	/// Uploads a file to libreautomate.com (IP 194.5.156.231).
	/// </summary>
	/// <param name="ftpDir">Eg <c>"domains/libreautomate.com/public_html"</c></param>
	/// <param name="localFile"></param>
	public static void UploadToLA(string ftpDir, string localFile) {
		var ci = Sftp.GetConnectionInfo(); if (ci == null) return;
		using var ftp = new SftpClient(ci.host, ci.port, ci.user, ci.pass);
		ConnectAndUpload(ftp, ftpDir, localFile);
	}
}

record SftpConnectionInfo {
	public string host { get; set; }
	public int port { get; set; }
	public string user { get; set; }
	public string pass { get; set; }
}
