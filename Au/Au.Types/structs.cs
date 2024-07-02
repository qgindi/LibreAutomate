using System.Drawing;
using System.Text.Json.Serialization;

namespace Au.Types {
	/// <summary>
	/// Point coordinates x y.
	/// </summary>
	public record struct POINT {
#pragma warning disable 1591, 3008 //XML doc, CLS-compliant
		[JsonInclude]
		public int x, y;
		
		public POINT(int x, int y) { this.x = x; this.y = y; }
		
		public static implicit operator POINT((int x, int y) t) => new(t.x, t.y);
		
		public static implicit operator POINT(Point p) => new(p.X, p.Y);
		
		public static implicit operator Point(POINT p) => new(p.x, p.y);
		
		public static implicit operator PointF(POINT p) => new(p.x, p.y);
		
		public static implicit operator System.Windows.Point(POINT p) => new(p.x, p.y);
		
		/// <param name="p"></param>
		/// <param name="round">Can round up, for example 1.7 to 2.</param>
		/// <exception cref="OverflowException"></exception>
		public static POINT From(PointF p, bool round)
			=> new(round ? p.X.ToInt() : checked((int)p.X), round ? p.Y.ToInt() : checked((int)p.Y));
		
		/// <param name="p"></param>
		/// <param name="round">Can round up, for example 1.7 to 2.</param>
		/// <exception cref="OverflowException"></exception>
		public static POINT From(System.Windows.Point p, bool round)
			=> new(round ? p.X.ToInt() : checked((int)p.X), round ? p.Y.ToInt() : checked((int)p.Y));
		
		//rejected
		///// <summary>Specifies position relative to the primary screen or its work area. Calls <see cref="Coord.Normalize"/> with <i>centerIfEmpty</i> <c>true</c>.</summary>
		//public static implicit operator POINT((Coord x, Coord y, bool workArea) t) => _Coord(t.x, t.y, t.workArea, default);
		///// <summary>Specifies position relative to the specified screen or its work area. Calls <see cref="Coord.Normalize"/> with <i>centerIfEmpty</i> <c>true</c>.</summary>
		//public static implicit operator POINT((Coord x, Coord y, screen screen, bool workArea) t) => _Coord(t.x, t.y, t.workArea, t.screen);
		///// <summary>Specifies position in the specified rectangle which is relative to the primary screen. Calls <see cref="Coord.NormalizeInRect"/> with <i>centerIfEmpty</i> <c>true</c>.</summary>
		//public static implicit operator POINT((RECT r, Coord x, Coord y) t) => Coord.NormalizeInRect(t.x, t.y, t.r, centerIfEmpty: true);
		//static POINT _Coord(Coord x, Coord y, bool workArea, screen screen) => Coord.Normalize(x, y, workArea, screen, centerIfEmpty: true);
		
		//maybe in the future
		///// <summary>
		///// Converts <see cref="Coord"/> coordinates into real coordinates.
		///// Calls <see cref="Coord.Normalize"/> with <i>centerIfEmpty</i> <c>true</c>.
		///// </summary>
		//public static POINT Normalize(Coord x, Coord y, bool workArea = false, screen screen = default)
		//	=> Coord.Normalize(x, y, workArea, screen, centerIfEmpty: true);
		
		//public static POINT NormalizeIn(RECT r, Coord x = default, Coord y = default)
		//	=> Coord.NormalizeInRect(x, y, r, centerIfEmpty: true);
		
		/// <summary><c>this.x += x; this.y += y;</c></summary>
		public void Offset(int x, int y) { this.x += x; this.y += y; }
		
		/// <summary>Returns <c>new POINT(p.x + d.x, p.y + d.y)</c>.</summary>
		public static POINT operator +(POINT p, (int x, int y) d) => new(p.x + d.x, p.y + d.y);
		
		public void Deconstruct(out int x, out int y) { x = this.x; y = this.y; }
		
		public override string ToString() => $"{{{x.ToS()}, {y.ToS()}}}";
#pragma warning restore 1591 //XML doc
	}
	
	/// <summary>
	/// Width and height.
	/// </summary>
	public record struct SIZE {
#pragma warning disable 1591, 3008 //XML doc, CLS-compliant
		[JsonInclude]
		public int width, height;
		
		public SIZE(int width, int height) { this.width = width; this.height = height; }
		
		public static implicit operator SIZE((int width, int height) t) => new(t.width, t.height);
		
