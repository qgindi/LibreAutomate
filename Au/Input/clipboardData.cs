
//#define SUPPORT_RAW_HANDLE

using System.Drawing;
using System.Drawing.Imaging;

namespace Au {
	/// <summary>
	/// Sets or gets clipboard data in multiple formats.
	/// </summary>
	/// <remarks>
	/// The <b>AddX</b> functions add data to the variable (not to the clipboard). Then <see cref="SetClipboard"/> copies the added data to the clipboard. Also you can use the variable with <see cref="clipboard.pasteData"/>.
	/// The static <b>GetX</b> functions get data directly from the clipboard.
	/// </remarks>
	/// <example>
	/// Get bitmap image from clipboard.
	/// <code><![CDATA[
	/// var image = clipboardData.getImage();
	/// if(image == null) print.it("no image in clipboard"); else print.it(image.Size);
	/// ]]></code>
	/// Set clipboard data in two formats: text and image.
	/// <code><![CDATA[
	/// new clipboardData().AddText("text").AddImage(Image.FromFile(@"C:\file.png")).SetClipboard();
	/// ]]></code>
	/// Paste data of two formats: HTML and text.
	/// <code><![CDATA[
	/// clipboard.pasteData(new clipboardData().AddHtml("<b>text</b>").AddText("text"));
	/// ]]></code>
	/// Copy data in two formats: HTML and text.
	/// <code><![CDATA[
	/// string html = null, text = null;
	/// clipboard.copyData(() => { html = clipboardData.getHtml(); text = clipboardData.getText(); });
	/// print.it(html); print.it(text);
	/// ]]></code>
	/// </example>
	public class clipboardData {
		struct _Data { public object data; public int format; }
		List<_Data> _a = new();

		#region add

		static void _CheckFormat(int format, bool minimalCheckFormat = false) {
			bool badFormat = false;
			if (format <= 0 || format > 0xffff) badFormat = true;
			else if (format < 0xC000 && !minimalCheckFormat) {
				if (format >= Api.CF_MAX) badFormat = true; //rare. Most are either not GlobalAlloc'ed or not auto-freed.
				else badFormat = format == Api.CF_BITMAP || format == Api.CF_PALETTE || format == Api.CF_METAFILEPICT || format == Api.CF_ENHMETAFILE; //not GlobalAlloc'ed
			}
			if (badFormat) throw new ArgumentException("Invalid format id.");
		}

		clipboardData _Add(object data, int format, bool minimalCheckFormat = false) {
			Not_.Null(data);
			_CheckFormat(format, minimalCheckFormat);

			_a.Add(new _Data() { data = data, format = format });
			return this;
		}

		/// <summary>
		/// Adds text.
		/// </summary>
		/// <returns>this.</returns>
		/// <param name="text">Text.</param>
		/// <param name="format">
		/// Clipboard format id. Default: <see cref="ClipFormats.Text"/> (<b>CF_UNICODETEXT</b>).
		/// Text encoding depends on <i>format</i>; default UTF-16. See <see cref="ClipFormats.Register"/>.
		/// </param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentException">Invalid <i>format</i>.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="Encoding.GetBytes(string)"/>, which is called if encoding is not UTF-16.</exception>
		public clipboardData AddText(string text, int format = ClipFormats.Text) {
			Encoding enc = ClipFormats.GetTextEncoding_(format, out _);
			if (enc == null) return _Add(text, format == 0 ? Api.CF_UNICODETEXT : format);
			return _Add(enc.GetBytes(text).InsertAt(-1), format);
		}

		/// <summary>
		/// Adds data of any format as <b>byte[]</b>.
		/// </summary>
		/// <returns>this.</returns>
		/// <param name="data"><b>byte[]</b> containing data.</param>
		/// <param name="format">Clipboard format id. See <see cref="ClipFormats.Register"/>.</param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentException">Invalid <i>format</i>. Supported are all registered formats and standard formats &lt;<b>CF_MAX</b> except GDI handles.</exception>
		public clipboardData AddBinary(byte[] data, int format) {
			return _Add(data, format);
		}

