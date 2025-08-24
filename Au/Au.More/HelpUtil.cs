namespace Au.More;

/// <summary>
/// Static functions to open a help topic etc.
/// </summary>
public static class HelpUtil {
	/// <summary>
	/// Opens a help topic of the Au library or LibreAutomate.
	/// </summary>
	/// <param name="topic">Topic file name, like <c>"Au.wnd.find"</c> or <c>"wnd.find"</c> or <c>"articles/Wildcard expression"</c>.</param>
	public static void AuHelp(string topic) {
		run.itSafe(AuHelpUrl(topic));
	}
	
	/// <summary>
	/// Gets URL of a help topic of the Au library or LibreAutomate.
	/// </summary>
	/// <param name="topic">Topic file name, like <c>"Au.wnd.find"</c> or <c>"wnd.find"</c> or <c>"articles/Wildcard expression"</c>.</param>
	public static string AuHelpUrl(string topic) {
		if (topic.Ends(".this[]")) topic = topic.ReplaceAt(^7.., ".Item");
		else if (topic.Ends(".this")) topic = topic.ReplaceAt(^5.., ".Item");
		else if (topic.Ends("[]")) topic = topic.ReplaceAt(^2.., ".Item");
		else if (topic.Starts("Au.timer") && topic.Ends(false, "after", "every") > 0) topic += "_1"; //the filename has this suffix because of the instance method After/Every
		
		var url = AuHelpBaseUrl;
		if (!url.Ends('/')) url += "/";
		if (!topic.NE()) url = url + (topic.Contains('/') ? null : (topic.Starts("Au.") ? "api/" : "api/Au.")) + topic + (topic.Ends('/') || topic.Ends(".html") ? null : ".html");
		return url;
	}
	
	/// <summary>
	/// URL of the LibreAutomate documentation website (default <c>"https://www.libreautomate.com/"</c>) or local documentation (like <c>"http://127.0.0.1:4555/"</c>).
	/// </summary>
	public static string AuHelpBaseUrl { get; set; } = AuHelpBaseUrlDefault_;
	
	internal const string AuHelpBaseUrlDefault_ = "https://www.libreautomate.com/";
}
