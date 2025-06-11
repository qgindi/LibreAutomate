using System.Windows;
using System.Windows.Media;

namespace Au.More;

/// <summary>
/// Parses XAML icon strings like <c>"*Pack.Name color"</c>.
/// </summary>
record struct IconString_ {
	public readonly string pack, name, color;
	public readonly int size, end;
	public readonly Thickness margin;
	public readonly char stretch;
	public readonly bool snapPixels;
	
	public bool HasValue => name != null;
	public bool HasSize => size is > 0 and <= 16;
	public bool HasMargin => margin != default;
	
	IconString_(RStr s) {
		if (!Detect(s, out var d)) return;
		pack = s[d.pack.Range].ToString();
		name = s[d.name.Range].ToString();
		
		int k = d.name.end;
		end = s.IndexOf(k, ';'); if (end < 0) end = s.Length;
		
		if (s.Eq(k, ' ')) {
			s = s[++k..end];
			Span<Range> a1 = stackalloc Range[3], a2 = stackalloc Range[6];
			foreach (var v in a1[..s.Split(a1, ' ')]) {
				int i = v.Start.Value, j = v.End.Value;
				if (j == i) continue;
				var c = s[i++];
				if (c is '#' or '|' || c.IsAsciiAlpha()) color = s[v].ToString();
				else if (c == '@') size = s[i..].ToInt_();
				else if (c == '%') { //margin
					int n = 0;
					var m = s[i..j];
					foreach (var u in a2[..m.Split(a2, ',')]) {
						n++;
						if (u.End.Value == u.Start.Value) continue;
						if (n == 6) {
							snapPixels = m[u][0] == 'p';
						} else if (n == 5) {
							stretch = m[u][0]; if (!(stretch is 'f' or 'm')) stretch = default;
						} else if (double.TryParse(m[u], CultureInfo.InvariantCulture, out var g)) {
							switch (n) { case 1: margin.Left = g; break; case 2: margin.Top = g; break; case 3: margin.Right = g; break; case 4: margin.Bottom = g; break; }
						}
					}
				}
			}
		}
		//note: don't use `MemoryExtensions.SpanSplitEnumerator<char> RStr.Split<char>(char separator)`. Easier to use, but: 1. Not in .NET 8. 2. 5-10 times slower.
	}
	
	/// <summary>
	/// Parses single-icon string. Format: <c>"[*&lt;library&gt;]*pack.name[ color][ @size][ %margin]"</c>.
	/// </summary>
	/// <returns>false if bad format.</returns>
	public static bool Parse(RStr s, out IconString_ r) {
		r = new(s);
		return r.HasValue;
	}
	
	/// <summary>
	/// Parses single-or-multi-icon string. Format: <c>"[*&lt;library&gt;]*pack.name[ color][ @size][ %margin][;more icons]"</c>.
	/// </summary>
	/// <returns>null if bad format.</returns>
	public static IconString_[] ParseAll(string icon) {
		List<IconString_> a = null;
		for (RStr s = icon; Parse(s, out var r); s = s[(r.end + 1)..].TrimStart()) {
			(a ??= new()).Add(r);
			if (r.end == s.Length || s[r.end] != ';') break;
		}
		return a?.ToArray();
	}
	
	/// <summary>
	/// Returns true if <i>s</i> starts with <c>"*pack.name"</c> or <c>"*&lt;library&gt;*pack.name"</c>.
	/// </summary>
	public static bool Detect(RStr s, out (StartEnd pack, StartEnd name) r) {
		r = default;
		if (s.Length < 8 || s[0] != '*' || !s.Contains('.')) return false;
		Span<StartEnd> a = stackalloc StartEnd[3];
		if (!s_rxDetect.Match(s, a)) return false;
		r = (a[1], a[2]);
		return true;
	}
	static regexp s_rxDetect = new regexp(@"^\*(?:<.+?>\*)?([[:alpha:]]\w+)\.(\w\w+)");
	
	/// <summary>
	/// Our compiler (_CreateManagedResources) adds XAML of icons to resources from literal strings. This function gets the XAML.
	/// </summary>
	/// <param name="icon"></param>
	/// <returns>null if failed.</returns>
	public static string GetXamlFromResources(string icon) {
		//print.it("\r\n----", icon);
		if (DetectAndRemoveParametersForResources(icon, out string icon2) && ResourceUtil.TryGetString_(icon2) is string xaml) {
			//print.it(xaml);
			var a = ParseAll(icon);
			if (!XamlSetColorSizeMargin(ref xaml, a)) return null;
			//print.it(xaml);
			return xaml;
		} else {
			Debug_.Print(icon);
			return null;
		}
	}
	
	static regexp s_rxPath = new regexp(@"<Path (.+?)/?>");
	static regexp s_rxAttr = new(@" (?:Fill|Stroke|Width|Height|Margin|Stretch|SnapsToDevicePixels)=""[^""]*");
	