		public static implicit operator SIZE(Size z) => new(z.Width, z.Height);
		
		public static implicit operator Size(SIZE z) => new(z.width, z.height);
		
		public static implicit operator SizeF(SIZE z) => new(z.width, z.height);
		
		public static implicit operator System.Windows.Size(SIZE z) => new(z.width, z.height);
		
		/// <param name="z"></param>
		/// <param name="round">Can round up, for example 1.7 to 2.</param>
		/// <exception cref="OverflowException"></exception>
		public static SIZE From(SizeF z, bool round)
			=> new(round ? z.Width.ToInt() : checked((int)z.Width), round ? z.Height.ToInt() : checked((int)z.Height));
		
		/// <param name="z"></param>
		/// <param name="round">Can round up, for example 1.7 to 2.</param>
		/// <exception cref="OverflowException"></exception>
		public static SIZE From(System.Windows.Size z, bool round)
			=> new(round ? z.Width.ToInt() : checked((int)z.Width), round ? z.Height.ToInt() : checked((int)z.Height));
		
		/// <summary>Returns <c>new SIZE(z.width + d.x, z.height + d.y)</c>.</summary>
		public static SIZE operator +(SIZE z, (int x, int y) d) => new(z.width + d.x, z.height + d.y);
		
		public void Deconstruct(out int width, out int height) { width = this.width; height = this.height; }
		
		public override string ToString() => $"{{{width.ToS()}, {height.ToS()}}}";
#pragma warning restore 1591 //XML doc
	}
	
	/// <summary>
	/// Rectangle coordinates left top right bottom.
	/// </summary>
	/// <remarks>
	/// This type can be used with Windows API functions. The .NET <b>Rectangle</b> etc can't, because their fields are different.
	/// Has conversions from/to <b>Rectangle</b>.
	/// </remarks>
	public record struct RECT {
#pragma warning disable 1591, 3008 //XML doc, CLS-compliant
		[JsonInclude]
		public int left, top;
		public int right, bottom;
		
		/// <summary>
		/// Sets all fields.
		/// </summary>
		/// <remarks>
		/// Sets <c>right = left + width; bottom = top + height;</c>. To specify right/bottom instead of width/height, use <see cref="FromLTRB"/> instead.
		/// </remarks>
		[JsonConstructor] //without it JSON deserializer sets incorrect Width/Height because does it before setting left/right
		public RECT(int left, int top, int width, int height) {
			this.left = left; this.top = top;
			right = left + width; bottom = top + height;
		}
		
		/// <summary>
		/// Creates <b>RECT</b> with specified <b>left</b>, <b>top</b>, <b>right</b> and <b>bottom</b>.
		/// </summary>
		public static RECT FromLTRB(int left, int top, int right, int bottom)
			=> new() { left = left, top = top, right = right, bottom = bottom };
		
		/// <summary>
		/// Converts from tuple (left, top, width, height).
		/// </summary>
		public static implicit operator RECT((int L, int T, int W, int H) t) => new(t.L, t.T, t.W, t.H);
		
		public static implicit operator RECT(Rectangle r) => new(r.Left, r.Top, r.Width, r.Height);
		
		public static implicit operator Rectangle(RECT r) => new(r.left, r.top, r.Width, r.Height);
		
		public static implicit operator RectangleF(RECT r) => new(r.left, r.top, r.Width, r.Height);
		
		public static implicit operator System.Windows.Rect(RECT r) => new(r.left, r.top, r.Width, r.Height);
		
		/// <param name="r"></param>
		/// <param name="round">Can round up, for example 1.7 to 2.</param>
		/// <exception cref="OverflowException"></exception>
		public static RECT From(RectangleF r, bool round) {
			if (round) return new(r.Left.ToInt(), r.Top.ToInt(), r.Width.ToInt(), r.Height.ToInt());
			checked { return new((int)r.Left, (int)r.Top, (int)r.Width, (int)r.Height); }
		}
		
		/// <param name="r"></param>
		/// <param name="round">Can round up, for example 1.7 to 2.</param>
		/// <exception cref="OverflowException"></exception>
		public static RECT From(System.Windows.Rect r, bool round) {
			if (round) return new(r.Left.ToInt(), r.Top.ToInt(), r.Width.ToInt(), r.Height.ToInt());
			checked { return new((int)r.Left, (int)r.Top, (int)r.Width, (int)r.Height); }
		}
		
