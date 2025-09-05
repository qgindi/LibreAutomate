using System.Drawing;

namespace Au;

public partial class dialog {
	
	public dialog Buttons(Strings buttons, bool asCommandLinks = false, Strings customButtons = default) {
		SetButtons(buttons, asCommandLinks, customButtons);
		return this;
	}
	
	public static Builder build(string header = null, string text = null) => new(header, text);
	
	public class Builder {
		dialog _d;
		
		public Builder(string header = null, string text = null) {
			_d = new(header, text);
		}
		
		public Builder(dialog d) {
			_d = d;
		}
		
		public dialog Dialog => _d;
		
		public Builder Text(string header = null, string text = null) {
			_d.SetText(header, text);
			return this;
		}
		
		public Builder Buttons(Strings buttons, bool asCommandLinks = false, Strings customButtons = default) {
			_d.SetButtons(buttons, asCommandLinks, customButtons);
			return this;
		}
		
		public Builder Icon(DIcon icon) {
			_d.SetIcon(icon);
			return this;
		}
		
		public Builder Icon(object icon) {
			_d.SetIcon(icon);
			return this;
		}
		
		public Builder Checkbox(string text, bool check = false) {
			_d.SetCheckbox(text, check);
			return this;
		}
		
		public Builder XY(Coord x = default, Coord y = default, bool rawXY = false) {
			_d.SetXY(x, y, rawXY);
			return this;
		}
		
		public int Show() {
			return _d.ShowDialog();
		}
		
		public bool ShowYesNo() {
			return 0 != _d.ShowDialog();
		}
		
		public Builder TextWithLinks(InterpolatedString text) {
			_d.SetText(null, text.GetFormattedText(_d));
			return this;
		}
		
		public Builder FooterWithLinks(InterpolatedString text) {
			_d.SetFooter(text.GetFormattedText(_d));
			return this;
		}
		
		public Builder ExpandedTextWithLinks(InterpolatedString text, bool showInFooter = false) {
			_d.SetExpandedText(text.GetFormattedText(_d), showInFooter);
			return this;
		}
		
	}
	
#pragma warning disable 1591 //no XML doc
	[InterpolatedStringHandler, NoDoc]
	public ref struct InterpolatedString {
		DefaultInterpolatedStringHandler _f;
		Dictionary<string, Delegate> _links;
		
		public InterpolatedString(int literalLength, int formattedCount) {
			_f = new(literalLength, formattedCount);
		}
		
		public InterpolatedString(int literalLength, int formattedCount, IFormatProvider provider) {
			_f = new(literalLength, formattedCount, provider);
		}
		
		public InterpolatedString(int literalLength, int formattedCount, IFormatProvider provider, Span<char> initialBuffer) {
			_f = new(literalLength, formattedCount, provider, initialBuffer);
		}
		
		public void AppendLiteral(string value)
			 => _f.AppendLiteral(value);
		
		public void AppendFormatted(string value)
			=> _f.AppendFormatted(value);
		
		public void AppendFormatted(string value, int alignment = 0, string format = null)
			=> _f.AppendFormatted(value, alignment, format);
		
		public void AppendFormatted(object value, int alignment = 0, string format = null)
			=> _f.AppendFormatted(value, alignment, format);
		
		public void AppendFormatted(scoped RStr value)
			=> _f.AppendFormatted(value);
		
		public void AppendFormatted(scoped RStr value, int alignment = 0, string format = null)
			=> _f.AppendFormatted(value, alignment, format);
		
		public void AppendFormatted<T>(T value) {
			if (value is Delegate del) {
				var guid = Guid.NewGuid().ToString("N");
				(_links ??= []).Add(guid, del);
				_f.AppendLiteral("href=\"\a\e");
				_f.AppendLiteral(guid);
				_f.AppendLiteral("\"");
			} else _f.AppendFormatted(value);
		}
		
		public void AppendFormatted<T>(T value, int alignment)
			 => _f.AppendFormatted(value, alignment);
		
		public void AppendFormatted<T>(T value, string format)
			=> _f.AppendFormatted(value, format);
		
		public void AppendFormatted<T>(T value, int alignment, string format)
			=> _f.AppendFormatted(value, alignment, format);
		
		public string GetFormattedText(dialog d) {
			if (_links is { } links) {
				d.HyperlinkClicked += e => {
					var s = e.LinkHref;
					if (s.Length == 34 && s is ['\a', '\e', ..] && links.TryGetValue(s[2..], out var del)) {
						if (del is Action k) k();
						else if (del is Action<DEventArgs> g) g(e);
					}
				};
			}
			return _f.ToStringAndClear();
		}
	}
#pragma warning restore 1591
}
