using Au.Controls;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;

class CiPopupList {
	KPopup _popup;
	DockPanel _panel;
	KTreeView _tv;
	StackPanel _tb;
	
	SciCode _doc;
	CiCompletion _compl;
	List<CiComplItem> _a;
	List<CiComplItem> _av;
	KCheckBox[] _kindButtons;
	KCheckBox _groupButton;
	Button _unimportedButton;
	bool _groupsEnabled, _winApi;
	List<string> _groups;
	CiPopupText _textPopup;
	timer _tpTimer;
	
	public KPopup PopupWindow => _popup;
	
	///// <summary>
	///// The child list control.
	///// </summary>
	//public KTreeView Control => _tv;
	
	public CiPopupList(CiCompletion compl) {
		_compl = compl;
		
		_tv = new KTreeView {
			ItemMarginLeft = 20,
			//HotTrack = true, //no
			CustomDraw = new _CustomDraw(this),
			//test = true
		};
		_tv.ItemActivated += _tv_ItemActivated;
		_tv.SelectedSingle += _tv_SelectedSingle;
		
		_tb = new StackPanel { Background = SystemColors.ControlBrush };
		
		var cstyle = Application.Current.FindResource(ToolBar.CheckBoxStyleKey) as Style;
		var bstyle = Application.Current.FindResource(ToolBar.ButtonStyleKey) as Style;
		
		var kindNames = CiUtil.ItemKindNames;
		_kindButtons = new KCheckBox[kindNames.Length];
		for (int i = 0; i < kindNames.Length; i++) {
			_AddButton(_kindButtons[i] = new(), kindNames[i], CiComplItem.ImageResource((CiItemKind)i), _KindButton_Click);
		}
		
		_tb.Children.Add(new Separator());
		
		_AddButton(_groupButton = new() { IsChecked = App.Settings.ci_complGroup }, "Group by namespace or inheritance", "resources/ci/groupby.xaml", _GroupButton_Click);
		_AddButton(_unimportedButton = new(), "Show more types or extension methods (Ctrl+Space)", "resources/ci/expandscope.xaml", _UnimportedButton_Click);
		
		//var options = new Button();
		//options.Click += _Options_Click;
		//_AddButton(options, "Options", "resources/images/settingsgroup_16x.xaml");
		
		void _AddButton(ButtonBase b, string text, string image, RoutedEventHandler click) {
			b.Style = (b is CheckBox) ? cstyle : bstyle;
			b.Content = ResourceUtil.GetWpfImageElement(image);
			b.ToolTip = text;
			AutomationProperties.SetName(b, text);
			b.Focusable = false; //would close popup
			if (b is KCheckBox c) c.CheckChanged += click; else b.Click += click;
			_tb.Children.Add(b);
		}
		
		_panel = new DockPanel { Background = SystemColors.WindowBrush };
		DockPanel.SetDock(_tb, Dock.Left);
		_panel.Children.Add(_tb);
		_panel.Children.Add(_tv);
		_popup = new KPopup {
			Size = (300, 360),
			Content = _panel,
			CloseHides = true,
			Name = "Ci.Completion",
			WindowName = "LA completion list",
		};
		
		_textPopup = new CiPopupText(CiPopupText.UsedBy.PopupList);
		_tpTimer = new timer(_ShowTextPopup);
	}
	
	private void _KindButton_Click(object sender, RoutedEventArgs e) {
		int kindsChecked = 0, kindsVisible = 0;
		for (int i = 0; i < _kindButtons.Length; i++) {
			var v = _kindButtons[i];
			if (v.IsVisible) {
				kindsVisible |= 1 << i;
				if (v.IsChecked == true) kindsChecked |= 1 << i;
			}
		}
		if (kindsChecked == 0) kindsChecked = kindsVisible;
		foreach (var v in _a) {
			if (0 != (kindsChecked & (1 << (int)v.kind))) v.hidden &= ~CiComplItemHiddenBy.Kind; else v.hidden |= CiComplItemHiddenBy.Kind;
		}
		UpdateVisibleItems();
	}
	
