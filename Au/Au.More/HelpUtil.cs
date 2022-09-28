namespace Au.More
{
	/// <summary>
	/// Static functions to open a help topic etc.
	/// </summary>
	public static class HelpUtil
	{
		/// <summary>
		/// Opens an Au library help topic online.
		/// </summary>
		/// <param name="topic">Topic file name, like <c>"Au.wnd.find"</c> or <c>"wnd.find"</c> or <c>"articles/Wildcard expression"</c>.</param>
		public static void AuHelp(string topic) {
			run.itSafe(AuHelpUrl(topic));
		}

		/// <summary>
		/// Gets URL of an Au library help topic.
		/// </summary>
		/// <param name="topic">Topic file name, like <c>"Au.wnd.find"</c> or <c>"wnd.find"</c> or <c>"articles/Wildcard expression"</c>.</param>
		public static string AuHelpUrl(string topic) {
			if (topic.Ends(".this[]")) topic = topic.ReplaceAt(^7.., ".Item");
			else if (topic.Ends(".this")) topic = topic.ReplaceAt(^5.., ".Item");
			else if (topic.Ends("[]")) topic = topic.ReplaceAt(^2.., ".Item");

			var url = "https://www.quickmacros.com/au/help/";
			if (!topic.NE()) url = url + (topic.Contains('/') ? null : (topic.Starts("Au.") ? "api/" : "api/Au.")) + topic + (topic.Ends('/') || topic.Ends(".html") ? null : ".html");
			return url;
		}
	}
}