		//rejected. Rare.
		///// <summary>
		///// Sets fields like constructor <see cref="RECT(int,int,int,int)"/>.
		///// </summary>
		//public void Set(int left, int top, int rightOrWidth, int bottomOrHeight, bool useWidthHeight = true) {
		//	this.left = left; this.top = top;
		//	right = rightOrWidth; bottom = bottomOrHeight;
		//	if (useWidthHeight) { right += left; bottom += top; }
		//}
		
		/// <summary>
		/// Returns <c>true</c> if all fields == 0.
		/// </summary>
		public bool Is0 => left == 0 && top == 0 && right == 0 && bottom == 0;
		
		/// <summary>
		/// Returns <c>true</c> if the rectangle area is empty or invalid: <c>right&lt;=left || bottom&lt;=top;</c>
		/// </summary>
		public bool NoArea => right <= left || bottom <= top;
		
		/// <summary>
		/// Gets or sets width.
		/// </summary>
		public int Width { get => right - left; set { right = left + value; } }
		
		/// <summary>
		/// Gets or sets height.
		/// </summary>
		public int Height { get => bottom - top; set { bottom = top + value; } }
		
		/// <summary>
		/// Returns <c>new POINT(left, top)</c>.
		/// </summary>
		public POINT XY => new(left, top);
		
		/// <summary>
		/// Returns <c>new SIZE(Width, Height)</c>.
		/// </summary>
		public SIZE Size => new(Width, Height);
		
		/// <summary>
		/// Gets horizontal center.
		/// </summary>
		public int CenterX => left + (right - left) / 2;
		//public int CenterX => (int)(((long)left + right) / 2);
		
		/// <summary>
		/// Gets vertical center.
		/// </summary>
		public int CenterY => top + (bottom - top) / 2;
		//public int CenterY => (int)(((long)top + bottom) / 2);
		
		/// <summary>
		/// Returns <c>true</c> if this rectangle contains the specified point.
		/// </summary>
		public bool Contains(int x, int y) => x >= left && x < right && y >= top && y < bottom;
		
		/// <summary>
		/// Returns <c>true</c> if this rectangle contains the specified point.
		/// </summary>
		public bool Contains(POINT p) => Contains(p.x, p.y);
		
		/// <summary>
		/// Returns <c>true</c> if this rectangle contains entire specified rectangle.
		/// </summary>
		public bool Contains(RECT r2) => r2.left >= left && r2.top >= top && r2.right <= right && r2.bottom <= bottom;
		
		/// <summary>
		/// Makes this rectangle bigger or smaller: <c>left-=dx; right+=dx; top-=dy; bottom+=dy;</c>
		/// Use negative <i>dx</i>/<i>dy</i> to make the rectangle smaller. Note: too big negative <i>dx</i>/<i>dy</i> can make it invalid (<b>right</b>&lt;<b>left</b> or <b>bottom</b>&lt;<b>top</b>).
		/// </summary>
		public void Inflate(int dx, int dy) { left -= dx; right += dx; top -= dy; bottom += dy; }
		
		/// <summary>
		/// Replaces this rectangle with the intersection of itself and the specified rectangle.
		/// If the rectangles don't intersect, makes this variable empty.
		/// </summary>
		/// <returns><c>true</c> if the rectangles intersect.</returns>
		public bool Intersect(RECT r2) => Api.IntersectRect(out this, this, r2);
		
		/// <summary>
		/// Returns the intersection rectangle of two rectangles.
		/// If they don't intersect, returns empty rectangle.
		/// </summary>
		public static RECT Intersect(RECT r1, RECT r2) { Api.IntersectRect(out RECT r, r1, r2); return r; }
		
		/// <summary>
		/// Returns <c>true</c> if this rectangle and another rectangle intersect.
		/// </summary>
		public bool IntersectsWith(RECT r2) => Api.IntersectRect(out _, this, r2);
		
		/// <summary>
		/// Moves this rectangle by the specified offsets: <c>left+=dx; right+=dx; top+=dy; bottom+=dy;</c>
		/// Negative <i>dx</i> moves to the left. Negative <i>dy</i> moves up.
		/// </summary>
		public void Offset(int dx, int dy) { left += dx; right += dx; top += dy; bottom += dy; }
		
		/// <summary>
		/// Moves this rectangle so that <b>left</b>=<i>x</i> and <b>right</b>=<i>y</i>. Does not change <b>Width</b> and <b>Height</b>.
		/// </summary>
		public void Move(int x, int y) => Offset(x - left, y - top);
		
