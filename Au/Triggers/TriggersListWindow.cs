using Au.Triggers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace Au.Triggers;

public partial class ActionTriggers {
	/// <summary>
	/// Shows a temporary window with a list of currently active triggers for the active window (hotkey, autotext and mouse edge/move triggers) or the mouse window (mouse click/wheel triggers).
	/// </summary>
	/// <param name="triggerType">Which trigger type to select initially: 0 hotkey, 1 autotext, 2 mouse, 3 window, -1 previous.</param>
	/// <remarks>
	/// To determine which triggers are active in the target window, the function uses window scopes specified in code (like <c>Triggers.Of.Window("Window name")</c>). However it ignores code like <c>Triggers.FuncOf...</c>, therefore the list includes triggers deactivated by your <c>FuncOf</c> functions.
	/// </remarks>
	/// <example>
	/// Code in file <c>Hotkey triggers.cs</c>. Calls this function when pressed hotkey <c>Alt+?</c>.
	/// <code><![CDATA[
	/// hk["Alt+?"] = o => Triggers.ShowTriggersListWindow();
	/// ]]></code>
	/// </example>
	public void ShowTriggersListWindow(int triggerType = -1) {
		run.thread(() => TriggersListWindow.Show_(this, triggerType));
		
		//triggers.SendMsg_(false, () => TriggersListWindow.Show_(this, triggerType)); //no. The triggers thread uses a special message loop etc. Possible various anomalies.
	}
	
	internal bool triggersListWindowIsActive_;
}

class TriggersListWindow : Window {
	static TriggersListWindow s_w;
	
	internal static void Show_(ActionTriggers triggers, int triggerType) {
		//single instance
		if (s_w != null) {
			s_w.Dispatcher.InvokeAsync(() => {
				s_w.Hwnd().ActivateL(true);
				if (triggerType is >= 0 and <= 3) {
					s_w._arb[triggerType].IsChecked = true;
				}
			});
			return;
		}
		
		s_w = new(triggers, triggerType);
		s_w.ShowDialog();
		s_w.Dispatcher.InvokeShutdown();
		s_w = null;
	}
	
	ActionTriggers _triggers;
	wnd _wActive, _wMouse;
	POINT _pMouse;
	RadioButton[] _arb;
	ListView _lv;
	ListCollectionView _view;
	