		//rejected: rarely used, difficult to use, creates problems. If somebody needs it, can use API.
#if SUPPORT_RAW_HANDLE
			/// <summary>
			/// Adds data of any format as raw clipboard object handle.
			/// </summary>
			/// <returns>this.</returns>
			/// <param name="handle">Any handle supported by API <msdn>SetClipboardData</msdn>. The type depends on format. For most formats, after setting clipboard data the handle is owned and freed by Windows.</param>
			/// <param name="format">Clipboard format id. See <see cref="RegisterClipboardFormat"/>.</param>
			/// <exception cref="ArgumentNullException"></exception>
			/// <exception cref="ArgumentException">Invalid format.</exception>
			/// <remarks>
			/// The same handle cannot be added to the clipboard twice. To avoid it, "set clipboard" functions remove handles from the variable.
			/// </remarks>
			public Data AddHandle(IntPtr handle, int format)
			{
				return _Add(handle != default ? (object)handle : null, format, minimalCheckFormat: true);
			}
#endif

		/// <summary>
		/// Adds image.
		/// Uses clipboard format <see cref="ClipFormats.Png"/> and/or <see cref="ClipFormats.Image"/> (<b>CF_BITMAP</b>).
		/// </summary>
		/// <returns>this.</returns>
		/// <param name="image">Image. Must be <see cref="Bitmap"/>, else exception.</param>
		/// <param name="png">
		/// Use PNG format (it supports transparency):
		/// <br/>• <c>false</c> - no, only CF_BITMAP.
		/// <br/>• <c>true</c> - yes, only PNG.
		/// <br/>• <c>null</c> (default) - add PNG and CF_BITMAP.
		/// </param>
		/// <exception cref="ArgumentNullException"></exception>
		public clipboardData AddImage(Image image, bool? png = null) {
			var b = (Bitmap)image;
			if (png != false) {
				var ms = new MemoryStream();
				b.Save(ms, ImageFormat.Png);
				_Add(ms.ToArray(), ClipFormats.Png, minimalCheckFormat: true);
			}
			if (png != true) {
				_Add(b, Api.CF_BITMAP, minimalCheckFormat: true);
			}
			return this;
		}

		/// <summary>
		/// Adds HTML text. Uses clipboard format <see cref="ClipFormats.Html"/> (<c>"HTML Format"</c>).
		/// </summary>
		/// <returns>this.</returns>
		/// <param name="html">Full HTML or HTML fragment. If full HTML, a fragment in it can be optionally specified. See examples.</param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <example>
		/// <code><![CDATA[
		/// d.AddHtml("<i>italy</i>");
		/// d.AddHtml("<html><body><i>italy</i></body></html>");
		/// d.AddHtml("<html><body><!--StartFragment--><i>italy</i><!--EndFragment--></body></html>");
		/// ]]></code>
		/// </example>
		public clipboardData AddHtml(string html) {
			return AddBinary(CreateHtmlFormatData_(html), ClipFormats.Html);
			//note: don't support UTF-16 string of HTML format (starts with "Version:"). UTF8 conversion problems.
		}

		/// <summary>
		/// Adds list of files to copy/paste. Uses clipboard format <see cref="ClipFormats.Files"/> (<b>CF_HDROP</b>).
		/// </summary>
		/// <returns>this.</returns>
		/// <param name="files">One or more file paths.</param>
		/// <exception cref="ArgumentNullException"></exception>
		public clipboardData AddFiles(params string[] files) {
			Not_.Null(files);
			var b = new StringBuilder("\x14\0\0\0\0\0\0\0\x1\0"); //struct DROPFILES
			foreach (var s in files) { b.Append(s); b.Append('\0'); }
			return _Add(b.ToString(), Api.CF_HDROP, false);
		}

		/// <summary>
		/// Copies the added data of all formats to the clipboard.
		/// </summary>
		/// <exception cref="AuException">Failed to open clipboard (after 10 s of wait/retry) or set clipboard data.</exception>
		/// <exception cref="OutOfMemoryException">Failed to allocate memory for clipboard data.</exception>
		/// <remarks>
		/// Calls API <msdn>OpenClipboard</msdn>, <msdn>EmptyClipboard</msdn>, <msdn>SetClipboardData</msdn> and <msdn>CloseClipboard</msdn>.
		/// </remarks>
		public void SetClipboard() {
			using (new clipboard.OpenClipboard_(true)) {
				clipboard.EmptyClipboard_();
				SetOpenClipboard();
			}
		}

