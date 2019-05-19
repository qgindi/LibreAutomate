﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.ExceptionServices;
//using System.Linq;

using Au.Types;
using static Au.AStatic;

namespace Au.Util
{
	/// <summary>
	/// Static functions to open a help topic etc.
	/// </summary>
	public static class AHelp
	{
#if true //online
		/// <summary>
		/// Opens a Au help topic onine.
		/// The file must be in <see cref="Folders.ThisApp"/>.
		/// </summary>
		/// <param name="topic">Topic file name, like "Au.Acc.Find" or "Acc.Find" or "articles/Wildcard expression".</param>
		public static void AuHelp(string topic)
		{
			var url = "http://3.quickmacros.com/help/";
			if(!Empty(topic)) url = url + (topic.IndexOf('/') < 0 ? (topic.Starts("Au.") ? "api/" : "api/Au.") : null) + topic + (topic.Ends('/') ? null : ".html");
			Exec.TryRun(url);
		}
#else //.chm
		/// <summary>
		/// Opens file "Au Help.chm" and a help topic in it.
		/// The file must be in <see cref="Folders.ThisApp"/>.
		/// </summary>
		/// <param name="topic">Topic file name, like "M_Au_Acc_Find" or "0248143b-a0dd-4fa1-84f9-76831db6714a".</param>
		public static void AuChm(string topic)
		{
			var s = Folders.ThisAppBS + "Help/Au Help.chm::/html/" + topic + ".htm";
			Api.HtmlHelp(Api.GetDesktopWindow(), s, 0, 0); //HH_DISPLAY_TOPIC
		}
#endif
	}
}