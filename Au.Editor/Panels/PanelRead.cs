extern alias CAW;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.Runtime.Loader;
using Au.Controls;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using System.Text.Json.Nodes;

namespace LA;

class PanelRead {
	WebView2 _wv;
	string _localBaseUri;
	
	//public PanelRead() {
	//	//P.UiaSetName("Read panel"); //no UIA element for Panel
	//}
	
	public Grid P { get; } = new();
	
	void _BeforeOpeningArticle() {
		var p = Panels.PanelManager[P];
		p.Visible = true;
		if (p.Floating) {
			wnd w = p.Content.Hwnd(), wmain = App.Hmain;
			if (w.IsMinimized) {
				w.ShowNotMinimized();
			} else if (!w.IsOwnedBy(wmain, 0) && !w.IsTopmost) {
				if (w.ClientRectInScreen.IntersectsWith(wmain.ClientRectInScreen)) w.ActivateL(true);
			}
		}
	}
	
	/// <summary>
	/// Opens a LA documentation page in this panel.
	/// Opens the local version of the page.
	/// If cannot open (eg WebView2 unavailable on current computer), opens the specified URL in default web browser app.
	/// </summary>
	/// <param name="url">Like <c>"https://www.libreautomate.com/articles/name.html"</c>.</param>
	public void OpenDocUrl(string url) {
		var baseUri = DocsHttpServer.LocalBaseUri;
		if (baseUri == null || !_IsDocsUrl(url, c_laWebsiteBaseUri) || !_IsWebViewAvailable) {
			run.itSafe(url);
			return;
		}
		_localBaseUri = baseUri; //save because DocsHttpServer.LocalBaseUri may become null
		url = string.Concat(baseUri, url.AsSpan(30));
		
		_BeforeOpeningArticle();
		if (_wv == null) _CreateWebView();
		_wv.Source = new(url);
	}
	
	const string c_laWebsiteBaseUri = "https://www.libreautomate.com/";
	
	static bool _IsDocsUrl(string url, string baseUri)
		=> url.Starts(baseUri) && url.Eq(baseUri.Length, false, "api", "editor", "articles", "cookbook") > 0;
	
	static bool _IsWebViewAvailable {
		get {
			if (s_wvAvailable == null) {
				try {
					CoreWebView2Environment.SetLoaderDllFolderPath(folders.ThisAppBS + $@"runtimes\win-{(osVersion.isArm64Process ? "arm64" : "x64")}\native"); //else would fail or use PATH etc
					
					string ver = CoreWebView2Environment.GetAvailableBrowserVersionString();
					s_wvAvailable = !ver.NE();
				}
				catch { s_wvAvailable = false; }
				if (s_wvAvailable == false) print.it("<>Info: To show documentation in <b>Read<> panel, download/install <google WebView2 site:microsoft.com>WebView2<>. Then restart this app. See <b>Options > Other<>.");
			}
			return s_wvAvailable == true;
		}
	}
	static bool? s_wvAvailable;
	
	void _CreateWebView() {
		var tb = Panels.Help.buttons_.toolbar.ToolBars[0];
		Panels.Help.buttons_.back = tb.AddButton("*EvaIcons.ArrowBack" + EdIcons.black, _ => { try { _wv.GoBack(); } catch { } }, "Back", enabled: false);
		Panels.Help.buttons_.forward = tb.AddButton("*EvaIcons.ArrowForward" + EdIcons.black, _ => { try { _wv.GoForward(); } catch { } }, "Forward", enabled: false);
		Panels.Help.buttons_.openInBrowser = tb.AddButton("*Modern.Browser" + EdIcons.black, _ => { _OpenInWebBrowser(); }, "Open in web browser", enabled: false);
		tb.Items.Add(new Separator());
		Panels.Help.buttons_.toggleReadPanel = tb.AddButton("*PixelartIcons.Article" + EdIcons.black, _ => { Panels.PanelManager[P].Visible ^= true; }, "Show/hide the Read panel");
		
		string udf = folders.ThisAppTemp + "wv";
		filesystem.delete(udf, FDFlags.CanFail); //would fail to init if it's corrupt. And it grows.
		_wv = new() {
			CreationProperties = new() { UserDataFolder = udf }
		};
		P.Children.Add(_wv);
		
		_wv.CoreWebView2InitializationCompleted += (_, e) => {
			if (!e.IsSuccess) {
				print.warning("Failed to initialize WebView2. Can't show LA documentation in the Read panel. See <+options Other>Options > Other<>. " + e.InitializationException);
				s_wvAvailable = false;
				return;
			}
			
			Panels.Help.buttons_.toolbar.Visibility = Visibility.Visible;
			
			var core = _wv.CoreWebView2;
			
			Debug.Assert(!core.Environment.UserDataFolder.Starts(folders.ThisApp)); //ignores CreationProperties if it was set too early
			
			core.HistoryChanged += (_, _) => {
				Panels.Help.buttons_.back.IsEnabled = _wv.CanGoBack;
				Panels.Help.buttons_.forward.IsEnabled = _wv.CanGoForward;
				Panels.Help.buttons_.openInBrowser.IsEnabled = true;
			};
			
			core.NavigationStarting += (_, e) => {
				var url = e.Uri;
				if (url.Starts("la-link:nuget/")) {
					e.Cancel = true;
					DNuget.ShowSingle(Uri.UnescapeDataString(url[14..]));
				} else if (!_IsDocsUrl(url, _localBaseUri)) {
					e.Cancel = true;
					run.itSafe(url);
				}
			};
			
			core.ContextMenuRequested += _ContextMenuRequested;
			
#if DEBUG
			_PreviewCurrentRecipeScript();
#endif
		};
	}
	
