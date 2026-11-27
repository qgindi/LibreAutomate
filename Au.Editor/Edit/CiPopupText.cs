using Au.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;

namespace LA;

class CiPopupText {
	public enum UsedBy { PopupList, Signature, Info }
	
	_KPopup _w;
	FlowDocumentControl _c;
	EventHandler _onHiddenOrDestroyed;
	UsedBy _usedBy;
	Section _section;
	bool _updateText;
	
	public CiPopupText(UsedBy usedy, EventHandler onHiddenOrDestroyed = null) {
		_usedBy = usedy;
		_onHiddenOrDestroyed = onHiddenOrDestroyed;
	}
	
	/// <summary>
	/// The top-level popup window.
	/// </summary>
	public KPopup PopupWindow => _CreateOrGet();
	
	/// <summary>
	/// Called when clicked a link with href prefix "^".
	/// </summary>
	public Action<CiPopupText, string> OnLinkClick { get; set; }
	
	/// <summary>
	/// Sets content text.
	/// </summary>
	/// <param name="value"></param>
	/// <param name="updateRect">
	/// If visible, update window rectangle for new content.
	/// Use <c>true</c> if you won't call <see cref="Show"/> afterward.
	/// </param>
	public void SetContent(Section value, bool updateRect = false) {
		if (value == _section) return;
		_section = value;
		if (!IsVisible) { _updateText = true; return; }
		_SetText();
		if (updateRect) _w.ResizeForNewContent();
	}
	
	KPopup _CreateOrGet() {
		if (_w == null) {
			bool ubInfo = _usedBy == UsedBy.Info;
			_w = new(ubInfo) {
				Name = "Ci.Info",
				WindowName = _usedBy switch { UsedBy.PopupList => "LA completion item info", UsedBy.Signature => "LA parameters info", _ => "LA quick info" },
				CloseHides = true
			};
			//_w.MaxSize = (_usedBy == UsedBy.Signature ? 800 : 600, _usedBy == UsedBy.PopupList ? 360 : 300); //no, use max available size
			
			_c = FlowDocumentControl.Create();
			_w.Content = _c;
			
			if (_onHiddenOrDestroyed != null) _w.Hidden += _onHiddenOrDestroyed;
			_c.LinkClicked += (sender, s) => {
				if (s.Starts('#')) return; //anchor
				if (s.Starts('^')) {
					OnLinkClick?.Invoke(this, s);
				} else if (s.Starts('|')) { //go to symbol source file/position or web page
					CiGoTo.LinkGoTo(s);
				} else if (HelpUtil.IsAuHelp_(s)) {
					HelpUtil.AuHelp(s);
				} else {
					run.itSafe(s);
				}
			};
			
			if (ubInfo) _w.ClickInactiveActivateOwner = true;
		}
		return _w;
	}
	
	/// <summary>
	/// Shows by anchorRect, owned by ownerControl.
	/// </summary>
	/// <param name="ownerControl"></param>
	/// <param name="anchorRect">Rectangle in screen coord. The popup window will be by it but not in it (in it if side = null).</param>
	/// <param name="hideIfOutside">Hide when mouse moved outside anchorRect unless is in the popup window.</param>
	public void Show(SciCode ownerControl, RECT anchorRect, Dock? side, bool hideIfOutside = false) {
		_CreateOrGet();
		if (_updateText) _SetText();
		_w.ShowByRect(ownerControl, side, anchorRect);
		
		if (hideIfOutside) {
			timer.every(100, t => {
				if (IsVisible) {
					var p = mouse.xy;
					if (anchorRect.Contains(p) || _w.Hwnd.Rect.Contains(p) || _c.IsMouseCaptureWithin) return;
					Hide();
				}
				t.Stop();
			});
		}
	}
	
	public void Show(SciCode ownerControl, int pos16, bool hideIfOutside = false, bool above = false) {
		var r = ownerControl.EGetCaretRectFromPos(pos16, inScreen: true);
		r.Inflate(50, 0);
		Show(ownerControl, r, above ? Dock.Top : Dock.Bottom, hideIfOutside);
	}
	
	public bool Hide() {
		if (!IsVisible) return false;
		_w.Close();
		_section = null;
		//_SetText(); //an unnecessary Measure
		_updateText = false;
		return true;
	}
	
	public bool IsVisible => _w?.IsVisible ?? false;
	
	void _SetText() {
		_updateText = false;
		_c?.Clear();
		if (_section != null) _c.Document.Blocks.Add(_section);
	}
	