		/// <summary>
		/// Replaces this rectangle with the union of itself and the specified rectangle.
		/// Union is the smallest rectangle that contains two full rectangles.
		/// If either rectangle is empty (<b>Width</b> or <b>Height</b> is &lt;=0), the result is another rectangle. If both empty - empty rectangle.
		/// </summary>
		/// <returns><c>true</c> if finally this rectangle is not empty.</returns>
		public bool Union(RECT r2) => Api.UnionRect(out this, this, r2);
		
		/// <summary>
		/// Returns the union of two rectangles.
		/// Union is the smallest rectangle that contains two full rectangles.
		/// If either rectangle is empty (<b>Width</b> or <b>Height</b> is &lt;=0), the result is another rectangle. If both empty - empty rectangle.
		/// </summary>
		public static RECT Union(RECT r1, RECT r2) { Api.UnionRect(out RECT r, r1, r2); return r; }
		
		/// <summary>
		/// If <b>width</b> or <b>height</b> are negative, modifies this rectangle so that they would not be negative.
		/// </summary>
		/// <param name="swap"><c>true</c> - swap <b>right</b>/<b>left</b>, <b>bottom</b>/<b>top</b>; <c>false</c> - set <b>right</b> = <b>left</b>, <b>bottom</b> = <b>top</b>.</param>
		public void Normalize(bool swap) {
			if (right < left) { if (swap) Math2.Swap(ref left, ref right); else right = left; }
			if (bottom < top) { if (swap) Math2.Swap(ref top, ref bottom); else bottom = top; }
		}
		
		/// <summary>
		/// Moves this rectangle to the specified coordinates in the specified screen, and ensures that whole rectangle is in screen.
		/// Final rectangle coordinates are relative to the primary screen.
		/// </summary>
		/// <param name="x">X coordinate in the specified screen. If <c>default</c> - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate in the specified screen. If <c>default</c> - center.</param>
		/// <param name="screen">Use this screen. If <c>default</c>, uses the primary screen. Example: <c>screen.index(1)</c>.</param>
		/// <param name="workArea">Use the work area, not whole screen. Default <c>true</c>.</param>
		/// <param name="ensureInScreen">If part of rectangle is not in screen, move and/or resize it so that entire rectangle would be in screen. Default <c>true</c>.</param>
		/// <remarks>
		/// This function can be used to calculate new window location before creating it. If window already exists, use <see cref="wnd.MoveInScreen"/>.
		/// </remarks>
		public void MoveInScreen(Coord x, Coord y, screen screen = default, bool workArea = true, bool ensureInScreen = true) {
			wnd.Internal_.MoveRectInScreen(false, ref this, x, y, screen, workArea, ensureInScreen);
		}
		
		/// <summary>
		/// Moves this rectangle to the specified coordinates in another rectangle <i>r</i>.
		/// </summary>
		/// <param name="r">Another rectangle.</param>
		/// <param name="x">X coordinate relative to <i>r</i>. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate relative to <i>r</i>. Default - center.</param>
		/// <param name="ensureInRect">If part of rectangle is not in <i>r</i>, move and/or resize it so that entire rectangle would be in <i>r</i>.</param>
		public void MoveInRect(RECT r, Coord x = default, Coord y = default, bool ensureInRect = false) {
			wnd.Internal_.MoveRectInRect(ref this, x, y, r, ensureInRect);
		}
		
		/// <summary>
		/// Adjusts this rectangle to ensure that whole rectangle is in screen.
		/// Initial and final rectangle coordinates are relative to the primary screen.
		/// </summary>
		/// <param name="screen">Use this screen (see <see cref="screen"/>). If <c>default</c>, uses screen of the rectangle (or nearest).</param>
		/// <param name="workArea">Use the work area, not whole screen. Default <c>true</c>.</param>
		/// <remarks>
		/// This function can be used to calculate new window location before creating it. If window already exists, use <see cref="wnd.EnsureInScreen"/>.
		/// </remarks>
		public void EnsureInScreen(screen screen = default, bool workArea = true) {
			wnd.Internal_.MoveRectInScreen(true, ref this, default, default, screen, workArea, true);
		}
		