	/// <summary>
	/// Opens the currently displayed page in the default web browser app.
	/// </summary>
	void _OpenInWebBrowser() {
		if (_wv.Source is { } uri) {
			var url = uri.ToString();
			if (App.Settings.doc_web_la) if (_IsDocsUrl(url, _localBaseUri)) url = c_laWebsiteBaseUri + url[_localBaseUri.Length..];
			run.itSafe(url);
		}
	}
	
	async void _ContextMenuRequested(object sender, CoreWebView2ContextMenuRequestedEventArgs e) {
		var defer = e.GetDeferral();
		try {
			var items = e.MenuItems;
			
			for (int i = items.Count; --i >= 0;) {
				if (items[i].Kind == CoreWebView2ContextMenuItemKind.Command && items[i].Name is "inspectElement" or "webCapture" or "webSelect" or "createQrCode")
					items.RemoveAt(i);
				//if need Inspect, Ctrl+Shit+I still works
			}
			
			var core = _wv.CoreWebView2;
			
			//detect <pre> and get its text
			string preText = null;
			var cmt = e.ContextMenuTarget;
			if (cmt.Kind == CoreWebView2ContextMenuTargetKind.Page && !(cmt.HasLinkUri || cmt.IsEditable)) {
				string js = $$"""
(function() {
    var el = document.elementFromPoint({{e.Location.X}}, {{e.Location.Y}});
    while(el && el.nodeType === 1) {
        if(el.tagName === 'PRE') return el.textContent;
        el = el.parentElement;
    }
    return null;
})();
""";
				try {
					preText = await core.ExecuteScriptAsync(js);
					if (preText != null) preText = (string)JsonValue.Parse(preText);
				}
				catch (Exception ex) { print.it(ex); }
			}
			
			var mCC = core.Environment.CreateContextMenuItem("Copy code", null, CoreWebView2ContextMenuItemKind.Command);
			var mNS = core.Environment.CreateContextMenuItem("New script", null, CoreWebView2ContextMenuItemKind.Command);
			if (preText != null) {
				mCC.CustomItemSelected += (_, __) => { clipboard.text = preText; };
				mNS.CustomItemSelected += (_, __) => {
					var name = pathname.getNameNoExt(Uri.UnescapeDataString(_wv.Source.AbsolutePath));
					App.Model.NewItem("Script.cs", null, name + ".cs", true, new(true, preText));
				};
			} else {
				mCC.IsEnabled = mNS.IsEnabled = false;
			}
			items.Insert(0, mCC);
			items.Insert(1, mNS);
			try { items.Insert(2, core.Environment.CreateContextMenuItem(null, null, CoreWebView2ContextMenuItemKind.Separator)); } catch { } //throws on Win7
			
			var mOB = core.Environment.CreateContextMenuItem("Open in browser", null, CoreWebView2ContextMenuItemKind.Command);
			mOB.CustomItemSelected += (_, __) => { _OpenInWebBrowser(); };
			try { items.Add(core.Environment.CreateContextMenuItem(null, null, CoreWebView2ContextMenuItemKind.Separator)); } catch { }
			items.Add(mOB);
		}
		finally {
			defer.Complete();
		}
	}
	
#if DEBUG
	/// <summary>
	/// Starts a timer that displays HTML (preview) of the current C# script, if it's a cookbook recipe source script.
	/// It makes editing cookbook recipes easy.
	/// </summary>
	void _PreviewCurrentRecipeScript() {
		string prevText = null;
		SciCode prevDoc = null;
		
		App.Timer1sWhenVisible += () => {
			if (!P.IsVisible || DocsHttpServer.LocalBaseUri == null) return;
			if (App.Model.WorkspaceName != "Cookbook") {
				if (!(App.Model.WorkspaceName == "ok" && App.Model.CurrentFile is { IsExternal: true, IsScript: true } cf && cf.Ancestors().Any(o => o.Name is "cookbook_files"))) return;
			}
			var doc = Panels.Editor.ActiveDoc;
			if (doc == null || !doc.EFile.IsScript || doc.EFile.Parent.Name == "-") return;
			
			string text = doc.aaaText;
			if (text == prevText) return;
			if (_IsCaretInPossiblyUnfinishedTag(doc, text)) return; //avoid printing debug info for invalid links etc
			
			if (_wv.Source.AbsolutePath != "/cookbook/preview.html") {
				prevText = null;
				prevDoc = doc;
				_wv.Source = new(DocsHttpServer.LocalBaseUri + "cookbook/preview.html");
				//DocsHttpServer will call GetPreviewHtmlTemplate_() and the control will display it (empty page).
				//In next timer call (the `else` code) its <article>'s inner HTML will be replaced with the HTML returned by _GetPreviewHtml.
			} else {
				var html = _GetPreviewHtml();
				var json = JsonValue.Create(html).ToJsonString();
				_wv.CoreWebView2.ExecuteScriptAsync($"window.updatePreview({json});{(doc != prevDoc ? "scrollToTop()" : null)}");
				prevDoc = doc;
				prevText = text;
			}
		};
		
		bool _IsCaretInPossiblyUnfinishedTag(SciCode doc, string text) {
			int i = doc.aaaCurrentPos16;
			i = i == 0 ? -1 : text.LastIndexOfAny(['<', '>', '\n'], i - 1);
			if (i > 0 && text[i] == '<' && text.Eq(text.LastIndexOf('\n', i) + 1, "///")) {
				//print.it("tag");
				return true;
			}
			return false;
		}
	}
	