	class _KPopup(bool usedByInfo) : KPopup(usedByInfo ? WS.BORDER : WS.THICKFRAME, shadow: usedByInfo, sizeToContent: SizeToContent.WidthAndHeight) {
		public bool autoWidth = true;
		
		protected override Size MeasureContent(Size availableSize) {
			var c = Content as FlowDocumentControl;
			var ds = base.MeasureContent(availableSize);
			
			//Find the paragraph containing the widest line.
			//Ignore non-text paragraphs; eg separators would make the calculated width = the available width.
			//Never mind: rectangles of offscreen paragraphs now are empty (until scrolled there?).
			var sv = c.Template.FindName("PART_ContentHost", c) as ScrollViewer;
			Rect widest = default;
			_Enum(sv, null);
			void _Enum(Visual parent, string parentName) {
				for (int n = VisualTreeHelper.GetChildrenCount(parent), i = 0; i < n; i++) {
					if (VisualTreeHelper.GetChild(parent, i) is not Visual v) continue;
					string name = v.GetType().Name;
					if (name == "LineVisual") {
						if (parentName == "ParagraphVisual") {
							var k = VisualTreeHelper.GetDescendantBounds(parent);
							if (k.Right > widest.Right) widest = k;
						} else Debug_.Print(parentName);
						continue;
					}
					_Enum(v, name);
				}
			}
			
			ds.Width = widest.Right
				+ Math.Max(widest.Left, 4); //some paddings. When testing, widest.Left was 3-4. I don't know how to get exact value.
			
			if (sv.ExtentHeight > ds.Height) { //will be scrollbar
				ds.Width += SystemParameters.VerticalScrollBarWidth;
			}
			
			ds.Width = Math.Min(ds.Width, availableSize.Width);
			
			//_Test(sv);
			
			return ds;
		}
		
		//void _Test(Visual root) {
		//	_Visuals(root, 0);
			
		//	void _Visuals(Visual visual, int depth) {
		//		for (int n = VisualTreeHelper.GetChildrenCount(visual), i = 0; i < n; i++) {
		//			if (VisualTreeHelper.GetChild(visual, i) is not Visual v) continue;
		//			var b = VisualTreeHelper.GetDescendantBounds(v);
		//			//print.it($"{new string(' ', depth * 2)}{v},  {b} (right={b.Right})");
		//			print.it($"{new string(' ', depth * 2)}{v},  {b.Left}  {b.Right}  (width={b.Width})");
		//			_Visuals(v, depth + 1);
		//		}
		//	}
		//}
	}
}

class FlowDocumentControl : FlowDocumentScrollViewer {
	public event Action<FlowDocumentControl, string> LinkClicked;
	
	//protected override Size MeasureOverride(Size constraint) {
	//	var p1 = perf.local();
	//	var r = base.MeasureOverride(constraint);
	//	p1.Next();
	//	print.it($"<><c blue>{constraint}, {r},  {p1.ToString()}");
	//	return r;
	//}
	
	internal static void OnLinkClicked_(Hyperlink h, RequestNavigateEventArgs e) {
		var s = e.Uri.OriginalString;
		//print.it(s);
		DependencyObject d = h;
		while (null != (d = LogicalTreeHelper.GetParent(d)))
			if (d is FlowDocumentControl c) {
				c.LinkClicked?.Invoke(c, s);
				break;
			}
	}
	
	/// <summary>
	/// Clears text (removes all blocks) and applies a workaround for a WPF bug.
	/// </summary>
	public void Clear() {
		Document.Blocks.Clear();
		_ = Selection?.IsEmpty; //workaround for WPF bug: if some text was selected, would select all new text
	}
	
	protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
		if (!Selection.IsEmpty) this.Hwnd().ActivateL(); //user may want to copy text with Ctrl+C
		base.OnPreviewMouseLeftButtonUp(e);
	}
	
	public static FlowDocumentControl Create(int colorRGB = 0xfffff0) {
		var ff = App.Wmain.FontFamily;
		double fs = App.Wmain.FontSize;
		if (ff.Source == "Segoe UI") { ff = new("Calibri"); fs += 2; }
		var d = new FlowDocument {
			FontFamily = ff,
			FontSize = fs,
			Background = ColorInt.WpfBrush_(colorRGB),
			PagePadding = default,
			TextAlignment = TextAlignment.Left,
		};
		TextOptions.SetTextFormattingMode(d, TextFormattingMode.Display);
		var c = new FlowDocumentControl {
			Document = d,
			FocusVisualStyle = null,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			IsInactiveSelectionHighlightEnabled = true,
		};
		return c;
	}
}