		/// <summary>
		/// Copies the added data of all formats to the clipboard which is open/owned by this thread.
		/// </summary>
		/// <param name="renderLater">Call API <msdn>SetClipboardData</msdn>: <c>SetClipboardData(format, default)</c>. When/if some app will try to get clipboard data, the first time your clipboard owner window will receive <msdn>WM_RENDERFORMAT</msdn> message and should call <c>SetOpenClipboard(false);</c>.</param>
		/// <param name="format">Copy data only of this format. If 0 (default), of all formats.</param>
		/// <exception cref="OutOfMemoryException">Failed to allocate memory for clipboard data.</exception>
		/// <exception cref="AuException">Failed to set clipboard data.</exception>
		/// <remarks>
		/// This function is similar to <see cref="SetClipboard"/>. It calls API <msdn>SetClipboardData</msdn> and does not call <b>OpenClipboard</b>, <b>EmptyClipboard</b>, <b>CloseClipboard</b>. The clipboard must be open and owned by a window of this thread.
		/// </remarks>
		public void SetOpenClipboard(bool renderLater = false, int format = 0) {
			for (int i = 0; i < _a.Count; i++) {
				var v = _a[i];
				if (format != 0 && v.format != format) continue;
				if (renderLater) {
					lastError.clear();
					Api.SetClipboardData(v.format, default);
					int ec = lastError.code; if (ec != 0) throw new AuException(ec, "*set clipboard data");
				} else _SetClipboard(v.format, v.data);
			}
#if SUPPORT_RAW_HANDLE
				//remove caller-added handles, to avoid using the same handle twice
				if(renderLater) return;
				for(int i = _a.Count; --i >= 0;) {
					var v = _a[i];
					if(format != 0 && v.format != format) continue;
					if(v.data is IntPtr) _a.RemoveAt(i);
				}
#endif
		}

		static unsafe void _SetClipboard(int format, object data) {
			IntPtr h = default;
			switch (data) {
			case string s:
				fixed (char* p = s) h = _CopyToHmem(p, (s.Length + 1) * 2);
				break;
			case byte[] b:
				fixed (byte* p = b) h = _CopyToHmem(p, b.Length);
				break;
#if SUPPORT_RAW_HANDLE
				case IntPtr ip:
					h = ip;
					break;
#endif
			case Bitmap bmp:
				h = bmp.GetHbitmap();
				var h2 = Api.CopyImage(h, 0, 0, 0, Api.LR_COPYDELETEORG); //DIB to compatible bitmap
				if (h2 == default) goto ge;
				h = h2;
				break;
			}
			Debug.Assert(h != default);
			if (default != Api.SetClipboardData(format, h)) return;
			ge:
			int ec = lastError.code;
			if (data is Bitmap) Api.DeleteObject(h); else Api.GlobalFree(h);
			throw new AuException(ec, "*set clipboard data");
		}

		static unsafe IntPtr _CopyToHmem(void* p, int size) {
			var h = Api.GlobalAlloc(Api.GMEM_MOVEABLE, size); if (h == default) goto ge;
			var v = (byte*)Api.GlobalLock(h); if (v == null) { Api.GlobalFree(h); goto ge; }
			try { MemoryUtil.Copy(p, v, size); } finally { Api.GlobalUnlock(h); }
			return h;
			ge: throw new OutOfMemoryException();
		}

		/// <summary>
		/// Copies Unicode text to the clipboard without open/empty/close.
		/// </summary>
		internal static void SetText_(string text) {
			Debug.Assert(text != null);
			_SetClipboard(Api.CF_UNICODETEXT, text);
		}

