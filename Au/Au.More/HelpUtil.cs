namespace Au.More;

/// <summary>
/// Static functions to open a help topic etc.
/// </summary>
public static class HelpUtil {
	/// <summary>
	/// Opens a LibreAutomate help topic.
	/// </summary>
	/// <param name="topic">Topic file name, like <c>"wnd.find"</c> or <c>"Au.Types.RECT"</c> or <c>"articles/Wildcard expression"</c>.</param>
	public static void AuHelp(string topic) {
		var url = AuHelpUrl(topic);
		if (AuHelpEvent_ is { } e) {
			var k = new AuHelpEventArgs_ { Url = url };
			e(k);
			if (k.Cancel) return;
			url = k.Url;
		}
		run.itSafe(url);
	}
	
	/// <summary>
	/// Gets URL of a LibreAutomate help topic.
	/// </summary>
	/// <param name="topic">Topic file name, like <c>"wnd.find"</c> or <c>"Au.Types.RECT"</c> or <c>"articles/Wildcard expression"</c>.</param>
	public static string AuHelpUrl(string topic) {
		string fragment = null;
		int i = topic.IndexOf('#');
		if (i >= 0) {
			fragment = topic[i..];
			topic = topic[..i];
		}
		
		if (topic.Ends(".this[]")) topic = topic.ReplaceAt(^7.., ".Item");
		else if (topic.Ends(".this")) topic = topic.ReplaceAt(^5.., ".Item");
		else if (topic.Ends("[]")) topic = topic.ReplaceAt(^2.., ".Item");
		else if (topic.Contains("timer")) topic = topic.RxReplace(@"\btimer2?\.(after|every)\b\K", "_1"); //the filename has this suffix because of the instance method After/Every
		
		var url = AuHelpBaseUrl;
		if (!url.Ends('/')) url += "/";
		if (!topic.NE()) url = url
				+ (topic.Contains('/') ? null : (topic.Starts("Au.") ? "api/" : "api/Au."))
				+ topic
				+ (topic.Ends(".html") || topic.Ends('/') ? null : ".html")
				+ fragment;
		return url;
	}
	
	/// <summary>
	/// <c>s.Starts(false, "Au.", "articles/", "editor/", "cookbook/", "api/") > 0</c>
	/// </summary>
	internal static bool IsAuHelp_(string s) => s.Starts(false, "Au.", "articles/", "editor/", "cookbook/", "api/") > 0;
	
	/// <summary>
	/// URL of the LibreAutomate documentation website: <c>"https://www.libreautomate.com/"</c>.
	/// </summary>
	public static string AuHelpBaseUrl => "https://www.libreautomate.com/";

	internal static event Action<AuHelpEventArgs_> AuHelpEvent_;
	
	internal class AuHelpEventArgs_ : CancelEventArgs {
		public string Url { get; set; }
	}
}
