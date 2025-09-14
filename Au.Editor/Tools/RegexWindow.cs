using Au.Controls;

namespace Au.Tools;

class RegexWindow : InfoWindow { //KPopup
	public RegexWindow() : base(250) {
		Size = (800, 220);
		WindowName = "Regex";
		Name = "Ci.Regex"; //prevent hiding when activated
		CloseHides = true;
	}
	
#if DEBUG
	//remind to update # links when doc changed
	static RegexWindow() {
		if (App.IsAtHome) Task.Run(() => {
			try {
				var url = "https://www.pcre.org/current/doc/html/pcre2pattern.html";
				var r = internet.http.Get(url);
				var s = r.Content.Headers.NonValidated["Last-Modified"].First();
				if (s != "Thu, 20 Feb 2025 22:48:29 GMT") {
					print.it($"<>LA dev info: <link {url}>PCRE doc<> changed. May need to update # links in <open>Regex.txt<>.\r\n\tAlso update the Last-Modified string in <open>RegexWindow.cs<>. New value: {s}");
				}
			}
			catch (Exception ex) { if (internet.ping()) print.it(ex); }
		});
	}
#endif
	
	protected override void OnHandleCreated() {
		for (int i = 0; i < 2; i++) {
			var c = i == 0 ? this.Control1 : this.Control2;
			c.AaTags.AddStyleTag(".r", new() { textColor = 0xf08080 }); //red regex
			c.AaTags.AddLinkTag("+p", o => CurrentTopic = o); //link to a local info topic
			c.AaTags.SetLinkStyle(new() { textColor = 0x0080FF, underline = false }); //remove underline from links
			c.Call(Sci.SCI_SETWRAPSTARTINDENT, 4);
		}
		this.Control2.AaTags.AddStyleTag(".h", new() { backColor = 0xC0E0C0, bold = true, eolFilled = true }); //topic header
		this.Control2.AaTags.AddLinkTag("+a", o => InsertCode.TextSimplyInControl(InsertInControl, o)); //link that inserts a regex token
		
		_SetTocText();
		CurrentTopic = @"help";
		
		base.OnHandleCreated();
	}
	
	string _GetContentText() {
		var s = ContentText ?? ResourceUtil.GetString("tools/regex.txt");
		if (!s.Contains('\n')) s = File.ReadAllText(s);
		return s;
	}
	
	void _SetTocText() {
		var s = _GetContentText();
		s = s.Remove(s.Find("\r\n\r\n-- "));
		this.Text = s;
	}
	
	/// <summary>
	/// Opens an info topic or gets current topic name.
	/// </summary>
	public string CurrentTopic {
		get => _topic;
		set {
			if (value == _topic) return;
			_topic = value;
			string s;
			if (value.Starts("Note:")) {
				s = value;
			} else {
				s = _GetContentText();
				if (!s.RxMatch($@"(?ms)^-- {_topic} --\R\R(.+?)\R-- ", 1, out s)) s = "";
				else s = s.Replace("{App.Settings.internetSearchUrl}", App.Settings.internetSearchUrl);
			}
			this.Text2 = s;
		}
	}
	string _topic;
	
	public void Refresh() {
		_SetTocText();
		var s = _topic;
		_topic = null;
		CurrentTopic = s;
	}
	
	/// <summary>
	/// Content text or file path.
	/// If changed later, then call Refresh.
	/// If null (default), uses text from resources of this dll.
	/// </summary>
	public string ContentText { get; set; }
}