		/// <summary>
		/// Converts HTML string to <b>byte[]</b> containing data in clipboard format "HTML Format".
		/// </summary>
		/// <param name="html">Full HTML or HTML fragment. If full HTML, a fragment in it can be optionally specified. See examples.</param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <example>
		/// HTML examples.
		/// <code><![CDATA[
		/// "<i>italy</i>"
		/// "<html><body><i>italy</i></body></html>"
		/// "<html><body><!--StartFragment--><i>italy</i><!--EndFragment--></body></html>"
		/// ]]></code>
		/// </example>
		internal static unsafe byte[] CreateHtmlFormatData_(string html) {
			Not_.Null(html);
			var b = new StringBuilder(c_headerTemplate);
			//find "<body>...</body>" and "<!--StartFragment-->...<!--EndFragment-->" in it
			int isb = -1, ieb = -1, isf = -1, ief = -1; //start/end of inner body and fragment
			if (html.RxMatch(@"<body\b.*?>", 0, out RXGroup body) && (ieb = html.Find("</body>", body.End)) >= 0) {
				isb = body.End;
				isf = html.Find(c_startFragment, isb..ieb, true);
				if (isf >= 0) {
					isf += c_startFragment.Length;
					ief = html.Find(c_endFragment, isf..ieb, true);
				}
			}
			//print.it($"{isb} {ieb}  {isf} {ief}");
			if (ieb < 0) { //no "<body>...</body>"
				b.Append("<html><body>").Append(c_startFragment).Append(html).Append(c_endFragment).Append("</body></html>");
				isf = 12 + c_startFragment.Length;
				ief = isf + Encoding.UTF8.GetByteCount(html);
			} else {
				if (ief < 0) { //"...<body>...</body>..."
					b.Append(html, 0, isb).Append(c_startFragment).Append(html, isb, ieb - isb)
						.Append(c_endFragment).Append(html, ieb, html.Length - ieb);
					isf = isb + c_startFragment.Length;
					ief = ieb + c_startFragment.Length;
				} else { //"...<body>...<!--StartFragment-->...<!--EndFragment-->...</body>..."
					b.Append(html);
					isb = isf; ieb = ief; //reuse these vars to calc UTF8 lengths
				}
				//correct isf/ief if html part lengths are different in UTF8
				if (!html.IsAscii()) {
					fixed (char* p = html) {
						int lenDiff1 = Encoding.UTF8.GetByteCount(p, isb) - isb;
						int lenDiff2 = Encoding.UTF8.GetByteCount(p + isb, ieb - isb) - (ieb - isb);
						isf += lenDiff1;
						ief += lenDiff1 + lenDiff2;
					}
				}
			}
			//print.it($"{isf} {ief}");
			isf += c_headerTemplate.Length; ief += c_headerTemplate.Length;

			b.Append('\0');
			var a = Encoding.UTF8.GetBytes(b.ToString());
			_SetNum(a.Length - 1, 53);
			_SetNum(isf, 79);
			_SetNum(ief, 103);

			//print.it(Encoding.UTF8.GetString(a));
			return a;

			void _SetNum(int num, int i) {
				for (; num != 0; num /= 10) a[--i] = (byte)('0' + num % 10);
			}
		}
		const string c_startFragment = "<!--StartFragment-->";
		const string c_endFragment = "<!--EndFragment-->";
		const string c_headerTemplate = @"Version:0.9
StartHTML:0000000105
EndHTML:0000000000
StartFragment:0000000000
EndFragment:0000000000
";

		#endregion

		#region get

		struct _GlobalLock : IDisposable {
			IntPtr _hmem;

			public _GlobalLock(IntPtr hmem, out IntPtr mem, out int size) {
				mem = Api.GlobalLock(hmem);
				if (mem == default) { _hmem = default; size = 0; return; }
				size = (int)Api.GlobalSize(_hmem = hmem);
			}

			public void Dispose() {
				Api.GlobalUnlock(_hmem);
			}
		}

