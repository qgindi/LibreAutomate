using Au.Controls;

namespace Au.Tools;

class RegexWindow : InfoWindow //KPopup
{
	public RegexWindow() : base(250) {
		Size = (800, 220);
		WindowName = "Regex";
		Name = "Ci.Regex"; //prevent hiding when activated
		CloseHides = true;
	}

	protected override void OnHandleCreated() {
		for (int i = 0; i < 2; i++) {
			var c = i == 0 ? this.Control1 : this.Control2;
			c.AaTags.AddStyleTag(".r", new SciTags.UserDefinedStyle { textColor = 0xf08080 }); //red regex
			c.AaTags.AddLinkTag("+p", o => CurrentTopic = o); //link to a local info topic
			c.AaTags.SetLinkStyle(new SciTags.UserDefinedStyle { textColor = 0x0080FF, underline = false }); //remove underline from links
			c.Call(Sci.SCI_SETWRAPSTARTINDENT, 4);
		}
		this.Control2.AaTags.AddStyleTag(".h", new SciTags.UserDefinedStyle { backColor = 0xC0E0C0, bold = true, eolFilled = true }); //topic header
		this.Control2.AaTags.AddLinkTag("+a", o => InsertCode.TextSimplyInControl(InsertInControl, o)); //link that inserts a regex token

		_SetTocText();
		CurrentTopic = "help";

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
