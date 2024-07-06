namespace Au.Controls;

using static Sci;

public unsafe partial class KScintilla {
	public class aaaFileLoaderSaver {
		_Encoding _enc;
		byte[] _text;
		
		public bool IsBinary => _enc == _Encoding.Binary;
		
		public bool IsImage { get; private set; }
		
		/// <summary>
		/// Loads file as UTF-8.
		/// Supports any encoding (UTF-8, UTF-16, etc), BOM. Remembers it for Save.
		/// </summary>
		/// <exception cref="Exception">Exceptions of File.OpenRead, File.Read, Encoding.Convert.</exception>
		public void Load(string file) {
			_enc = _Encoding.Binary;
			IsImage = false;
			if (file.Ends(true, ".png", ".bmp", ".jpg", ".jpeg", ".gif", ".tif", ".tiff", ".ico", ".cur", ".ani") > 0) {
				if (!filesystem.exists(file).File) throw new FileNotFoundException($"Could not find file '{file}'.");
				IsImage = true;
				_text = Encoding.UTF8.GetBytes($"<image \"{file}\">");
				return;
			}
			
			using var fr = filesystem.loadStream(file);
			
			if (fr.Length > 100_000_000) {
				_text = "//Cannot edit. The file is too big, more than 100_000_000 bytes."u8.ToArray();
				return;
			}
			
			int fileSize = (int)fr.Length;
			var b = new byte[fileSize];
			fr.Read(b, 0, fileSize);
			
			_enc = _DetectEncoding(b);
			//print.it(_enc);
			if (_enc == _Encoding.Binary) {
				_text = "//Cannot edit. The file is binary, not text."u8.ToArray();
				return;
			}
			
			if (_enc == _Encoding.Utf8_BOM && !System.Text.Unicode.Utf8.IsValid(b[3..])) b = b.ToStringUTF8().ToUTF8(); //else Scintilla text would be bad
			
			if (_EncodingEnumToObject() is { } e) {
				int bomLength = (int)_enc >>> 4;
				b = Encoding.Convert(e, Encoding.UTF8, b, bomLength, (int)fileSize - bomLength);
			}
			
			_text = b;
		}
		
		Encoding _EncodingEnumToObject() {
			switch (_enc) {
			case _Encoding.Utf16_BOM or _Encoding.Utf16: return Encoding.Unicode;
			case _Encoding.Utf16BE_BOM or _Encoding.Utf16BE: return Encoding.BigEndianUnicode;
			case _Encoding.Utf32_BOM: return Encoding.UTF32;
			case _Encoding.Utf32BE_BOM: return new UTF32Encoding(true, false);
			case _Encoding.Ansi: return StringUtil.GetEncoding(-1);
			}
			return null;
		}
		
		static unsafe _Encoding _DetectEncoding(RByte s) {
			int len = s.Length;
			//is too short to have a BOM?
			if (len == 0) return _Encoding.Utf8;
			if (len == 1) return s[0] == 0 ? _Encoding.Binary : (s[0] < 128 ? _Encoding.Utf8 : _Encoding.Ansi);
			//has a BOM?
			if (s is [0xEF, 0xBB, 0xBF, ..]) return _Encoding.Utf8_BOM;
			if (s is [0xFF, 0xFE, 0, 0, ..]) return _Encoding.Utf32_BOM;
			if (s is [0xFF, 0xFE, ..]) return _Encoding.Utf16_BOM;
			if (s is [0xFE, 0xFF, ..]) return _Encoding.Utf16BE_BOM;
			if (s is [0, 0, 0xFE, 0xFF, ..]) return _Encoding.Utf32BE_BOM;
			//has '\0'?
			int zeroAt = s.IndexOf((byte)0);
			if (zeroAt is 0 or 1 && 0 == (len & 1)) {
				if (MemoryMarshal.Cast<byte, char>(s).Contains('\0')) return _Encoding.Binary;
				return zeroAt is 0 ? _Encoding.Utf16BE : _Encoding.Utf16;
			}
			//if (zeroAt == len - 1) { s = s[..--len]; zeroAt = -1; } //WordPad saves .rtf files with '\0' at the end. Rejected; eg VS and VSCode detects as binary.
			if (zeroAt >= 0) return _Encoding.Binary;
			return System.Text.Unicode.Utf8.IsValid(s) ? _Encoding.Utf8 : _Encoding.Ansi;
		}
		
		enum _Encoding : byte {
			/// <summary>Not a text file, or loading failed, or not initialized.</summary>
			Binary = 0, //must be 0
			
			/// <summary>ASCII or UTF-8 without BOM.</summary>
			Utf8 = 1,
			
			/// <summary>UTF-8 with BOM (3 bytes).</summary>
			Utf8_BOM = 1 | (3 << 4),
			