		/// <summary>
		/// Gets clipboard text without open/close.
		/// If format is 0, tries CF_UNICODETEXT and CF_HDROP.
		/// </summary>
		internal static unsafe string GetText_(int format) {
			IntPtr h = default;
			if (format == 0) {
				h = Api.GetClipboardData(Api.CF_UNICODETEXT);
				if (h == default) format = Api.CF_HDROP;
			}
			if (format == 0) format = Api.CF_UNICODETEXT;
			else {
				h = Api.GetClipboardData(format); if (h == default) return null;
				if (format == Api.CF_HDROP) return string.Join("\r\n", HdropToFiles_(h));
			}

			using (new _GlobalLock(h, out var mem, out int len)) {
				if (mem == default) return null;
				var s = (char*)mem; var b = (byte*)s;

				Encoding enc = ClipFormats.GetTextEncoding_(format, out bool unknown);
				if (unknown) {
					if ((len & 1) != 0 || BytePtr_.Length(b, len) > len - 2) enc = Encoding.Default; //autodetect  //never mind: it is UTF-8, not ANSI. Rarely used, especially with non-ASCII text.
				}

				if (enc == null) {
					len /= 2; while (len > 0 && s[len - 1] == '\0') len--;
					return new string(s, 0, len);
				} else {
					//most apps add single '\0' at the end. Some don't add. Some add many, eg Dreamweaver. Trim all.
					int charLen = enc.GetByteCount("\0");
					switch (charLen) {
					case 1:
						while (len > 0 && b[len - 1] == '\0') len--;
						break;
					case 2:
						for (int k = len / 2; k > 0 && s[k - 1] == '\0'; k--) len -= 2;
						break;
					case 4:
						var ip = (int*)s; for (int k = len / 4; k > 0 && ip[k - 1] == '\0'; k--) len -= 4;
						break;
					}
					return enc.GetString(b, len);
					//note: don't parse HTML format here. Let caller use GetHtml or parse itself.
				}
			}
		}

		/// <summary>
		/// Gets text from the clipboard.
		/// </summary>
		/// <returns><c>null</c> if there is no text.</returns>
		/// <param name="format">
		/// Clipboard format id. Default: <see cref="ClipFormats.Text"/> (<b>CF_UNICODETEXT</b>).
		/// If 0, tries to get text (<see cref="ClipFormats.Text"/>) or file paths (<see cref="ClipFormats.Files"/>; returns multiline text).
		/// Text encoding depends on <i>format</i>; default UTF-16. See <see cref="ClipFormats.Register"/>.
		/// </param>
		/// <exception cref="AuException">Failed to open clipboard (after 10 s of wait/retry).</exception>
		public static string getText(int format = ClipFormats.Text) {
			using (new clipboard.OpenClipboard_(false)) {
				return GetText_(format);
			}
		}

		/// <summary>
		/// Gets clipboard data of any format as <b>byte[]</b>.
		/// </summary>
		/// <returns><c>null</c> if there is no data of this format.</returns>
		/// <exception cref="ArgumentException">Invalid <i>format</i>. Supported are all registered formats and standard formats &lt;<b>CF_MAX</b> except GDI handles.</exception>
		/// <exception cref="AuException">Failed to open clipboard (after 10 s of wait/retry).</exception>
		public static byte[] getBinary(int format) {
			_CheckFormat(format);
			using (new clipboard.OpenClipboard_(false)) {
				var h = Api.GetClipboardData(format); if (h == default) return null;
				using (new _GlobalLock(h, out var mem, out int len)) {
					if (mem == default) return null;
					var b = new byte[len];
					Marshal.Copy(mem, b, 0, len);
					return b;
				}
			}
		}

		/// <summary>
		/// Gets clipboard data of any format without copying to array. Uses a callback function.
		/// </summary>
		/// <param name="get">Callback function that receives data. The clipboard is open until it returns. The data is read-only.</param>
		/// <returns>The return value of the callback function. Returns default(T) if there is no data of this format.</returns>
		/// <inheritdoc cref="getBinary(int)"/>
		public static T getBinary<T>(int format, Func<IntPtr, int, T> get) {
			_CheckFormat(format);
			using (new clipboard.OpenClipboard_(false)) {
				return _GetBinary(format, get);
			}
		}

		static T _GetBinary<T>(int format, Func<IntPtr, int, T> get) {
			var h = Api.GetClipboardData(format);
			if (h != default) {
				using (new _GlobalLock(h, out var mem, out int len)) {
					if (mem != default) return get(mem, len); //.NET does not allow Func<ReadOnlySpan<byte>, T>
				}
			}
			return default;
		}

#if SUPPORT_RAW_HANDLE
			public static IntPtr GetHandle(int format)
			{
				_CheckFormat(format, minimalCheckFormat: true);
				using(new OpenClipboard_(false)) {
					return Api.GetClipboardData(format);
				}
			}
#endif