		/// <summary>
		/// Returns <c>true</c> if all fields of <i>r1</i> and <i>r2</i> are equal or almost equal with max difference +- <i>maxDiff</i>.
		/// </summary>
		internal static bool EqualFuzzy_(RECT r1, RECT r2, int maxDiff) {
			if (Math.Abs(r1.left - r2.left) > maxDiff) return false;
			if (Math.Abs(r1.top - r2.top) > maxDiff) return false;
			if (Math.Abs(r1.right - r2.right) > maxDiff) return false;
			if (Math.Abs(r1.bottom - r2.bottom) > maxDiff) return false;
			return true;
		}
		
		/// <summary>
		/// Converts to string <c>"{L=left T=top W=width H=height}"</c>.
		/// </summary>
		/// <seealso cref="TryParse"/>
		public override string ToString() {
			return $"{{L={left.ToS()} T={top.ToS()} W={Width.ToS()} H={Height.ToS()}}}";
			//note: don't change the format. Some functions parse it, eg TryParse and acc in C++.
			
			//don't need R B. Rarely useful, just makes more difficult to read W H.
			//return $"{{L={left} T={top} R={right} B={bottom}  W={Width} H={Height}}}";
		}
		
		/// <summary>
		/// Converts to string <c>"left top width height"</c>.
		/// </summary>
		/// <seealso cref="TryParse"/>
		public string ToStringSimple() {
			return $"{left.ToS()} {top.ToS()} {Width.ToS()} {Height.ToS()}";
		}
		
		/// <summary>
		/// Formats string from <b>RECT</b> main fields and properties.
		/// </summary>
		/// <param name="format">
		/// <see cref="StringBuilder.AppendFormat"/> format string. Example: <c>"({0}, {1}, {4}, {5})"</c>.
		/// This function passes to <b>AppendFormat</b> 6 values in this order: <b>left</b>, <b>top</b>, <b>right</b>, <b>bottom</b>, <b>Width</b>, <b>Height</b>.
		/// </param>
		public string ToStringFormat(string format) {
			using (new StringBuilder_(out var b)) {
				b.AppendFormat(format, left.ToS(), top.ToS(), right.ToS(), bottom.ToS(), Width.ToS(), Height.ToS());
				return b.ToString();
			}
		}
		
		/// <summary>
		/// Converts string to <b>RECT</b>.
		/// </summary>
		/// <returns><c>false</c> if invalid string format.</returns>
		/// <param name="s">String in format <c>"{L=left T=top W=width H=height}"</c> (<see cref="ToString"/>) or <c>"left top width height"</c> (<see cref="ToStringSimple"/>).</param>
		/// <param name="r"></param>
		public static bool TryParse(string s, out RECT r) {
			r = default;
			bool ok;
			if (s.Starts('{')) {
				ok = s.Eq(1, "L=") && s.ToInt(out r.left, 3, out int e)
					&& s.Eq(e, " T=") && s.ToInt(out r.top, e + 3, out e)
					&& s.Eq(e, " W=") && s.ToInt(out r.right, e + 3, out e)
					&& s.Eq(e, " H=") && s.ToInt(out r.bottom, e + 3, out e)
					&& s.Length == e + 1 && s[e] == '}';
				//tested: regex @"^\{L=(-?\d+) T=(-?\d+) W=(-?\d+) H=(-?\d+)\}$" 9 times slower.
			} else {
				ok = s.ToInt(out r.left, 0, out int e) && s.ToInt(out r.top, e, out e) && s.ToInt(out r.right, e, out e) && s.ToInt(out r.bottom, e);
			}
			if (ok) {
				r.right += r.left;
				r.bottom += r.top;
			}
			return ok;
		}
#pragma warning restore 1591 //XML doc
	}

	/// <summary>
	/// Struct with fields <c>int start</c> and <c>int end</c>.
	/// </summary>
	public record struct StartEnd {
		///
		public int start;
		///
		public int end;

		///
		public StartEnd(int start, int end) { this.start = start; this.end = end; }

		/// <summary>
		/// Returns <c>end - start</c>.
		/// </summary>
		public int Length => end - start;

		/// <summary>
		/// Converts this to <see cref="Range"/>.
		/// Can be used to get substring, like <c>s[x.Range]</c> instead of <c>s[x.start..x.end]</c>.
		/// </summary>
		public Range Range => start..end;

		//public static implicit operator Range(StartEnd s) => s.start..s.end; //could be used to get substring like s[se], but error

		/// <summary>
		/// Gets string span.
		/// </summary>
		public RStr Span(string s) => s.AsSpan(start, end - start);

		///
		public override string ToString() => $"({start}, {end})";
	}
	