	private void _GroupButton_Click(object sender, RoutedEventArgs e) {
		_groupsEnabled = (sender as KCheckBox).IsChecked && _groups != null;
		_SortAndSetControlItems();
		this.SelectedItem = null;
		_tv.Redraw(true);
		App.Settings.ci_complGroup = _groupButton.IsChecked == true;
	}
	
	private void _UnimportedButton_Click(object sender, RoutedEventArgs e) {
		App.Dispatcher.InvokeAsync(() => _compl.ShowList());
	}
	
	//private void _Options_Click(object sender, RoutedEventArgs e) {
	//	var m = new popupMenu();
	//	//m[""] = o => ;
	//}
	
	private void _tv_ItemActivated(TVItemEventArgs e) {
		_compl.Commit(_doc, _av[e.Index]);
		Hide();
	}
	
	private void _tv_SelectedSingle(object sender, int index) {
		if ((uint)index < _av.Count) {
			var ci = _av[index];
			//print.it(ci.ci.ProviderName, ci.Provider);
			if (ci.Provider == CiComplProvider.XmlDoc) return;
			_tpTimer.Tag = ci;
			_tpTimer.After(300);
			_textPopup.Text = null;
		} else {
			_textPopup.Hide();
			_tpTimer.Stop();
		}
	}
	
	void _ShowTextPopup(timer t) {
		var ci = t.Tag as CiComplItem;
		var text = _compl.GetDescriptionDoc(ci, 0);
		if (text == null) return;
		_textPopup.Text = text;
		_textPopup.OnLinkClick = (ph, e) => {
			if (e.ToInt(1) is int i && i != 0) {
				ph.Text = _compl.GetDescriptionDoc(ci, e.ToInt(1));
			} else if (e.Starts("^snippet ")) {
				DSnippets.ShowSingle(e[9..]);
			}
		};
		_textPopup.Show(Panels.Editor.ActiveDoc, _popup.Hwnd.Rect, Dock.Right);
	}
	
	void _SortAndSetControlItems() {
		if (_winApi) {
			if (!_groupsEnabled) { //the database table is already grouped/sorted
				_av.Sort((c1, c2) => string.Compare(c1.Text, c2.Text, StringComparison.OrdinalIgnoreCase));
			}
		} else {
			_av.Sort((c1, c2) => {
				int diff = c1.moveDown - c2.moveDown;
				if (diff != 0) return diff;
				
				if (_groupsEnabled) {
					diff = c1.group - c2.group;
					if (diff != 0) return diff;
					
					if (_groups[c1.group].NE()) {
						CiItemKind k1 = c1.kind, k2 = c2.kind;
						
						//group Enum and Enum.Member together
						if (k1 == CiItemKind.EnumMember) k1 = CiItemKind.Enum;
						if (k2 == CiItemKind.EnumMember) k2 = CiItemKind.Enum;
						
						diff = k1 - k2;
						if (diff != 0) return diff;
					}
				}
				
				int r = CiUtil.SortComparer.Compare(c1.GetCI().SortText, c2.GetCI().SortText);
				if (r == 0) {
					r = CiSnippets.Compare(c1, c2); //custom snippet first
				}
				return r;
			});
		}
		
		CiComplItem prev = null;
		foreach (var v in _av) {
			var group = _groupsEnabled && (prev == null || v.group != prev.group) ? _groups[v.group] : null;
			v.SetDisplayText(group);
			prev = v;
		}
		
		_tv.SetItems(_av);
	}
	
