/*/ nuget -\SSH.NET; /*/
using Renci.SshNet;
using System.Text.Json;
using System.Windows.Controls;

partial class AuDocs {
	public static void CompressAndUpload(string siteDir) {
		if (1 != dialog.show("Upload?", null, "1 Yes|2 No"/*, secondsTimeout: 5*/)) return;
		var tarDir = pathname.getDirectory(siteDir);
		_Compress(siteDir, tarDir);
		_Upload(tarDir);
	}
	
	static void _Compress(string siteDir, string tarDir) {
		var sevenZip = @"C:\Program Files\7-Zip\7z.exe";
		
		var tar = tarDir + @"\site.tar";
		filesystem.delete(tar);
		filesystem.delete(tarDir + @"\site.tar.gz");
		
		int r1 = run.console(out var s, sevenZip, $@"a ""{tar}""", siteDir); //add files to the root in the archive, not to "site" dir
		if (r1 != 0) { print.it(r1, s); return; }
		int r2 = run.console(out s, sevenZip, $@"a site.tar.gz site.tar", tarDir);
		if (r2 != 0) { print.it(r2, s); return; }
		
		filesystem.delete(tarDir + @"\site.tar");
		
		print.it("Compressed");
	}
	
	static void _Upload(string tarDir) {
		var j = _GetSshConnectionInfo(); if (j == null) return;
		
		//upload
		const string ftpDir = "domains/libreautomate.com/public_html", name = @"/site.tar.gz";
		var path = tarDir + name;
		using var ftp = new SftpClient(j.host, j.port, j.user, j.pass);
		ftp.Connect();
		ftp.ChangeDirectory(ftpDir);
		using (var stream = File.OpenRead(path)) {
			ftp.UploadFile(stream, pathname.getName(path));
		}
		print.it("Uploaded");
		
		//extract
		using var ssh = new SshClient(j.host, j.port, j.user, j.pass);
		ssh.Connect();
		_Cmd2("tar -zxf site.tar.gz");
		
		void _Cmd(string s, bool silent = false) {
			var c = ssh.RunCommand(s);
			//print.it($"ec={c.ExitStatus}, result={c.Result}, error={c.Error}");
			if (!silent && c.ExitStatus != 0) throw new Exception(c.Error);
		}
		
		//cd does not work when separate command
		void _Cmd2(string s, bool silent = false) => _Cmd($"cd {ftpDir} && {s}", silent);
		
		ftp.DeleteFile("site.tar.gz");
		filesystem.delete(path);
		
		print.it("<>Extracted to <link>https://www.libreautomate.com/</link>");
	}
	
	static SshConnectionInfo _GetSshConnectionInfo() {
		string rk = @"HKEY_CURRENT_USER\Software\Au", rv = "Docs";
		var j = JsonSerializer.Deserialize<SshConnectionInfo>(Registry.GetValue(rk, rv, "{}") as string);
		g1:
		if (j.host.NE() || j.port == 0 || j.user.NE() || j.pass.NE()) {
			var b = new wpfBuilder("SSH connection").WinSize(400);
			b.R.Add("IP", out TextBox tIp, j.host).Focus();
			b.R.Add("Port", out TextBox tPort, j.port.ToS());
			b.R.Add("User", out TextBox tUser, j.user);
			b.R.Add("Password", out TextBox tPass, j.pass.NE() ? null : Convert2.AesDecryptS(j.pass, "8470"));
			b.R.AddOkCancel();
			b.End();
#if WPF_PREVIEW //menu Edit -> View -> WPF preview
	b.Window.Preview();
#endif
			if (!b.ShowDialog()) return null;
			j.host = tIp.Text;
			j.port = tPort.Text.ToInt();
			j.user = tUser.Text;
			j.pass = Convert2.AesEncryptS(tPass.Text, "8470");
			Registry.SetValue(rk, rv, JsonSerializer.Serialize(j));
			goto g1;
		}
		j.pass = Convert2.AesDecryptS(j.pass, "8470");
		return j;
	}
	
	record SshConnectionInfo {
		public string host { get; set; }
		public int port { get; set; }
		public string user { get; set; }
		public string pass { get; set; }
	}
	
}