	internal unsafe struct VARIANT : IDisposable {
		public Api.VARENUM vt; //ushort
		ushort _u1;
		uint _u2;
		public nint value;
		public nint value2;
		//note: cannot use FieldOffset because of different 32/64 bit size
		
		public VARIANT(int x) { vt = Api.VARENUM.VT_I4; value = x; }
		public VARIANT(string x) { vt = Api.VARENUM.VT_BSTR; value = Marshal.StringToBSTR(x); }
		
		public static implicit operator VARIANT(int x) => new(x);
		public static implicit operator VARIANT(string x) => new(x);
		
		public int ValueInt { get { Debug.Assert(vt == Api.VARENUM.VT_I4); return (int)value; } }
		public BSTR ValueBstr { get { Debug.Assert(vt == Api.VARENUM.VT_BSTR); return BSTR.AttachBSTR((char*)value); } }
		
		/// <summary>
		/// Calls <b>VariantClear</b>.
		/// </summary>
		public void Dispose() {
			_Clear();
		}
		
		void _Clear() {
			if (vt >= Api.VARENUM.VT_BSTR) Api.VariantClear(ref this);
			else vt = 0; //info: VariantClear just sets vt=0 and does not clear other members
		}
		
		/// <summary>
		/// Converts to string.
		/// </summary>
		public override string ToString() {
			return _ToString();
		}
		
		string _ToString() {
			switch (vt) {
			case Api.VARENUM.VT_BSTR: return value == default ? null : ValueBstr.ToString();
			case Api.VARENUM.VT_I4: return value.ToString();
			case 0: case Api.VARENUM.VT_NULL: return null;
			}
			VARIANT v2 = default;
			uint lcid = 0x409; //invariant
			switch (vt & (Api.VARENUM)0xff) { case Api.VARENUM.VT_DATE: case Api.VARENUM.VT_DISPATCH: lcid = 0x400; break; } //LOCALE_USER_DEFAULT
			if (0 != Api.VariantChangeTypeEx(ref v2, this, lcid, 2, Api.VARENUM.VT_BSTR)) return null; //2 VARIANT_ALPHABOOL
			return v2.value == default ? null : v2.ValueBstr.ToStringAndDispose();
		}
		
		/// <summary>
		/// Converts to string.
		/// Disposes this <b>VARIANT</b>.
		/// </summary>
		public string ToStringAndDispose() {
			var r = _ToString();
			Dispose();
			return r;
		}
	}
	
	internal unsafe struct BSTR : IDisposable {
		char* _p;
		
		BSTR(char* p) => _p = p;
		
		public static explicit operator BSTR(string s) => new((char*)Marshal.StringToBSTR(s));
		public static explicit operator nint(BSTR b) => (nint)b._p;
		
		public static BSTR AttachBSTR(char* bstr) => new(bstr);
		
		public static BSTR CopyFrom(char* anyString) => anyString == null ? default : Api.SysAllocString(anyString);
		
		public static BSTR Alloc(int len) => Api.SysAllocStringLen(null, len);
		
		public char* Ptr => _p;
		
		/// <summary>
		/// Returns <c>true</c> if the string is <c>null</c>.
		/// </summary>
		public bool Is0 => _p == null;
		
		public int Length => _p == null ? 0 : Api.SysStringLen(_p);
		
		/// <summary>
		/// Unsafe.
		/// </summary>
		public char this[int i] => _p[i];
		
		/// <summary>
		/// Converts to string.
		/// Does not dispose.
		/// </summary>
		public override string ToString() {
			var p = _p; if (p == null) return null;
			return Marshal.PtrToStringBSTR((IntPtr)_p);
		}
		
		/// <summary>
		/// Converts to string and disposes.
		/// </summary>
		public string ToStringAndDispose() {
			var p = _p; if (p == null) return null;
			int len = Api.SysStringLen(p);
			
			//rejected:
			//Some objects can return BSTR containing '\0's. Then probably the rest of string is garbage. I never noticed this but saw comments. Better allow '\0's, because in some cases it can be valid string. When invalid, it will not harm too much.
			//int len2 = Ptr_.Length(p, len); Debug_.PrintIf(len2 != len, "BSTR with '\\0'"); len = len2;
			
			string r = len == 0 ? "" : new string(p, 0, len);
			Dispose();
			return r;
		}
		
		public void Dispose() {
			var t = _p;
			if (t != null) {
				_p = null;
				Api.SysFreeString(t);
			}
		}
	}
	
}