		/// <summary>
		/// Gets image from the clipboard.
		/// Uses clipboard format <see cref="ClipFormats.Png"/> or <see cref="ClipFormats.Image"/> (<b>CF_BITMAP</b>).
		/// </summary>
		/// <returns><c>null</c> if there is no data of this format.</returns>
		/// <param name="png">
		/// Use PNG format (it supports transparency):
		/// <br/>• <c>false</c> - no, only CF_BITMAP.
		/// <br/>• <c>true</c> - yes, only PNG.
		/// <br/>• <c>null</c> (default) - yes, but get CF_BITMAP if there is no PNG.
		/// </param>
		/// <exception cref="AuException">Failed to open clipboard (after 10 s of wait/retry).</exception>
		/// <exception cref="Exception">Exceptions of <see cref="Image.FromHbitmap"/> or <see cref="Image.FromStream"/>.</exception>
		public static unsafe Bitmap getImage(bool? png = null) {
			using (new clipboard.OpenClipboard_(false)) {
				if (png != false && _GetBinary(ClipFormats.Png, static (mem, len) => {
					using var ms = new UnmanagedMemoryStream((byte*)mem, len);
					return Image.FromStream(ms);
				}) is Bitmap b1) return b1;

				if (png != true) {
					var h = Api.GetClipboardData(Api.CF_BITMAP);
					if (h != default) {
						using var b = Image.FromHbitmap(h, Api.GetClipboardData(Api.CF_PALETTE)); //bottom-up 32Rgb (GDI)
						return b?.Clone(new(default, b.Size), PixelFormat.Format32bppArgb) as Bitmap; //top-down 32Argb (GDI+)
					}
				}
				return null;
			}
		}

		/// <summary>
		/// Gets HTML text from the clipboard. Uses clipboard format <see cref="ClipFormats.Html"/> (<c>"HTML Format"</c>).
		/// </summary>
		/// <returns><c>null</c> if there is no data of this format or if failed to parse it.</returns>
		/// <exception cref="AuException">Failed to open clipboard (after 10 s of wait/retry).</exception>
		public static string getHtml() => getHtml(out _, out _, out _);

		/// <param name="fragmentStart">Fragment start index in the returned string.</param>
		/// <param name="fragmentLength">Fragment length.</param>
		/// <param name="sourceURL">Source URL, or <c>null</c> if unavailable.</param>
		/// <inheritdoc cref="getHtml()"/>
		public static string getHtml(out int fragmentStart, out int fragmentLength, out string sourceURL) {
			return ParseHtmlFormatData_(getBinary(ClipFormats.Html), out fragmentStart, out fragmentLength, out sourceURL);
		}

		internal static string ParseHtmlFormatData_(byte[] b, out int fragmentStart, out int fragmentLength, out string sourceURL) {
			//print.it(s);
			fragmentStart = fragmentLength = 0; sourceURL = null;
			if (b == null) return null;
			string s = Encoding.UTF8.GetString(b);

			int ish = s.Find("StartHTML:", true);
			int ieh = s.Find("EndHTML:", true);
			int isf = s.Find("StartFragment:", true);
			int ief = s.Find("EndFragment:", true);
			if (ish < 0 || ieh < 0 || isf < 0 || ief < 0) return null;
			isf = s.ToInt(isf + 14); if (isf < 0) return null;
			ief = s.ToInt(ief + 12); if (ief < isf) return null;
			ish = s.ToInt(ish + 10); if (ish < 0) ish = isf; else if (ish > isf) return null;
			ieh = s.ToInt(ieh + 8); if (ieh < 0) ieh = ief; else if (ieh < ief) return null;

			if (s.Length != b.Length) {
				if (ieh > b.Length) return null;
				_CorrectOffset(ref isf);
				_CorrectOffset(ref ief);
				_CorrectOffset(ref ish);
				_CorrectOffset(ref ieh);
			} else if (ieh > s.Length) return null;
			//print.it(ish, ieh, isf, ief);

			int isu = s.Find("SourceURL:", true), ieu;
			if (isu >= 0 && (ieu = s.FindAny("\r\n", (isu += 10)..)) >= 0) sourceURL = s[isu..ieu];

			fragmentStart = isf - ish; fragmentLength = ief - isf;
			return s[ish..ieh];

			void _CorrectOffset(ref int i) {
				i = Encoding.UTF8.GetCharCount(b, 0, i);
			}
		}