	/// <summary>
	/// Converts text of the current C# script to HTML. It must be a cookbook recipe source script.
	/// Uses the same converter as the "Au docs" script (it creates HTML etc files for the website).
	/// </summary>
	static string _GetPreviewHtml() {
		try {
			var doc = Panels.Editor.ActiveDoc;
			string name = doc.EFile.DisplayName, code = doc.Dispatcher.Invoke(() => doc.aaaText);
			
			if (_RecipeCodeToHtml == null) {
				AssemblyLoadContext.Default.LoadFromAssemblyPath(@"C:\code\ok\.nuget\-\Markdig.dll");
				var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(@"C:\code\ok\dll\AuDocsLib.dll");
				_RecipeCodeToHtml = asm.GetType("ADL.AuDocsShared").GetMethod("RecipeCodeToHtml").CreateDelegate<Func<string, string, string>>();
			}
			return _RecipeCodeToHtml(name, code);
		}
		catch (Exception ex) { print.it(ex); throw; }
	}
	
	static Func<string, string, string> _RecipeCodeToHtml;
	
	/// <summary>
	/// Called by the HTTP server. The code is here because it's better to keep most of the preview code in one place.
	/// </summary>
	internal static string GetPreviewHtmlTemplate_() {
		return """
<!DOCTYPE html>
<html>
  <head>
    <meta charset="utf-8">
      <meta http-equiv="X-UA-Compatible" content="IE=edge,chrome=1">
      <title>Constants, enum | LibreAutomate </title>
      <meta name="viewport" content="width=device-width">
      <link rel="stylesheet" href="../styles/docfx.vendor.min.css">
      <link rel="stylesheet" href="../styles/docfx.css">
      <link rel="stylesheet" href="../styles/main.css">
      <link rel="stylesheet" href="../styles/code.css">
  <link rel="stylesheet" href="../styles/la.css">
</head>
  <body>
    <a name="top"></a>
    <div id="wrapper">
      <div role="main" class="container-fluid body-content hide-when-search">
          <div class="col-md-12">
            <article class="content wrap" id="_content"/>
          </div>
      </div>
    </div>
<script>
window.updatePreview = function(html) {
    document.getElementById('_content').innerHTML = html;
};
window.scrollToTop = function() {
    window.scrollTo(0, 0);
};
</script>
</body>
</html>
""";
	}
#endif
	
	internal static void UriProtocol_(string s) {
		if (s.Starts("nuget/")) DNuget.ShowSingle(Uri.UnescapeDataString(s[6..]));
	}
}

