namespace LA;

class DocsHttpServer : HttpServerSession {
	static bool s_running;
	static int s_port;
	
	public static void StartOrSwitch() {
		if (!App.Settings.localDocumentation) {
			HelpUtil.AuHelpBaseUrl = HelpUtil.AuHelpBaseUrlDefault_;
		} else if (s_running) {
			HelpUtil.AuHelpBaseUrl = $"http://127.0.0.1:{s_port}/";
		} else {
			s_running = true;
			run.thread(() => {
				try {
					_AutoDownloadDocs();
					HttpServerSession.Listen<DocsHttpServer>(0, "127.0.0.1", listener => {
						s_port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
						HelpUtil.AuHelpBaseUrl = $"http://127.0.0.1:{s_port}/";
					});
				}
				catch (Exception ex) {
					print.warning(ex);
					print.it("<><c red>Failed to install local documentation. You can retry: in <b>Options > Other<> select <b>Documentation > Local<>, and click <b>Apply<>. Meanwhile will be used online documentation.<>");
					HelpUtil.AuHelpBaseUrl = HelpUtil.AuHelpBaseUrlDefault_;
					App.Settings.localDocumentation = false;
				}
				s_running = false;
			}, sta: false);
		}
	}
	
	protected override void MessageReceived(HSMessage m, HSResponse r) {
		if (m.Method != "GET") { r.Status = System.Net.HttpStatusCode.MethodNotAllowed; return; }
		
		var siteDir = folders.ThisAppDataCommon + "docs";
		var path = m.TargetPath; if (path.Ends('/')) path += "index.html";
		path = siteDir + path;
		if (!filesystem.exists(path).File) { r.Status = System.Net.HttpStatusCode.NotFound; return; }
		
		r.Content = filesystem.loadBytes(path);
	}
	
	static void _AutoDownloadDocs() {
		var siteDir = folders.ThisAppDataCommon + "docs";
		var versionFile = siteDir + @"\version.txt";
		if (filesystem.exists(versionFile) && File.ReadAllLines(versionFile)[0] == Au_.Version) return;
		
		print.it($"<><lc YellowGreen>Installing local documentation for {App.AppName} {Au_.Version}.<>");
		if (filesystem.exists(siteDir)) {
			print.it("Deleting old documentation");
			filesystem.delete(versionFile);
			filesystem.delete(siteDir);
		}
		
		print.it("Downloading documentation from libreautomate.com, ~3 MB");
		var zipPath = siteDir + @"\site.tar.bz2";
		internet.http.Get($"https://www.libreautomate.com/download/doc/{Au_.Version}.tar.bz2", zipPath);
		
		print.it($"Extracting documentation to {siteDir}");
		var sevenzip = folders.ThisAppBS + @"32\7za.exe";
		if (0 != run.console(out var s, sevenzip, $@"x -aoa -o""{siteDir}"" ""{zipPath}""")) throw new AuException($"*extract {zipPath}. {s}");
		filesystem.delete(zipPath);
		zipPath = zipPath[..^4];
		if (0 != run.console(out s, sevenzip, $@"x -aoa -o""{siteDir}"" ""{zipPath}""")) throw new AuException($"*extract {zipPath}. {s}");
		filesystem.delete(zipPath);
		
		filesystem.saveText(versionFile, Au_.Version);
		print.it("DONE.");
	}
}