	public void UpdateVisibleItems() {
		int n1 = 0; foreach (var v in _a) if (v.hidden == 0) n1++;
		_av = new(n1);
		foreach (var v in _a) if (v.hidden == 0) _av.Add(v);
		_SortAndSetControlItems();
		
		//Occasionally app used to crash without an error UI when typing a word and should show completions.
		//	Windows event log shows exception with call stack, which shows that _av.Select called with _av=null.
		//	The reason (reproduced):
		//		in _SortAndSetControlItems -> _tv.SetItems -> ... -> _Measure, probably when setting scrollbar properties,
		//		WPF raises an UIA event and waits + dispatches messages. During that time is called Hide(). It sets _av=null.
		//	Workaround: return now if _aw null. Workaround 2: replace the WPF scrollbar with native scrollbar. Both done.
		if (_av == null) return;
		
		_compl.SelectBestMatch(_av, _groupsEnabled); //pass items sorted like in the visible list
		
		//Still _av and _a null sometimes (see above comments). It means, Roslyn code called by SelectBestMatch pumps messages.
		//	It started when started using the Windows "Text cursor indicator" feature. It aggressively sends many WM_GETOBJECT.
		if (_a == null) return;
		
		int kinds = 0;
		foreach (var v in _a) if ((v.hidden & ~CiComplItemHiddenBy.Kind) == 0) kinds |= 1 << (int)v.kind;
		for (int i = 0; i < _kindButtons.Length; i++) _kindButtons[i].Visibility = 0 != (kinds & (1 << i)) ? Visibility.Visible : Visibility.Collapsed;
	}
	
	public void Show(SciCode doc, int position, List<CiComplItem> a, List<string> groups, bool winApi) {
		if (a.NE_()) {
			Hide();
			return;
		}
		
		_a = a;
		_groups = groups;
		_groupsEnabled = _groups != null && _groupButton.IsChecked == true;
		_doc = doc;
		_winApi = winApi;
		
		foreach (var v in _kindButtons) v.IsChecked = false;
		_groupButton.Visibility = _groups != null ? Visibility.Visible : Visibility.Collapsed;
		_unimportedButton.Visibility = _groups != null ? Visibility.Visible : Visibility.Collapsed;
		UpdateVisibleItems();
		
		var r = _doc.EGetCaretRectFromPos(position, inScreen: true);
		r.left -= Dpi.Scale(50, _doc);
		
		_popup.ShowByRect(_doc, Dock.Bottom, r);
	}
	
	public void Hide() {
		//Debug_.PrintIf(_debug, "reenter, " + new StackTrace());
		if (_a == null) return;
		_tv.SetItems(null);
		_popup.Close();
		_textPopup.Hide();
		_tpTimer.Stop();
		_a = null;
		_av = null;
		_groups = null;
		_suggestedItem = null;
		
		if (_winApi) Task.Run(() => GC.Collect());
	}
	
	public CiComplItem SelectedItem {
		get {
			int i = _tv.SelectedIndex;
			return i >= 0 ? _av[i] : null;
		}
		set {
			int i = value == null ? -1 : _av.IndexOf(value);
			if (_tv.SelectedIndex == i) return;
			if (i >= 0) _tv.SelectSingle(i, andFocus: true, scrollTop: true); else _tv.UnselectAll();
		}
	}
	
	public CiComplItem SuggestedItem {
		get => _suggestedItem;
		set {
			_tv.UnselectAll();
			if (value != _suggestedItem) {
				var old = _suggestedItem;
				_suggestedItem = value;
				if (value != null) {
					_tv.EnsureVisible(value, scrollTop: true);
					_tv.Redraw(value);
				}
				if (old != null) _tv.Redraw(old);
			}
		}
	}
	CiComplItem _suggestedItem;
	
	public bool OnCmdKey(KKey key) {
		if (_popup.IsVisible) {
			switch (key) {
			case KKey.Escape:
				Hide();
				return true;
			case KKey.Down:
			case KKey.Up:
			case KKey.PageDown:
			case KKey.PageUp:
			case KKey.Home:
			case KKey.End:
				_tv.ProcessKey(keys.more.KKeyToWpf(key));
				return true;
			}
		}
		return false;
	}
	