	/// <summary>
	/// In XAML sets color, size and margin specified in parsed icons.
	/// </summary>
	/// <param name="xaml">XAML with as many <i>Path</i> as <i>a</i> elements.</param>
	/// <param name="a">Parsed icons.</param>
	/// <returns>false if found errors etc.</returns>
	public static bool XamlSetColorSizeMargin(ref string xaml, IconString_[] a) {
		using var b_ = new StringBuilder_(out var b, xaml.Length / 10 * 11 + 100);
		int appendFrom = 0, i = 0;
		bool anyHasSizeOrMargin = a.Any(o => o.HasSize || o.HasMargin);
		
		foreach (var gp in s_rxPath.FindAllG(xaml, 1)) { //for each <Path ...>
			if (i == a.Length) return false;
			var x = a[i++];
			bool hasSize = x.HasSize, hasMargin = x.HasMargin, xamlHasSizeOrMargin = false;
			foreach (var ga in s_rxAttr.FindAllG(xaml, 0, gp.Start..gp.End)) { //for each attribute
				int attr = ga.Start + 1;
				if (xaml[attr] is 'F' or 'S') {
					string sVal;
					if (xaml[attr + 1] == 'n') { //SnapsToDevicePixels
						if (hasSize || x.snapPixels) sVal = "True"; else continue;
					} else if (xaml[attr + 3] == 'e') { //Stretch
						if (x.stretch is 'f' or 'm') sVal = x.stretch is 'f' ? "Fill" : "UniformToFill"; else continue;
					} else {
						sVal = NormalizeColor(x.color);
					}
					b.Append(xaml, appendFrom, xaml.IndexOf('"', attr) + 1 - appendFrom).Append(sVal);
					appendFrom = ga.End;
				} else {
					xamlHasSizeOrMargin = true;
					if (hasSize || hasMargin) {
						print.warning($"@size and %margin not supported for this icon: *{x.pack}.{x.name}", -1);
						return false;
					}
				}
			}
			if (hasSize || hasMargin || (anyHasSizeOrMargin && !xamlHasSizeOrMargin)) {
				b.Append(xaml, appendFrom, gp.End - appendFrom);
				appendFrom = gp.End;
				if (hasSize) {
					if (hasMargin) { print.warning($"Error in icon *{x.pack}.{x.name}: use @size or %margin, not both.", -1); return false; }
					b.AppendFormat(" Width=\"{0}\" Height=\"{0}\" Margin=\"{1}\"", x.size, ((16 - x.size) / 2.0).ToS());
				} else if (hasMargin) {
					double wid = 16 - x.margin.Left - x.margin.Right, hei = 16 - x.margin.Top - x.margin.Bottom;
					if (wid <= 0 || hei <= 0) { print.warning($"Error in icon *{x.pack}.{x.name}: margins left+right and top+bottom must be < 16.", -1); return false; }
					b.AppendFormat(" Width=\"{0}\" Height=\"{1}\" Margin=\"{2}\"", wid.ToS(), hei.ToS(), x.margin.ToString());
				} else {
					//if like "icon1;icon2", and some icons have specified size or margin, and some others don't, some icons may be rendered very small, depending on others.
					//	Don't know why. Workaround: specify Width/Height for all.
					b.Append(" Width=\"16\" Height=\"16\"");
				}
			}
		}
		b.Append(xaml, appendFrom, xaml.Length - appendFrom);
		xaml = b.ToString();
		return true;
	}
	
	/// <summary>
	/// If <i>s</i> is or contains <c>"color|color2"</c>, removes <c>"|color2"</c> or <c>"color|"</c> depending on <see cref="WpfUtil_.IsHighContrastDark"/>.
	/// If then (or initially) <i>s</i> is empty, returns <c>SystemColors.ControlTextColor</c>.
	/// </summary>
	public static string NormalizeColor(string s) {
		int i = s?.IndexOf('|') ?? -1;
		if (i >= 0) {
			bool dark = WpfUtil_.IsHighContrastDark;
			using (new StringBuilder_(out var b)) {
				int appendFrom = 0;
				do {
					int j = i + 1;
					if (dark) {
						while (i > 0 && s[i - 1] != ' ') i--;
					} else {
						while (j < s.Length && !(s[j] is ' ' or ';')) j++;
					}
					b.Append(s, appendFrom, i - appendFrom);
					i = s.IndexOf('|', j);
					appendFrom = j;
				} while (i > 0);
				b.Append(s, appendFrom, s.Length - appendFrom);
				s = b.ToString();
			}
		}
		if (s.NE()) s = SystemColors.ControlTextColor.ToString();
		return s;
	}
	
	/// <summary>
	/// If <i>s</i> is an icon string, removes parameters and returns true.
	/// <br/>Example1: <c>"*Pack.Name color"</c> -> <c>"*Pack.Name"</c>.
	/// <br/>Example2: <c><![CDATA["*<assembly>*Pack.Name1 color; *Pack.Name2 color margin;"]]></c> -> <c>"*Pack.Name1;Pack.Name2"</c>.
	/// </summary>
	public static bool DetectAndRemoveParametersForResources(RStr s, out string result) {
		StringBuilder b = null;
		while (Detect(s, out var d)) {
			if (b == null) b = new(); else b.Append(';');
			b.Append('*').Append(s[d.pack.Range]).Append('.').Append(s[d.name.Range]);
			int semi = s.IndexOf(d.name.end, ';'); if (semi < 0) break;
			s = s[++semi..].TrimStart();
		}
		return (result = b?.ToString()) != null;
	}
}
