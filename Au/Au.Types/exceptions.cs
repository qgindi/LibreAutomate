namespace Au.Types
{
	/// <summary>
	/// The base exception class used in this library.
	/// Thrown when something fails and there is no better exception type for that failure.
	/// </summary>
	/// <remarks>
	/// Some constructors support Windows API error code. Then <see cref="Message"/> will contain its error description.
	/// If the string passed to the constructor starts with <c>"*"</c>, replaces the <c>"*"</c> with <c>"Failed to "</c>. If does not end with <c>"."</c>, appends <c>"."</c>.
	/// </remarks>
	//[Serializable] //in .NET Framework would need for marshaling between appdomains. Probably don't need now.
	public class AuException : Exception//, ISerializable
	{
		/// <summary>
		/// Sets <b>Message</b> = <i>message</i> (default <c>"Failed."</c>).
		/// Sets <b>NativeErrorCode</b> = 0.
		/// </summary>
		public AuException(string message = "Failed.", Exception innerException = null) : base(message, innerException) { }

		/// <summary>
		/// Sets <b>NativeErrorCode</b> = <c>(errorCode != 0) ? errorCode : lastError.code</c>.
		/// Sets <b>Message</b> = <c>message + " " + lastError.messageFor(NativeErrorCode)</c>.
		/// </summary>
		public AuException(int errorCode, string message = "Failed.", Exception innerException = null) : base(message, innerException) {
			NativeErrorCode = (errorCode != 0) ? errorCode : lastError.code;
		}

		/// <summary>Gets the Windows API error code.</summary>
		public int NativeErrorCode { get; protected set; }

		/// <summary>Gets error message.</summary>
		public override string Message => FormattedMessage ?? FormatMessage();

		/// <summary>String created by <b>FormatMessage</b>, which should be called by the <b>Message</b> override if null. Initially null.</summary>
		protected string FormattedMessage;

		/// <summary>
		/// Formats error message. Sets and returns <b>FormattedMessage</b>.
		/// </summary>
		/// <remarks>
		/// As base text, uses the text passed to the constructor (default <c>"Failed."</c>).
		/// If it starts with <c>"*"</c>, replaces the <c>"*"</c> with <c>"Failed to "</c>.
		/// If it ends with <c>"*"</c>, replaces the <c>"*"</c> with <i>commonPostfix</i> if it is not empty.
		/// If then the message does not end with <c>"."</c>, appends <c>"."</c>.
		/// If <i>appendMessage</i> is null, uses <c>lastError.messageFor(NativeErrorCode)</c> if <b>NativeErrorCode</b> not 0.
		/// If then <i>appendMessage</i> is not empty, appends <c>" " + appendMessage</c>.
		/// Also appends <b>InnerException.Message</b> in new tab-indented line if <b>InnerException</b> is not null.
		/// </remarks>
		protected string FormatMessage(string appendMessage = null, string commonPostfix = null) {
			var m = base.Message;

			if (!m.NE()) {
				if (m[0] == '*') m = "Failed to " + m[1..];
				if (!commonPostfix.NE() && m[^1] == '*') m = m[..^1] + commonPostfix;
				if (!m.Ends('.')) m += ".";
			}

			if (appendMessage == null && NativeErrorCode != 0) appendMessage = lastError.messageFor(NativeErrorCode);

			if (!appendMessage.NE()) m = m + " " + appendMessage;

			if (InnerException != null) m = m + "\r\n\t" + InnerException.Message;

			return FormattedMessage = m;
		}

		/// <summary>
		/// If <i>errorCode</i> is not 0, throws <b>AuException</b> that includes the code and its message.
		/// More info: <see cref="FormatMessage"/>.
		/// </summary>
		/// <param name="errorCode">Windows API error code or <b>HRESULT</b>.</param>
		/// <param name="message">Main message. The message of the error code will be appended to it.</param>
		public static void ThrowIfHresultNot0(int errorCode, string message = null) {
			if (errorCode != 0) throw new AuException(errorCode, message);
		}

		/// <summary>
		/// If <i>errorCode</i> is less than 0, throws <b>AuException</b> that includes the code and its message.
		/// More info: <see cref="FormatMessage"/>.
		/// </summary>
		/// <param name="errorCode">Windows API error code or <b>HRESULT</b>.</param>
		/// <param name="message">Main message. The message of the error code will be appended to it.</param>
		public static void ThrowIfHresultNegative(int errorCode, string message = null) {
			if (errorCode < 0) throw new AuException(errorCode, message);
		}

		//#region ISerializable

		/////
		//public override void GetObjectData(SerializationInfo info, StreamingContext context)
		//{
		//	info.AddValue("NativeErrorCode", NativeErrorCode);
		//	base.GetObjectData(info, context);
		//}

		/////
		//protected AuException(SerializationInfo info, StreamingContext context) : base(info, context)
		//{
		//	NativeErrorCode = info.GetInt32("NativeErrorCode");
		//}

		//#endregion
	}

	/// <summary>
	/// Exception thrown mostly by <see cref="wnd"/> functions.
	/// </summary>
	/// <remarks>
	/// Some constructors support Windows API error code. Then <b>Message</b> also will contain its error description.
	/// If error code <b>ERROR_INVALID_WINDOW_HANDLE</b>, <b>Message</b> also depends on whether the window handle is 0.
	/// If parameter <i>errorCode</i> is 0 or not used: if the window handle is invalid, uses <b>ERROR_INVALID_WINDOW_HANDLE</b>.
	/// If the string passed to the constructor starts with <c>"*"</c>, replaces the <c>"*"</c> with <c>"Failed to "</c>. If ends with <c>"*"</c>, replaces the <c>"*"</c> with <c>" window."</c>. If does not end with <c>"."</c>, appends <c>"."</c>.
	/// </remarks>
	//[Serializable]
	public class AuWndException : AuException//, ISerializable
	{
		const string _errStr_0Handle = "The window handle is 0. Usually it means 'window not found'.";
		const string _errStr_InvalidHandle = "Invalid window handle. Usually it means 'the window was closed'.";

		/// <summary>
		/// Sets <b>Message</b> = <i>message</i> (default <c>"Failed."</c>).
		/// Sets <b>NativeErrorCode</b> = <c>w.IsAlive ? 0 : ERROR_INVALID_WINDOW_HANDLE</c>.
		/// </summary>
		public AuWndException(wnd w, string message = "Failed.", Exception innerException = null)
			: base(message, innerException) { Window = w; NativeErrorCode = _Code(0, w); }

		/// <summary>
		/// Sets <b>NativeErrorCode</b> = <c>(errorCode != 0) ? errorCode : (w.IsAlive ? lastError.code : ERROR_INVALID_WINDOW_HANDLE)</c>.
		/// Sets <b>Message</b> = <c>message + " " + lastError.messageFor(NativeErrorCode)</c>.
		/// </summary>
		public AuWndException(wnd w, int errorCode, string message = "Failed.", Exception innerException = null)
			: base(_Code(errorCode, w), message, innerException) { Window = w; }

		static int _Code(int code, wnd w) {
			if (code != 0 || w.IsAlive) return code;
			return Api.ERROR_INVALID_WINDOW_HANDLE;
		}

		/// <summary>Gets the window passed to the constructor.</summary>
		public wnd Window { get; }

		/// <summary>Gets error message.</summary>
		public override string Message {
			get {
				if (FormattedMessage == null) {
					string m;
					if (Window.Is0) m = _errStr_0Handle;
					else if (NativeErrorCode == Api.ERROR_INVALID_WINDOW_HANDLE) m = _errStr_InvalidHandle;
					else m = null; //will append lastError.messageFor(NativeErrorCode) if NativeErrorCode not 0, or InnerException.Message if it is not null.
					FormatMessage(m, " window.");
				}
				return FormattedMessage;
			}
		}

		//#region ISerializable

		/////
		//public override void GetObjectData(SerializationInfo info, StreamingContext context)
		//{
		//	info.AddValue("Window", (int)Window);
		//	base.GetObjectData(info, context);
		//}

		/////
		//protected AuWndException(SerializationInfo info, StreamingContext context) : base(info, context)
		//{
		//	Window = (wnd)info.GetInt32("Window");
		//}

		//#endregion
	}

	/// <summary>
	/// Functions that search for an object can throw this exception when not found.
	/// </summary>
	//[Serializable]
	public class NotFoundException : Exception
	{
		/// <summary>
		/// Sets <c>Message = "Not found."</c>.
		/// </summary>
		public NotFoundException() : base("Not found.") { }

		/// <summary>
		/// Sets <c>Message = message</c>.
		/// </summary>
		public NotFoundException(string message) : base(message) { }

		//#region ISerializable

		/////
		//protected NotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
		//{
		//}

		//#endregion
	}
}

namespace Au.Types
{
	static partial class ExtMisc
	{
		/// <summary>
		/// Returns string containing exception type name and message.
		/// </summary>
		public static string ToStringWithoutStack(this Exception t) {
			return t.GetType().Name + ", " + t.Message;
		}
	}
}