	TriggersListWindow(ActionTriggers triggers, int triggerType) {
		_triggers = triggers;
		if (triggerType is < 0 or > 3) triggerType = Math.Clamp(s_sett.triggerType, 0, 3);
		
		_pMouse = mouse.xy;
		_wMouse = wnd.fromXY(_pMouse, WXYFlags.NeedWindow);
		_wActive = wnd.active;
		
		var a = _GetTriggers();
		
		Title = "Triggers";
		var b = new wpfBuilder(this).WinSize(500, 700);
		b.R.AddToolBar_(out _, out var tb, hideOverflow: true, controlBrush: true).Margin("0");
		b.Add(out TextBox tFilter).Margin("L0").Focus();
		tFilter.PreviewMouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) tFilter.Clear(); };
		
		b.Row(-1).Add(out _lv);
		_lv.SelectionMode = SelectionMode.Single;
		VirtualizingStackPanel.SetVirtualizationMode(_lv, VirtualizationMode.Recycling);
		ScrollViewer.SetHorizontalScrollBarVisibility(_lv, ScrollBarVisibility.Disabled);
		_SetItemTemplate();
		_lv.ItemsSource = a;
		
		_lv.MouseUp += (_, e) => {
			if (e.ChangedButton is MouseButton.Left or MouseButton.Right) {
				if (s_sett.runOn2Click && e.ChangedButton is MouseButton.Left) return;
				if (_lv.ContainerFromElement(e.OriginalSource as DependencyObject) is ListViewItem { Content: _TLItem t } lvi) _Menu(t, lvi, 1);
			}
		};
		_lv.MouseDoubleClick += (_, e) => {
			if (s_sett.runOn2Click && e.ChangedButton is MouseButton.Left) {
				if (_lv.ContainerFromElement(e.OriginalSource as DependencyObject) is ListViewItem { Content: _TLItem t } lvi) _Menu(t, lvi, 2);
			}
		};
		
		_lv.GotKeyboardFocus += (_, _) => {
			Dispatcher.InvokeAsync(() => {
				if (_lv.Items.Count > 0) {
					int i = _lv.SelectedIndex;
					if (i < 0) _lv.SelectedIndex = i = 0;
					_lv.ScrollIntoView(_lv.Items.GetItemAt(i));
					if (_lv.ItemContainerGenerator.ContainerFromIndex(i) is ListViewItem lvi) lvi.Focus();
				}
			});
		};
		
		b.End();
		
		string filter = null;
		_view = (ListCollectionView)CollectionViewSource.GetDefaultView(a);
		_view.Filter = o => {
			if (o is _TLItem t) {
				if (_TriggerTypeToInt(t) != triggerType) return false;
				if (!filter.NE()) {
					if (!(t.trigger.Contains(filter, StringComparison.OrdinalIgnoreCase) || (t.action?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))) return false;
				}
			}
			return true;
		};
		if (s_sett.sort) _view.CustomSort = new _SortComparer();
		
		string[] aTriggersTypeStrings = { "_Hotkey", "_Autotext", "_Mouse", "_Window" };
		_arb = new RadioButton[aTriggersTypeStrings.Length];
		for (int i = 0; i < _arb.Length; i++) {
			tb.Items.Add(_arb[i] = new RadioButton { Content = new AccessText { Text = aTriggersTypeStrings[i] }, Width = 60, Margin = new(0, 0, 3, 0), BorderBrush = SystemColors.ActiveBorderBrush });
			int tt = i;
			_arb[i].Checked += (_, e) => { s_sett.triggerType = triggerType = tt; _view.Refresh(); };
			_arb[i].Focusable = false; //to avoid focusing on access key
		}
		_arb[triggerType].IsChecked = true;
		tFilter.TextChanged += (_, _) => { filter = tFilter.Text; _view.Refresh(); };
		
		SourceInitialized += (_, _) => {
			var w = this.Hwnd();
			w.MoveToScreenCenter(); //workaround for WPF bug: incorrectly centers in a non-primary screen with different DPI
		};
		Loaded += (_, _) => {
			var w = this.Hwnd();
			w.ActivateL();
		};
		Activated += (_, _) => {
			_triggers.triggersListWindowIsActive_ = true;
		};
		Deactivated += (_, _) => {
			_triggers.triggersListWindowIsActive_ = false;
			if (s_sett.autoClose && IsVisible) timer.after(100, _ => {
				if (!wnd.active.IsOfThisThread) Close();
			});
		};
	}
	
	protected override void OnPreviewKeyDown(KeyEventArgs e) {
		switch (e.Key) {
		case Key.Escape: Close(); e.Handled = true; return;
		case Key.Down when Keyboard.FocusedElement is TextBox: _lv.Focus(); e.Handled = true; return;
		case Key.Enter:
			if (e.OriginalSource is ListViewItem { Content: _TLItem t } lvi) {
				_Menu(t, lvi, 0);
			} else {
				_lv.Focus();
				timer.after(1, _ => {
					if (_lv.SelectedItem is _TLItem t && _lv.ItemContainerGenerator.ContainerFromItem(t) is ListViewItem lvi) _Menu(t, lvi, 0);
				});
			}
			e.Handled = true;
			return;
		}
		base.OnPreviewKeyDown(e);
	}
	
	void _Menu(_TLItem t, ListViewItem li, int clickCount) {
		if ((s_sett.runOnEnter && clickCount == 0) || (s_sett.runOn2Click && clickCount == 2)) {
			_RunAction(t);
			return;
		}
		
		var m = new popupMenu { CheckDontClose = true };
		
		m["&Run"] = o => _RunAction(t);
		if (clickCount == 0) m.FocusedItem = m.Last;
		
		m["&Edit"] = o => {
			if (s_sett.autoClose) Close();
			ScriptEditor.Open(t.t.SourceFile, t.t.SourceLine);
		};
		m.Separator();
		m.Submenu("&Settings", m => {
			m.AddCheck("Auto-close this window", s_sett.autoClose, o => { s_sett.autoClose = o.IsChecked; });
			m.Separator();
			m.AddCheck("Run on 2*click", s_sett.runOn2Click, o => { s_sett.runOn2Click = o.IsChecked; });
			m.AddCheck("Run on single Enter", s_sett.runOnEnter, o => { s_sett.runOnEnter = o.IsChecked; });
			m.Separator();
			m.AddCheck("Compact list", s_sett.compact, o => { s_sett.compact = o.IsChecked; _SetItemTemplate(); });
			m.AddCheck("Sort", s_sett.sort, o => { s_sett.sort = o.IsChecked; _view.CustomSort = s_sett.sort ? new _SortComparer() : null; });
			m.AddCheck("Sort hotkeys by modifier", s_sett.sortByMod, o => { s_sett.sortByMod = o.IsChecked; if (s_sett.sort) _view.CustomSort = new _SortComparer(); });
		});
		m["About"] = o => _About();
		
		var r = li.RectInScreen();
		POINT p = clickCount==0 ? new(r.left, r.bottom) : mouse.xy;
		m.Show(PMFlags.Underline | PMFlags.AlignRectBottomTop | (clickCount==0 ? 0 : PMFlags.AlignCenterH), xy: p, excludeRect: r, owner: this);
	}
	
	void _RunAction(_TLItem t) {
		if (s_sett.autoClose) Close();
		wnd ww = default;
		if (t.t is WindowTrigger tw) {
			ww = tw.Finder.Find();
			if (ww.Is0) { dialog.showInfo("Cannot run trigger action", $"Window not found.\n\n{tw.Finder}", flags: DFlags.CenterMouse); return; }
			if (tw.Event is TWEvent.Active or TWEvent.ActiveNew or TWEvent.ActiveOnce || !ww.HasExStyle(WSE.NOACTIVATE)) ww.ActivateL(true);
		} else if (t.t is MouseTrigger { Kind: TMKind.Click or TMKind.Wheel } mt) {
			if (_wMouse == _wActive || wnd.fromXY(_pMouse, WXYFlags.NeedWindow) != _wMouse) _wMouse.ActivateL(true);
		} else {
			_wActive.ActivateL(true);
		}
		if (!s_sett.autoClose && this.Hwnd().IsActive) this.Hwnd().ShowMinimized(1);
		
		timer2.after(200, _ => _RunNow(t.t, ww));
		
		void _RunNow(ActionTrigger t, wnd ww) {
			wnd w, w2; string s2 = null;
			if (t is WindowTrigger tw) {
				w = w2 = ww;
				if (!w.IsVisible) return;
				if (tw.Event is TWEvent.Active or TWEvent.ActiveNew or TWEvent.ActiveOnce && !w.IsActive) (w2, s2) = (default, "active");
			} else {
				if (t is MouseTrigger { Kind: TMKind.Click or TMKind.Wheel } mt) {
					try { mouse.move(_pMouse); 100.ms(); } catch { return; }
					(w, w2, s2) = (_wMouse, wnd.fromXY(_pMouse, WXYFlags.NeedWindow), "mouse");
				} else {
					(w, w2, s2) = (_wActive, wnd.active, "active");
				}
			}
			if (w2 != w) { dialog.showInfo("Cannot run trigger action", $"The {s2} window must be:\n\n{w}", flags: DFlags.CenterMouse); return; }
			TriggerArgs ta = t switch {
				HotkeyTrigger k => new HotkeyTriggerArgs(k, w, 0, 0),
				AutotextTrigger k => new AutotextTriggerArgs(k, w, "", false),
				MouseTrigger k => new MouseTriggerArgs(k, w, 0),
				WindowTrigger k => new WindowTriggerArgs(k, w, 0),
				_ => null
			};
			t.RunAction(ta);
		}
	}
	
	void _SetItemTemplate() {
		_lv.ItemTemplate = (DataTemplate)XamlReader.Parse($$$"""
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
	<TextBlock TextWrapping="Wrap" Padding="0,1,0,2">
		<TextBlock.Resources>
			<Style TargetType="Run">
				<Style.Triggers>
					<DataTrigger Binding="{Binding t.Disabled}" Value="True">
						<Setter Property="Foreground" Value="Gray"/>
					</DataTrigger>
					<DataTrigger Binding="{Binding t.Disabled}" Value="False">
						<Setter Property="Foreground" Value="{{{(WpfUtil_.IsHighContrastDark ? "#FFFF2D" : "#0060F0")}}}"/>
					</DataTrigger>
				</Style.Triggers>
			</Style>
		</TextBlock.Resources>
		<Bold><Run Text="{Binding trigger, Mode=OneWay}"/></Bold><Run Text="{Binding options, Mode=OneWay}" Foreground="YellowGreen"/><Run Text="{{{(s_sett.compact ? "" : "&#x0a;")}}}        " Foreground="Black"/><Run Text="{Binding action, Mode=OneWay}" Foreground="{DynamicResource {x:Static SystemColors.WindowTextBrushKey}}"/>
	</TextBlock>
</DataTemplate>
""");
	}
	
	void _About() {
		var s = $"""
The list contains triggers that work in the active window (hotkey, autotext and mouse edge/move triggers) or the mouse window (mouse click/wheel triggers).

Active: {_WndToStr(_wActive)}
Mouse: {_WndToStr(_wMouse)}

Code like `Triggers.Of...` is applied. Code like `Triggers.FuncOf...` is ignored.
""";
		
		dialog.showInfo("About Triggers", s, owner: this);
		
		static string _WndToStr(wnd w) {
			var name = w.Name;
			return name.NE() ? $"cn={w.ClassName}, program={w.ProgramName}" : $"name={name}, program={w.ProgramName}";
		}
	}
	
	ObservableCollection<_TLItem> _GetTriggers() {
		List<_TLItem> a = new();
		List<(string path, string text, int[] lines)> aFiles = new();
		Dictionary<TriggerScope, bool> dScopes = new();
		WFCache wfCache = new() { CacheName = true, NoTimeout = true, IgnoreVisibility = true };
		
		foreach (var t in _triggers.Hotkey) {
			if (!_InScope(t, _wActive)) continue;
			string sTrigger = t.ParamsString, sOptions = null;
			if (t.Flags != 0) {
				sTrigger = sTrigger[..sTrigger.Find(" (")];
				var f1 = t.Flags & (TKFlags.LeftMod | TKFlags.RightMod | TKFlags.Numpad | TKFlags.NumpadNot); //display only flags that are important here
				if (f1 != 0) sOptions = $"    {f1}";
			}
			_Add(t, sTrigger, sOptions);
		}
		
		string postfixKey = _triggers.Autotext.PostfixKey.ToString();
		(string s, TAPostfix pt, string pc) atPrev = default; //optimization: don't build identical options
		foreach (var t in _triggers.Autotext) {
			if (!_InScope(t, _wActive)) continue;
			string sOptions = null;
			if (t.PostfixType == atPrev.pt && t.PostfixChars == atPrev.pc) {
				sOptions = atPrev.s;
			} else {
				if (t.PostfixType != TAPostfix.None) {
					using (new StringBuilder_(out var b)) {
						b.Append("    + ");
						switch (t.PostfixType) {
						case TAPostfix.Key: b.Append(postfixKey); break;
						case TAPostfix.Char: b.Append(t.PostfixChars ?? "char"); break;
						default: b.Append(postfixKey).Append(" or ").Append(t.PostfixChars ?? "char"); break;
						}
						sOptions = b.ToString();
					}
				}
				atPrev = (sOptions, t.PostfixType, t.PostfixChars);
			}
			_Add(t, t.Text, sOptions);
		}
		
		foreach (var t in _triggers.Mouse) {
			if (!_InScope(t, t.Kind is TMKind.Click or TMKind.Wheel ? _wMouse : _wActive)) continue;
			string s = t.ParamsString, sOptions = null;
			if (t.Flags != 0) {
				s = s.RxReplace(@" \(.+?\)", "", 1);
				var f1 = t.Flags & (TMFlags.LeftMod | TMFlags.RightMod); //display only flags that are important here
				if (f1 != 0) sOptions = $"    {f1}";
			}
			_Add(t, s, sOptions);
		}
		
		foreach (var t in _triggers.Window) {
			var s2 = "    " + t.Event.ToString();
			if (t.Later != 0) s2 = s2 + ", later " + t.Later;
			_Add(t, t.ParamsString, s2);
		}
		
		return new(a.OrderBy(o => _TriggerTypeToInt(o)).ThenBy(o => o.fileIndex).ThenBy(o => o.t.SourceLine));
		
		bool _InScope(ActionTrigger t, wnd w) {
			if (t.Scope is { } scope) {
				if (!dScopes.TryGetValue(t.Scope, out bool match)) {
					dScopes[scope] = match = scope.Match(w, wfCache);
				}
				if (!match) return false;
			}
			return true;
		}
		
		void _Add(ActionTrigger t, string sTrigger, string sOptions) {
			var sa = _GetAction(t, out int fileIndex);
			//print.it($"<><open {t.SourceFile}|{t.SourceLine}>{sTrigger}<>  <\a>{sa}</\a>");
			if (t.Disabled) sOptions += "    DISABLED";
			a.Add(new(t, sTrigger, sOptions, sa, fileIndex));
		}
		
		string _GetAction(ActionTrigger t, out int fileIndex) {
			string text, file = t.SourceFile; int[] lines;
			for (fileIndex = aFiles.Count; --fileIndex >= 0;) if (aFiles[fileIndex].path == file) break;
			if (fileIndex < 0) {
				fileIndex = aFiles.Count;
				try { text = filesystem.loadText(file); } catch (Exception) { text = ""; }
				
				//find line offsets once, not for each trigger (slow)
				text = text.ReplaceLineEndings("\n");
				lines = new int[text.AsSpan().Count('\n') + 1];
				for (int j = 1; j < lines.Length; j++) lines[j] = text.IndexOf('\n', lines[j - 1]) + 1;
				
				aFiles.Add((file, text, lines));
			} else {
				text = aFiles[fileIndex].text;
				lines = aFiles[fileIndex].lines;
			}
			
			int sourceLine = t.SourceLine - 1;
			if ((uint)sourceLine < lines.Length) {
				int start = lines[sourceLine], end = sourceLine + 1 < lines.Length ? lines[sourceLine + 1] - 1 : text.Length;
				if (!text.RxMatch(@"\]\s*=\s*(?|\w+\s*=>\s*(.+)|(.+))", 1, out string s, range: start..end)) return null;
				if (s.Ends("\"\"\"")) {
					if (text.RxMatch(@"(?s)\s+(.+?)\R\h*""""""", 1, out string s2, range: end..)) s += s2.RxReplace(@"\R\h*", "    ");
				} else if (t is AutotextTrigger && s.Ends(".Menu(")) {
					if (text.RxMatch(@"(?s)\s+(.+?)\R\h*\);", 1, out string s2, range: end..)) s += s2.RxReplace(@"\R\h*", "    ");
				}
				return s.Limit(200);
			}
			return null;
		}
	}
	
	static int _TriggerTypeToInt(_TLItem t) => t.t switch { HotkeyTrigger => 0, AutotextTrigger => 1, MouseTrigger => 2, _ => 3 };
	
	record _TLItem(ActionTrigger t, string trigger, string options, string action, int fileIndex) {
		public ReadOnlySpan<char> GetSortInfo(out int keyWeight, out int modWeight) {
			if (t is HotkeyTrigger k) {
				int mod = (int)k.modMask ^ 15 | (int)k.modMasked;
				modWeight = (((mod & 2) << 3 | (mod & 4) << 3 | (mod & 1) << 6 | (mod & 8) << 4)) >> 4 | System.Numerics.BitOperations.PopCount((uint)mod) << 4;
				
				int i = trigger.LastIndexOf('+') + 1;
				var s = trigger.AsSpan(i).Trim();
				keyWeight = s.Length > 1 ? 2 : s[0].IsAsciiAlphaDigit() ? 1 : 0;
				
				return s;
			}
			keyWeight = modWeight = 0;
			return trigger;
		}
	}
	
	class _SortComparer : System.Collections.IComparer {
		int System.Collections.IComparer.Compare(object o1, object o2) {
			if (o1 is _TLItem i1 && o2 is _TLItem i2) {
				var s1 = i1.GetSortInfo(out int keyWeight1, out int modWeight1);
				var s2 = i2.GetSortInfo(out int keyWeight2, out int modWeight2);
				if (i1.t is not HotkeyTrigger) return s1.CompareTo(s2, StringComparison.CurrentCultureIgnoreCase);
				if (s_sett.sortByMod) {
					int r = modWeight1 - modWeight2;
					if (r == 0) r = keyWeight1 - keyWeight2;
					if (r == 0) r = s1.CompareTo(s2, StringComparison.CurrentCultureIgnoreCase);
					return r;
				} else {
					int r = keyWeight1 - keyWeight2;
					if (r == 0) r = s1.CompareTo(s2, StringComparison.CurrentCultureIgnoreCase);
					if (r == 0) r = modWeight1 - modWeight2;
					return r;
				}
			}
			return 0;
		}
	}
	
	internal record class _Settings : JSettings {
		public static readonly string File = folders.ThisAppDataRoaming + @"TriggersListWindow settings.json";
		
		public static _Settings Load() => Load<_Settings>(File);
		
		public bool autoClose = true;
		public bool compact;
		public bool sort, sortByMod;
		public bool runOnEnter, runOn2Click;
		public int triggerType;
	}
	
	static readonly _Settings s_sett = _Settings.Load();
}