		/// <summary>
		/// Gets file paths from the clipboard. Uses clipboard format <see cref="ClipFormats.Files"/> (<b>CF_HDROP</b>).
		/// </summary>
		/// <returns><c>null</c> if there is no data of this format.</returns>
		/// <exception cref="AuException">Failed to open clipboard (after 10 s of wait/retry).</exception>
		public static string[] getFiles() {
			using (new clipboard.OpenClipboard_(false)) {
				var h = Api.GetClipboardData(Api.CF_HDROP); if (h == default) return null;
				return HdropToFiles_(h);
			}
		}

		/// <summary>
		/// Gets file paths from HDROP.
		/// </summary>
		/// <returns>Array of zero or more non-<c>null</c> elements.</returns>
		internal static unsafe string[] HdropToFiles_(IntPtr hdrop) {
			int n = Api.DragQueryFile(hdrop, -1, null, 0);
			var a = new string[n];
			var b = stackalloc char[500];
			for (int i = 0; i < n; i++) {
				int len = Api.DragQueryFile(hdrop, i, b, 500);
				a[i] = new string(b, 0, len);
			}
			return a;
		}

		#endregion

		#region contains

		/// <summary>
		/// Returns <c>true</c> if the clipboard contains data of the specified format.
		/// </summary>
		/// <param name="format">Clipboard format id. See <see cref="ClipFormats"/>.</param>
		/// <remarks>Calls API <msdn>IsClipboardFormatAvailable</msdn>.</remarks>
		public static bool contains(int format) {
			return Api.IsClipboardFormatAvailable(format);
		}

		/// <summary>
		/// Returns the first of the specified formats that is in the clipboard.
		/// Returns 0 if the clipboard is empty. Returns -1 if the clipboard contains data but not in any of the specified formats.
		/// </summary>
		/// <param name="formats">Clipboard format ids. See <see cref="ClipFormats"/>.</param>
		/// <remarks>Calls API <msdn>GetPriorityClipboardFormat</msdn>.</remarks>
		public static int contains(params int[] formats) {
			return Api.GetPriorityClipboardFormat(formats, formats.Length);
		}

		#endregion

		//CONSIDER: EnumFormats, OnClipboardChanged
	}
}

namespace Au.Types {
	/// <summary>
	/// Some clipboard format ids.
	/// These and other standard and registered format ids can be used with <see cref="clipboardData"/> class functions.
	/// </summary>
	public static class ClipFormats {
		/// <summary>The text format. Standard, API constant <b>CF_UNICODETEXT</b>. The default format of <see cref="clipboardData"/> add/get text functions.</summary>
		public const int Text = Api.CF_UNICODETEXT;

		/// <summary>The image format. Standard, API constant <b>CF_BITMAP</b>. Used by <see cref="clipboardData"/> add/get image functions.</summary>
		public const int Image = Api.CF_BITMAP;

		/// <summary>The file-list format. Standard, API constant <b>CF_HDROP</b>. Used by <see cref="clipboardData"/> add/get files functions.</summary>
		public const int Files = Api.CF_HDROP;

		/// <summary>The HTML format. Registered, name "HTML Format". Used by <see cref="clipboardData"/> add/get HTML functions.</summary>
		public static int Html { get; } = Api.RegisterClipboardFormat("HTML Format");

		/// <summary>The PNG format. Registered, name "PNG". Used by <see cref="clipboardData"/> add/get image functions.</summary>
		public static int Png { get; } = Api.RegisterClipboardFormat("PNG");

		/// <summary>Registered "Shell IDList Array" format.</summary>
		internal static int ShellIDListArray_ { get; } = Api.RegisterClipboardFormat("Shell IDList Array");

		/// <summary>Registered "FileGroupDescriptorW" format.</summary>
		internal static int FileGroupDescriptorW_ { get; } = Api.RegisterClipboardFormat("FileGroupDescriptorW");

		/// <summary>
		/// The "Clipboard Viewer Ignore" registered format.
		/// </summary>
		/// <remarks>
		/// Some clipboard viewer/manager programs don't try to get clipboard data if this format is present. For example Ditto, Clipdiary.
		/// The copy/paste functions of this library add this format to the clipboard to avoid displaying the temporary text/data in these programs, which also could make the paste function slower and less reliable.
		/// </remarks>
		public static int ClipboardViewerIgnore { get; } = Api.RegisterClipboardFormat("Clipboard Viewer Ignore");