	class _CustomDraw : ITVCustomDraw {
		CiPopupList _p;
		TVDrawInfo _cd;
		GdiTextRenderer _tr;
		int _textColor;
		
		public _CustomDraw(CiPopupList list) {
			_p = list;
		}
		
		public void Begin(TVDrawInfo cd, GdiTextRenderer tr) {
			_cd = cd;
			_tr = tr;
			_textColor = Api.GetSysColor(Api.COLOR_WINDOWTEXT);
		}
		
		//public bool DrawBackground() {
		//}
		
		//public bool DrawCheckbox() {
		//}
		
		//public bool DrawImage(System.Drawing.Bitmap image) {
		//}
		
		public bool DrawText() {
			var ci = _cd.item as CiComplItem;
			
			if (ci == _p.SuggestedItem) {
				_cd.graphics.DrawRectangleInset(System.Drawing.Pens.DodgerBlue, _cd.rect);
			}
			
			var s = _cd.item.DisplayText;
			Range black, green;
			if (ci.commentOffset == 0) { black = ..s.Length; green = default; } else { black = ..ci.commentOffset; green = ci.commentOffset..; }
			
			int xEndOfText = 0;
			int color = ci.moveDown.HasAny(CiComplItemMoveDownBy.Name | CiComplItemMoveDownBy.FilterText) ? 0x808080 : _textColor;
			_tr.MoveTo(_cd.xText, _cd.yText);
			if (ci.hilite != 0) {
				ulong h = ci.hilite;
				for (int normalFrom = 0, boldFrom, boldTo, to = black.End.Value; normalFrom < to; normalFrom = boldTo) {
					for (boldFrom = normalFrom; boldFrom < to && 0 == (h & 1); boldFrom++) h >>= 1;
					_tr.DrawText(s, color, normalFrom..boldFrom);
					if (boldFrom == to) break;
					for (boldTo = boldFrom; boldTo < to && 0 != (h & 1); boldTo++) h >>= 1;
					_tr.FontBold(); _tr.DrawText(s, color, boldFrom..boldTo); _tr.FontNormal();
				}
			} else {
				_tr.DrawText(s, color, black);
			}
			
			if (ci.moveDown.Has(CiComplItemMoveDownBy.Obsolete)) xEndOfText = _tr.GetCurrentPosition().x;
			
			if (ci.commentOffset > 0) _tr.DrawText(s, 0x00A040, green);
			
			//draw red line over obsolete items
			if (ci.moveDown.Has(CiComplItemMoveDownBy.Obsolete)) {
				int vCenter = _cd.rect.top + _cd.rect.Height / 2;
				_cd.graphics.DrawLine(System.Drawing.Pens.OrangeRed, _cd.xText, vCenter, xEndOfText, vCenter);
			}
			
			return true;
		}
		
		public void DrawMarginLeft() {
			var ci = _cd.item as CiComplItem;
			var g = _cd.graphics;
			var r = _cd.rect;
			
			//draw images: access, static/abstract
			var cxy = _cd.imageRect.Width;
			var ri = new System.Drawing.Rectangle(_cd.imageRect.left - cxy, _cd.imageRect.top + cxy / 4, cxy, cxy);
			if (ci.AccessImageSource is string s1) g.DrawImage(IconImageCache.Common.Get(s1, _cd.dpi, isImage: true), ri);
			if (ci.ModifierImageSource is string s2) g.DrawImage(IconImageCache.Common.Get(s2, _cd.dpi, isImage: true), ri);
			
			//draw group separator
			if (_cd.index > 0) {
				var cip = _p._av[_cd.index - 1];
				if (cip.moveDown != ci.moveDown || (_p._groupsEnabled && cip.group != ci.group)) {
					_cd.graphics.DrawLine(System.Drawing.Pens.YellowGreen, 0, r.top + 1, r.right, r.top + 1);
				}
			}
		}
	}
}
