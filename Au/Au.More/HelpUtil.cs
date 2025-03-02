using System.Security.Policy;

namespace Au.More
{
	/// <summary>
	/// Static functions to open a help topic etc.
	/// </summary>
	public static class HelpUtil
	{
		/// <summary>
		/// Determines whether local rather than online documentation is shown for calls to <see cref="Au.More.HelpUtil.AuHelp(string)"/>.
		/// </summary>
		public static bool enableLocalDocumentation;
		/// <summary>
		/// Directory where local documentation is found.  Defaults to <see cref="Au.More.HelpUtil.stdDocumentationDir"/>.
		/// </summary>
		public static string documentationDir;
        /// <summary>
        /// Usual directory where local documentation is found.  Set to <c>\docs</c> in the folder <see cref="Au.folders.Editor"/> at runtime.
        /// </summary>
        public static readonly string stdDocumentationDir = folders.Editor + @"\docs";
        /// <summary>
        /// Full path to <c>docfx.exe</c>.  Defaults to <see cref="Au.More.HelpUtil.stdDocFxExecutable"/>.
        /// </summary>
        public static string docFxExecutable;
        /// <summary>
        /// Usual full path to <c>docfx.exe</c>.  Set to <c>\.dotnet\tools\docfx.exe</c> in the folder <see cref="Au.folders.Profile"/> at runtime.
        /// </summary>
        public static readonly string stdDocFxExecutable = folders.Profile + @"\.dotnet\tools\docfx.exe";
        /// <summary>
        /// Port number used by docfx to serve the local documentation.  Passed as <c>--port</c> parameter.  Defaults to <see cref="Au.More.HelpUtil.stdDocFxPort"/>.
        /// </summary>
        public static string docFxPort;
        /// <summary>
        /// Usual port number to pass to docfx to serve the local documentation.  Passed as <c>--port</c> parameter.  Set to <c>8080</c>.
        /// </summary>
        public static readonly string stdDocFxPort = "8080";

        /// <summary>
        /// Opens an Au library help topic.  If <see cref="Au.More.HelpUtil.enableLocalDocumentation"/> is false then help is obtained from <b>https://www.libreautomate.com/</b>.
		/// Otherwise docfx.exe is called using the path <see cref="Au.More.HelpUtil.docFxExecutable"/> with the <c>serve</c> command and the <c>--port</c> parameter and help is obtained from the url <b>http://localhost:port"/</b> where <b>port</b> is <see cref="Au.More.HelpUtil.docFxPort"/>.
        /// </summary>
        /// <param name="topic">Topic file name, like <c>"Au.wnd.find"</c> or <c>"wnd.find"</c> or <c>"articles/Wildcard expression"</c>.</param>
        public static void AuHelp(string topic, bool forceNonLocalDocumentation = false) {
            run.itSafe(AuHelpUrl(topic, forceNonLocalDocumentation));
		}

		/// <summary>
		/// Gets URL of an Au library help topic.
		/// </summary>
		/// <param name="topic">Topic file name, like <c>"Au.wnd.find"</c> or <c>"wnd.find"</c> or <c>"articles/Wildcard expression"</c>.</param>
		public static string AuHelpUrl(string topic, bool forceNonLocalDocumentation = false) {
			string url;
			if (topic.Ends(".this[]")) topic = topic.ReplaceAt(^7.., ".Item");
			else if (topic.Ends(".this")) topic = topic.ReplaceAt(^5.., ".Item");
			else if (topic.Ends("[]")) topic = topic.ReplaceAt(^2.., ".Item");

            if (enableLocalDocumentation && !forceNonLocalDocumentation)
            {
                LaunchLocalDocFxServer();
                url = @"http://localhost:" + docFxPort + @"/";
			}
            else 
            {
                url = "https://www.libreautomate.com/";
            }
            if (!topic.NE()) url = url + (topic.Contains('/') ? null : (topic.Starts("Au.") ? "api/" : "api/Au.")) + topic + (topic.Ends('/') || topic.Ends(".html") ? null : ".html");
			return url;
		}
        /// <summary>
        /// Launches DocFx.exe server if it is not already running and creates an event handler to close it when this process exits.
        /// </summary>
        private static void LaunchLocalDocFxServer()
        {
            documentationDir ??= stdDocumentationDir;
            docFxPort ??= stdDocFxPort;
            string cmdLine = $@"serve ""{documentationDir}"" --port {docFxPort}";
            foreach (int pidInLoop in process.getProcessIds("docfx.exe"))
            {
                if (string.Equals(process.getCommandLine(pidInLoop, true), cmdLine)) return;
            }

            docFxExecutable ??= stdDocFxExecutable;
            RResult rresult = run.it(docFxExecutable, cmdLine, flags: RFlags.InheritAdmin, dirEtc: new() { WindowState = ProcessWindowStyle.Hidden });
            var pid = rresult.ProcessId;
            process.getTimes(pid, out long created, out long _);

            process.thisProcessExit += (Exception ex) =>
            {
                if (process.getTimes(pid, out long createdInEventHandler, out long _))
                {
                    if (created == createdInEventHandler) process.terminate(pid);
                }
            };
        }
    }
}
