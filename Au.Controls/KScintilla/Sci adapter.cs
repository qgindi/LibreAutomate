namespace Au.Controls;

using static Sci;

public unsafe partial class KScintilla {
	_Adapter _adapter; //created in KScintilla ctor
	
	/// <summary>
	/// Gets or sets text.
	/// Uses caching, therefore the <c>get</c> function is fast and garbage-free when calling multiple times.
	/// </summary>
	/// <remarks>
	/// The <c>get</c> function gets cached text if called not the first time after setting or modifying control text.
	/// The <c>set</c> function calls <see cref="aaaSetText"/> when need. Uses default parameters (with undo and notifications, unless <b>AaInitReadOnlyAlways</b>).
	/// Unlike the above methods, this property can be used before creating handle.
	/// </remarks>
	public string aaaText {
		get => _adapter.Text;
		set { _adapter.Text = value; }
	}
	
	/// <summary>
	/// UTF-8 text length.
	/// </summary>
	public int aaaLen8 => _adapter.Len8;
	
	/// <summary>
	/// UTF-16 text length.
	/// </summary>
	public int aaaLen16 => _adapter.Len16;
	
	/// <summary>
	/// Converts UTF-16 position to UTF-8 position. Fast.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">Negative or greater than <see cref="aaaLen16"/>.</exception>
	public int aaaPos8(int pos16) => _adapter.Pos8(pos16);
	
	/// <summary>
	/// Converts UTF-8 position to UTF-16 position. Fast.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">Negative or greater than <see cref="aaaLen8"/>.</exception>
	public unsafe int aaaPos16(int pos8) => _adapter.Pos16(pos8);
	
	/// <summary>
	/// Maps UTF-16 to/from UTF-8 positions.
	/// Gets UTF-16 and UTF-8 length.
	/// Gets cached UTF-16 text.
	/// </summary>
	class _Adapter {
		record struct _NA(int i8, int i16, int len16, int charLen);
		
		readonly KScintilla _sci;
		string _text;
		readonly List<_NA> _a;
		int _len8, _len16;
		bool _mapDone;
		
		public _Adapter(KScintilla sci) {
			_sci = sci;
			_a = new();
		}
		
		public void HandleCreated() {
			if (!_text.NE()) _sci.aaaSetText(_text, SciSetTextFlags.NoUndoNoNotify);
		}
		
		public void TextModified() {
			_text = null;
			_a.Clear();
			_mapDone = false;
		}
		
		string _GetText() => _sci._RangeText(0, Len8);
		
		public string Text {
			get {
				//if (_sci.Name == "document") print.qm2.write($"Text: cached={_text != null}");
				if (_text == null && !_sci._w.Is0) _text = _GetText(); //_NotifyModified sets _text=null
				return _text;
			}
			set {
				if (_sci._w.Is0) _text = value; //will set control text on WM_CREATE
				else _sci.aaaSetText(value); //_NotifyModified sets _text=null. Control text can be != value, eg when tags parsed.
			}
		}
		
		public int Len8 => _mapDone ? _len8 : _sci.Call(SCI_GETTEXTLENGTH);
		
		public int Len16 {
			get {
				if (_text != null) return _text.Length;
				if (!_mapDone) _CreatePosMap();
				return _len16;
			}
		}
		
		public int Pos8(int pos16) {
			if (!_mapDone) _CreatePosMap();
			
			Debug.Assert((uint)pos16 <= _len16);
			if ((uint)pos16 > _len16) throw new ArgumentOutOfRangeException(nameof(pos16), $"pos16 = {pos16}, _len16 = {_len16}, _GetText().Length={_GetText().Length}");
			
			//using binary search find max _a[r].i16 that is < pos16
			int r = -1, from = 0, to = _a.Count;
			while (to > from) {
				int m = (from + to) / 2;
				if (_a[m].i16 < pos16) from = (r = m) + 1; else to = m;
			}
			if (r < 0) return pos16; //_a is empty (ASCII text) or pos16 <= _a[0].i16 (before first non-ASCII character)
			var p = _a[r];
			return p.i8 + Math.Min(pos16 - p.i16, p.len16) * p.charLen + Math.Max(pos16 - (p.i16 + p.len16), 0); //p.i8 + utf + ascii
		}
		
		public unsafe int Pos16(int pos8) {
			if (!_mapDone) _CreatePosMap();
			
			Debug.Assert((uint)pos8 <= _len8);
			if ((uint)pos8 > _len8) throw new ArgumentOutOfRangeException(nameof(pos8), $"pos8 = {pos8}, _len8 = {_len8}, _sci.Call(SCI_GETTEXTLENGTH)={_sci.Call(SCI_GETTEXTLENGTH)}");
			
			//using binary search find max _a[r].i8 that is < pos8
			int r = -1, from = 0, to = _a.Count;
			while (to > from) {
				int m = (from + to) / 2;
				if (_a[m].i8 < pos8) from = (r = m) + 1; else to = m;
			}
			if (r < 0) return pos8; //_a is empty (ASCII text) or pos8 <= _a[0].i8 (before first non-ASCII character)
			var p = _a[r];
			int len8 = p.len16 * p.charLen;
			return p.i16 + Math.Min(pos8 - p.i8, len8) / p.charLen + Math.Max(pos8 - (p.i8 + len8), 0); //p.i16 + utf + ascii
		}
		
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		unsafe void _CreatePosMap() {
			//This func is fast and often garbageless. For code edit controls don't need to optimize to avoid calling it frequently, eg for each added character.
			//Should not be used for output/log controls if called on each "append text".
			
			int textLen;
			int gap = Sci_Range(_sci.AaSciPtr, 0, -1, out var p, out var p2, &textLen);
			int to8 = p2 == null ? textLen : gap;
			int i8 = 0, i16 = 0;
			for (; ; ) {
				//ASCII range
				int start8 = i8;
				int lenAscii8 = new RByte(p + i8, to8 - i8).IndexOfAnyExceptInRange((byte)0, (byte)127);
				if (lenAscii8 < 0) i8 = to8; else i8 += lenAscii8;
				i16 += i8 - start8;
				
				//non-ASCII range
				if (i8 < to8) {
					start8 = i8;
					int charLen = 0;
					while (i8 < to8 && p[i8] >= 0x80) {
						Rune.DecodeFromUtf8(new(p + i8, to8 - i8), out _, out int nb);
						if (charLen == 0) charLen = nb; else if (nb != charLen) break;
						i8 += nb;
					}
					if (charLen == 4) charLen = 2;
					int len16 = (i8 - start8) / charLen;
					_a.Add(new(start8, i16, len16, charLen));
					i16 += len16;
					if (i8 < to8) continue;
				}
				
				//the part after gap
				if (p2 == null) break;
				p = p2 - i8;
				p2 = null;
				to8 = textLen;
			}
			
			_len8 = textLen;
			_len16 = i16;
			_mapDone = true;
		}
	}
}