			/// <summary>ANSI containing non-ASCII characters, unknown code page.</summary>
			Ansi = 2,
			
			/// <summary>UTF-16 without BOM.</summary>
			Utf16 = 3,
			
			/// <summary>UTF-16 big endian without BOM.</summary>
			Utf16BE = 4,
			
			/// <summary>UTF-16 with BOM (2 bytes).</summary>
			Utf16_BOM = 3 | (2 << 4),
			
			/// <summary>UTF-16 with big endian BOM (2 bytes).</summary>
			Utf16BE_BOM = 4 | (2 << 4),
			
			/// <summary>UTF-32 with BOM (4 bytes).</summary>
			Utf32_BOM = 5 | (4 << 4),
			
			/// <summary>UTF-32 with big endian BOM (4 bytes).</summary>
			Utf32BE_BOM = 6 | (4 << 4),
			
			//rejected. .NET does not save/load with UTF-7 BOM, so we too. Several different BOM of different length.
			///// <summary>UTF-7 with BOM.</summary>
			//Utf7_BOM,
		}
		
		/// <summary>
		/// Sets control text.
		/// If the file is image, binary or too big (&gt;100_000_000), sets to display the image or/and some short info text, makes the control read-only, sets <b>Save</b> to throw exception, and returns false. Else returns true.
		/// Uses <see cref="SciSetTextFlags"/> NoUndo and NoNotify.
		/// Must be called once.
		/// </summary>
		public unsafe bool SetText(KScintilla k) {
			RByte text = _text;
			if (_enc == _Encoding.Utf8_BOM) text = text[3..];
			if (_enc == _Encoding.Binary) k.Call(SCI_SETREADONLY); //caller may set AaInitReadOnlyAlways = true
			using (new _NoUndoNotif(k, SciSetTextFlags.NoUndoNoNotify)) {
				k.aaaSetString(SCI_APPENDTEXT, text.Length, text);
			}
			if (_enc != _Encoding.Binary) return true;
			k.Call(SCI_SETREADONLY, 1);
			return false;
		}
		
		/// <summary>
		/// Returns true if text contains newlines other than <c>\r\n</c> and <c>\n</c>.
		/// </summary>
		public bool DetectBadNewlines() {
			if (_enc != _Encoding.Binary) {
				var s = _text;
				for (int i = 0; i < s.Length - 1;) { //ends with '\0'
					switch (s[i++]) {
					case 13 when s[i] != 10: return true;
					case 0xc2 when s[i] == 0x85: return true;
					case 0xe2 when s[i] == 0x80 && s[i + 1] is 0xa8 or 0xa9: return true;
					}
				}
			}
			return false;
		}
		
		public void FinishedLoading() {
			_text = null; //GC
		}
		
		/// <summary>
		/// Saves control text with the same encoding/BOM as loaded. Uses <see cref="filesystem.save"/>.
		/// </summary>
		/// <param name="file">To pass to filesystem.save.</param>
		/// <param name="tempDirectory">To pass to filesystem.save.</param>
		/// <exception cref="Exception">Exceptions of filesystem.save.</exception>
		/// <exception cref="InvalidOperationException">The file is binary (then <b>SetText</b> made the control read-only), or <b>Load</b> not called.</exception>
		public unsafe void Save(KScintilla k, string file, string tempDirectory = null) {
			if (_enc == _Encoding.Binary) throw new InvalidOperationException();
			
			//_enc = _Encoding.; //test
			
			Encoding e = _EncodingEnumToObject();
			
			int bom = (int)_enc >> 4; //BOM length
			uint bomm = 0; //BOM memory
			if (e != null) bomm = _enc switch {
				_Encoding.Utf16_BOM or _Encoding.Utf32_BOM => 0xFEFF,
				_Encoding.Utf16BE_BOM => 0xFFFE,
				_Encoding.Utf32BE_BOM => 0xFFFE0000,
				_ => 0
			};
			else if (bom == 3) bomm = 0xBFBBEF; //UTF8; else bom 0
			
			//print.it(_enc, bom, bomm, e);
			
			filesystem.save(file, temp => {
				using var fs = File.Create(temp);
				if (bomm != 0) { uint u = bomm; fs.Write(new RByte((byte*)&u, bom)); } //rare
				if (e != null) { //rare
					var bytes = e.GetBytes(k.aaaText); //convert encoding. aaaText likely gets cached text, fast
					fs.Write(bytes);
				} else {
					int len = k.aaaLen8;
					var bytes = (byte*)k.CallRetPtr(SCI_GETCHARACTERPOINTER);
					fs.Write(new RByte(bytes, len));
				}
			}, tempDirectory: tempDirectory);
			
			//print.it("file", File.ReadAllBytes(file));
		}
	}
}
