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
		
		filesystem.delete(tarDir + @"\site.tar");
		filesystem.delete(tarDir + @"\site.tar.gz");
		
		int r1 = run.console(out var s, sevenZip, $@"a site.tar .\site", tarDir);
		if (r1 != 0) { print.it(s); return; }
		int r2 = run.console(out s, sevenZip, $@"a site.tar.gz site.tar", tarDir);
		if (r2 != 0) { print.it(s); return; }
		
		filesystem.delete(tarDir + @"\site.tar");
		
		print.it("Compressed");
	}
	
	static void _Upload(string tarDir) {
		var j = _GetSshConnectionInfo(); if (j == null) return;
		
		//upload. Use SFTP. Maybe possible with SSH, but I tried scp command and failed.
		const string ftpDir = "public_html/au", name = @"/site.tar.gz";
		var path = tarDir + name;
		using (var client = new SftpClient(j.ip, j.port, j.user, j.pass)) {
			client.Connect();
			client.ChangeDirectory(ftpDir);
			using var stream = File.OpenRead(path);
			client.UploadFile(stream, pathname.getName(path));
		}
		filesystem.delete(path);
		print.it("Uploaded");
		
		//extract
		//perf.first();
		using (var client = new SshClient(j.ip, j.port, j.user, j.pass)) {
			client.Connect();
			//_Cmd("cd public_html/test"); _Cmd("pwd"); //cd does not work when separate command. Not tested: ShellStream.
			//_Cmd("cd public_html/test && pwd"); //ok
			//perf.next();
			_Cmd2("tar -zxf site.tar.gz");
			_Cmd2("rm -r help", silent: true);
			_Cmd2("mv site help");
			_Cmd2("rm site.tar.gz", silent: true);
			//perf.nw(); //tar ~20 s, others fast
			client.Disconnect();
			
			void _Cmd(string s, bool silent = false) {
				var c = client.RunCommand(s);
				//print.it($"ec={c.ExitStatus}, result={c.Result}, error={c.Error}");
				if (!silent && c.ExitStatus != 0) throw new Exception(c.Error);
			}
			
			void _Cmd2(string s, bool silent = false) => _Cmd($"cd {ftpDir} && {s}", silent);
		}
		print.it("<>Extracted to <link>https://www.quickmacros.com/au/help/</link>");
	}
	
	static SshConnectionInfo _GetSshConnectionInfo() {
		string rk = @"HKEY_CURRENT_USER\Software\Au", rv = "Docs";
		var j = JsonSerializer.Deserialize<SshConnectionInfo>(Registry.GetValue(rk, rv, "{}") as string);
		g1:
		if (j.ip.NE() || j.port == 0 || j.user.NE() || j.pass.NE()) {
			var b = new wpfBuilder("SSH connection").WinSize(400);
			b.R.Add("IP", out TextBox tIp, j.ip).Focus();
			b.R.Add("Port", out TextBox tPort, j.port.ToS());
			b.R.Add("User", out TextBox tUser, j.user);
			b.R.Add("Password", out TextBox tPass);
			b.R.AddOkCancel();
			b.End();
#if WPF_PREVIEW //menu Edit -> View -> WPF preview
	b.Window.Preview();
#endif
			if (!b.ShowDialog()) return null;
			j.ip = tIp.Text;
			j.port = tPort.Text.ToInt();
			j.user = tUser.Text;
			j.pass = Convert.ToBase64String(Encoding.UTF8.GetBytes(tPass.Text));
			j.pass = Convert2.AesEncryptS(tPass.Text, "8470");
			Registry.SetValue(rk, rv, JsonSerializer.Serialize(j));
			goto g1;
		}
		j.pass = Convert2.AesDecryptS(j.pass, "8470");
		return j;
	}
	
	record SshConnectionInfo {
		public string ip { get; set; }
		public int port { get; set; }
		public string user { get; set; }
		public string pass { get; set; }
	}
	
}