/// <summary>
/// Used by the Read panel. Retrieves local LA documentation files.
/// Probably it's the best way to display local HTML files. Other ways have problems.
/// </summary>
class DocsHttpServer : HttpServerSession {
	static bool s_running;
	static int s_port;
	
	public static void StartOrSwitch() {
		if ((App.Settings.doc_web & App.Settings.doc_web_la) || s_running) {
			_Switch();
		} else {
			s_running = true;
			run.thread(() => {
				int port = 59472; //prefer a stable port. Useful for bookmarks and URI scheme. If taken, retry with a random auto-assigned port, never mind.
#if IDE_LA
				port++;
#endif
				g1:
				try {
					Listen<DocsHttpServer>(port, "127.0.0.1", listener => {
						s_port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
						_Switch();
					});
				}
				catch (Exception ex) {
					if (port != 0 && ex is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.AddressAlreadyInUse }) {
						port = 0;
						goto g1;
					}
					print.warning(ex);
				}
				App.Settings.doc_web = App.Settings.doc_web_la = true;
				_Switch();
				s_running = false;
			}, sta: false);
		}
	}
	
	public static string LocalBaseUri { get; private set; }
	
	static void _Switch() {
		if (App.Settings.doc_web & App.Settings.doc_web_la) {
			LocalBaseUri = null;
			HelpUtil.AuHelpEvent_ -= _AuHelpEvent;
		} else if (LocalBaseUri == null) {
			LocalBaseUri = $"http://127.0.0.1:{s_port}/";
			HelpUtil.AuHelpEvent_ += _AuHelpEvent;
		}
		
		static void _AuHelpEvent(HelpUtil.AuHelpEventArgs_ e) {
			e.Cancel = true;
			var url = e.Url;
			if (App.Settings.doc_web) {
				run.itSafe(string.Concat(LocalBaseUri, url.AsSpan(30)));
			} else {
				Panels.Read.OpenDocUrl(url);
			}
		}
	}
	
	protected override void MessageReceived(HSMessage m, HSResponse r) {
		if (m.Method != "GET") { r.Status = System.Net.HttpStatusCode.MethodNotAllowed; return; }
		
		var path = m.TargetPath; if (path.Ends('/')) path += "index.html";
		path = path[1..];
		//print.it(path);
		
#if DEBUG
		if (path == "cookbook/preview.html") {
			r.SetContentText(PanelRead.GetPreviewHtmlTemplate_(), "text/html; charset=utf-8");
			return;
		}
		//this can be used when editing/testing a CSS file
		//if (path == "styles/docfx.vendor.min.css") {
		//	r.SetContentText(filesystem.loadText(@"C:\Temp\Au\DocFX\site\styles\docfx.vendor.min.css"), "text/css");
		//	r.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
		//	return;
		//}
#endif
		
		if (path == "favicon.ico" || path.Ends("/com.chrome.devtools.json")) { r.Status = System.Net.HttpStatusCode.NotFound; return; }
		
		lock (typeof(DocsHttpServer)) { //faster than the SQLite's busy timeout implementation
			using var db = new sqlite(folders.ThisAppBS + "doc-html.db", SLFlags.SQLITE_OPEN_READONLY); //fast, don't keep open
			if (db.Get(out byte[] content, "SELECT text FROM doc WHERE name=?", path)) {
				r.Content = content;
				var ext = pathname.getExtension(path).Lower();
				var ct = ext switch {
					".html" => "text/html; charset=utf-8",
					".css" => "text/css",
					".js" => "text/javascript",
					".png" => "image/png",
					_ => null
				};
				if (ct != null) r.Headers["Content-Type"] = ct; else Debug_.PrintIf(!(ext is ".eot"), path);
				if (ext != ".html") r.Headers["Cache-Control"] = "max-age=86400"; //1 day or until process exit
			} else {
				Debug_.PrintIf(
					!(path is "styles/docfx.vendor.min.css.map" /*size ~500kb, requested when showing devtools, works well without*/),
					"NOT FOUND: " + path);
				r.Status = System.Net.HttpStatusCode.NotFound;
			}
		}
		
		if (!s_schemeRegistered) {
			s_schemeRegistered = true;
			_RegisterUriScheme();
		}
	}
	
	static void _RegisterUriScheme() {
		if (App.IsPortable || miscInfo.isChildSession) return;
		
		try {
			string scheme = "la-link", exePath = process.thisExePath;
			
			using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{scheme}");
			key.SetValue("", $"URL:{scheme} Protocol");
			key.SetValue("URL Protocol", "");
			
			using var commandKey = key.CreateSubKey(@"shell\open\command");
			commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
		}
		catch { }
	}
	static bool s_schemeRegistered;
}
