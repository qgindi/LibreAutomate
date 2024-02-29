namespace Au {
	/// <summary>
	/// Gets, sets or clears the last error code of Windows API. Gets error text.
	/// </summary>
	/// <remarks>
	/// Many Windows API functions, when failed, set an error code. Code 0 means no error. It is stored in an internal thread-specific int variable. But only if the API declaration's <b>DllImport</b> attribute has <c>SetLastError = true</c>.
	/// 
	/// Some functions of this library simply call these API functions and don't throw exception when API fail. For example, most <see cref="wnd"/> property-get functions.
	/// When failed, they return <c>false</c>/0/<c>null</c>/empty. Then you can use <see cref="code"/> to get the error code or <see cref="message"/> to get error text.
	/// 
	/// Most of functions set error code only when failed, and don't clear the old error code when succeeded. Therefore may need to call <see cref="clear"/> before.
	/// 
	/// Windows API error code definitions and documentation are not included in this library. You can look for them in API function documentation on the internet.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// wnd w = wnd.find("Notepag");
	/// lastError.clear();
	/// bool enabled = w.IsEnabled; //returns true if enabled, false if disabled or failed
	/// if(!enabled && lastError.code != 0) { print.it(lastError.message); return; } //1400, Invalid window handle
	/// print.it(enabled);
	/// ]]></code>
	/// </example>
	public static class lastError {
		/// <summary>
		/// Calls API <msdn>SetLastError</msdn>(0), which clears the Windows API last error code of this thread.
		/// </summary>
		/// <remarks>
		/// Need it before calling some functions if you want to use <see cref="code"/> or <see cref="message"/>.
		/// The same as <c>lastError.code = 0;</c>.
		/// </remarks>
		public static void clear() => Api.SetLastError(0);
		
		/// <summary>
		/// Gets (<see cref="Marshal.GetLastWin32Error"/>) or sets (API <msdn>SetLastError</msdn>) the Windows API last error code of this thread.
		/// </summary>
		public static int code {
			get => Marshal.GetLastWin32Error();
			set => Api.SetLastError(value);
		}
		
		/// <summary>
		/// Gets the text message of the Windows API last error code of this thread.
		/// </summary>
		/// <returns><c>null</c> if the code is 0.</returns>
		/// <remarks>
		/// The string always ends with <c>"."</c>.
		/// </remarks>
		public static string message => messageFor(code);
		
		/// <summary>
		/// Gets the text message of a Windows API error code.
		/// </summary>
		/// <returns><c>null</c> if <i>errorCode</i> is 0.</returns>
		/// <remarks>
		/// The string always ends with <c>"."</c>.
		/// </remarks>
		public static unsafe string messageFor(int errorCode) {
			if (errorCode == 0) return null;
			if (errorCode == 1) return "The requested data or action is unavailable. (0x1)."; //or ERROR_INVALID_FUNCTION, but it's rare
			string s = "Unknown exception";
			char* p = null;
			const uint fl = Api.FORMAT_MESSAGE_FROM_SYSTEM | Api.FORMAT_MESSAGE_ALLOCATE_BUFFER | Api.FORMAT_MESSAGE_IGNORE_INSERTS;
			int r = Api.FormatMessage(fl, default, errorCode, 0, &p, 0, default);
			if (p != null) {
				while (r > 0 && p[r - 1] <= ' ') r--;
				if (r > 0) {
					if (p[r - 1] == '.') r--;
					s = new string(p, 0, r);
				}
				Api.LocalFree(p);
			}
			s = (uint)errorCode <= 0xffff ? $"{s} ({errorCode})." : $"{s} (0x{errorCode:X}).";
			return s;
		}
	}
}