		/// <summary>
		/// Registers a clipboard format and returns its id. If already registered, just returns id.
		/// </summary>
		/// <param name="name">Format name.</param>
		/// <param name="textEncoding">Text encoding, if it's a text format. Used by <see cref="clipboardData.getText"/>, <see cref="clipboardData.AddText"/> and functions that call them. For example <see cref="Encoding.UTF8"/>. If <c>null</c>, text of unknown formats is considered Unicode UTF-16 (no encoding/decoding needed).</param>
		/// <remarks>Calls API <msdn>RegisterClipboardFormat</msdn>.</remarks>
		public static int Register(string name, Encoding textEncoding = null) {
			var R = Api.RegisterClipboardFormat(name);
			if (textEncoding != null && R != 0 && R != Html) s_textEncoding[R] = textEncoding;
			return R;
		}

		static readonly ConcurrentDictionary<int, Encoding> s_textEncoding = new();

		/// <summary>
		/// Gets text encoding for format.
		/// Returns <c>null</c> if UTF-16 or if the format is unknown and not in s_textEncoding.
		/// </summary>
		internal static Encoding GetTextEncoding_(int format, out bool unknown) {
			unknown = false;
			if (format is 0 or Api.CF_UNICODETEXT or Api.CF_HDROP) return null;
			if (format < Api.CF_MAX) return Encoding.Default; //never mind: it is UTF-8, not ANSI. Rarely used, especially with non-ASCII text.
			if (format == Html) return Encoding.UTF8;
			if (s_textEncoding.TryGetValue(format, out var enc)) return enc == Encoding.Unicode ? null : enc;
			unknown = true;
			return null;
		}

		/// <summary>
		/// Gets clipboard format name.
		/// </summary>
		/// <param name="format">A registered or standard clipboard format. If standard, returns string like <c>"CF_BITMAP"</c>.</param>
		/// <param name="orNull">Return <c>null</c> if <i>format</i> is unknown. If <c>false</c>, returns <i>format</i> as string.</param>
		/// <remarks>
		/// Calls API <msdn>GetClipboardFormatName</msdn>. Although undocumented, it also can get other strings from the same system atom table, for example registered Windows message names and window class names.
		/// </remarks>
		[SkipLocalsInit]
		public static unsafe string GetName(int format, bool orNull = false) {
			//registered
			if (format >= 0xC000 && format <= 0xffff) {
				var b = stackalloc char[300];
				int len = Api.GetClipboardFormatName(format, b, 300);
				if (len > 0) return new string(b, 0, len);
			}
			//standard
			var s = format switch { Api.CF_TEXT => "CF_TEXT", Api.CF_BITMAP => "CF_BITMAP", Api.CF_METAFILEPICT => "CF_METAFILEPICT", Api.CF_SYLK => "CF_SYLK", Api.CF_DIF => "CF_DIF", Api.CF_TIFF => "CF_TIFF", Api.CF_OEMTEXT => "CF_OEMTEXT", Api.CF_DIB => "CF_DIB", Api.CF_PALETTE => "CF_PALETTE", Api.CF_RIFF => "CF_RIFF", Api.CF_WAVE => "CF_WAVE", Api.CF_UNICODETEXT => "CF_UNICODETEXT", Api.CF_ENHMETAFILE => "CF_ENHMETAFILE", Api.CF_HDROP => "CF_HDROP", Api.CF_LOCALE => "CF_LOCALE", Api.CF_DIBV5 => "CF_DIBV5", _ => null };
			return s ?? (orNull ? null : format.ToS());
		}

		/// <summary>
		/// Gets formats currently in the clipboard.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// foreach (var f in ClipFormats.EnumClipboard()) {
		/// 	print.it(ClipFormats.GetName(f));
		/// }
		/// ]]></code>
		/// </example>
		public static IEnumerable<int> EnumClipboard() {
			using var oc = new clipboard.OpenClipboard_(true);
			for (int format = 0; 0 != (format = Api.EnumClipboardFormats(format));) {
				yield return format;
			}
		}
	}
}
