extern alias CAW;

using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.FindSymbols;
using CAW::Microsoft.CodeAnalysis.PatternMatching;
using CAW::Microsoft.CodeAnalysis.Shared.Collections;

using Au.Controls;
using System.Windows.Input;

class CiFindGo : KDialogWindow {
	TextBox _tQuery;
	KCheckBox _cFuzzy, _cKeepOpen;
	KTreeView _tv;
	CancellationTokenSource _cancelTS;
	
	public static void ShowSingle() {
		if (Panels.Editor.ActiveDoc?.FN.IsCodeFile != true) return;
		ShowSingle(() => new CiFindGo());
	}
	
	CiFindGo() {
		InitWinProp("Find symbol", App.Wmain);
		var b = new wpfBuilder(this).WinSize(600, 600).Columns(-1, 0, 0, 0);
		
		_timer1 = new(_ => _Update());
		
		b.R.Add(out _tQuery, s_lastQuery).Font("Consolas").Align(y: VerticalAlignment.Center).Focus()
			.Tooltip("""
Symbol name.
Can be part, or part1 part2, or Type.Member, or camel.
If fuzzy, must be full name, 1 word.
Filters: t Type, m Member, n Namespace.
""");
		_tQuery.TextChanged += (_, _) => _timer1.After(200);
		
		b.Add(out _cFuzzy, "Fuzzy");
		_cFuzzy.CheckChanged += (_, _) => _timer1.After(200);
		
		b.xAddButtonIcon("*EvaIcons.Options2" + Menus.green, _Options, "Tool settings");
		_cKeepOpen = b.xAddCheckIcon("*MaterialLight.Pin" + Menus.black, "Keep this window open");
		
		b.Row(-1).Add(out _tv);
		_tv.CustomDraw = new _TvDraw();
		_tv.CustomItemHeightAddPercent = 100;
		_tv.ItemMarginLeft = 8;
		_tv.HotTrack = true;
		_tv.SingleClickActivate = !App.Settings.ci_findgoDclick;
		_tv.ItemActivated += e => {
			if (!_cKeepOpen.IsChecked) Close();
			(e.Item as _TvItem).Clicked();
		};
		
		b.End();
		
		if (!s_lastQuery.NE()) {
			_tQuery.SelectAll();
			_timer1.After(1);
		}
		
		b.WinSaved(App.Settings.wndpos.symbol, o => App.Settings.wndpos.symbol = o);
	}
	
	timer _timer1;
	static string s_lastQuery;
	
	async void _Update() {
		_cancelTS?.Cancel(); //it seems the API always runs in this thread, but anyway
		
		var query = _tQuery.Text.Trim();
		if (query.Length < 2) {
			_tv.SetItems(null);
			return;
		}
		s_lastQuery = query;
		
		if (!CodeInfo.GetContextAndDocument(out var cd, metaToo: true)) return;
		
		var a = new List<_TvItem>();
		
		var filter = SymbolFilter.All;
		if (query.Length > 2 && query[1] is ' ' or ':' && query[0] is 't' or 'm' or 'n') {
			filter = query[0] switch { 't' => SymbolFilter.Type, 'm' => SymbolFilter.Member, _ => SymbolFilter.Namespace };
			query = query[2..].TrimStart();
		}
		
		bool fuzzy = _cFuzzy.IsChecked;
		
		var cancelTS = _cancelTS = new CancellationTokenSource();
		var cancelToken = cancelTS.Token;
		
		IEnumerable<ISymbol> e;
		var solution = cd.document.Project.Solution;
		try {
			if (fuzzy) {
				using var sq = SearchQuery.CreateFuzzy(query);
				e = await SymbolFinder.FindSourceDeclarationsWithCustomQueryAsync(solution, sq, filter, cancelToken);
			} else {
				e = await SymbolFinder.FindSourceDeclarationsWithPatternAsync(solution, query, filter, cancelToken);
			}
			//BAD: skips ctors. Same in VSCode, but in VS ok.
		}
		catch (OperationCanceledException) { return; }
		finally {
			cancelTS.Dispose();
			if (cancelTS == _cancelTS) _cancelTS = null;
		}
		
		if (!e.Any()) {
			_tv.SetItems(null);
			return;
		}
		
		var qname = PatternMatcher.GetNameAndContainer(query).name;
		using var matcher = PatternMatcher.CreatePatternMatcher(qname, includeMatchedSpans: true, allowFuzzyMatching: fuzzy);
		using var ta = TemporaryArray<PatternMatch>.Empty;
		var b1 = new StringBuilder();
		
		foreach (var sym in e) {
			string name = sym.JustName(), text;
			int nameStart = 0;
			if (sym is INamespaceSymbol) {
				text = sym.ToString(); /* with ancestor namespaces */
				nameStart = text.Length - sym.Name.Length;
			} else {
				text = sym.ToDisplayString(s_symbolFormat);
				if (name.Length < text.Length) {
					nameStart = text.FindWord(name, isWordChar: c => SyntaxFacts.IsIdentifierPartCharacter(c));
					Debug_.PrintIf(nameStart < 0, $"{name}, {text}");
				}
			}
			
			ImmutableArray<PatternMatch> matches = default;
			if (nameStart >= 0) {
				if (matcher.AddMatches(name, ref ta.AsRef())) {
					matches = ta.ToImmutableAndClear();
				} else ta.Clear();
			}
			
			string type = sym is INamespaceOrTypeSymbol ? null : sym.ContainingType?.ToDisplayString(s_symbolFormat);
			
			foreach (var loc in sym.Locations) {
				var st = loc.SourceTree;
				if (st == null) continue;
				string path = st.FilePath;
				string text2 = type != null ? $"in {type},  {path}" : path;
				var fn = CiProjects.FileOf(loc.SourceTree, solution);
				
				a.Add(new(sym, text, text2, matches, nameStart, fn, loc.SourceSpan.Start));
			}
		}
		
		a.Sort((x, y) => x.CompareTo(y));
		
		_tv.SetItems(a);
		_tv.Select(0, focus: true);
	}
	
	class _TvItem : ITreeViewItem {
		string _text, _text2, _kindImage, _accessImage;
		ImmutableArray<PatternMatch> _matches;
		int _nameStart, _pos;
		FileNode _f;
		
		public _TvItem(ISymbol sym, string text, string text2, ImmutableArray<PatternMatch> matches, int nameStart, FileNode f, int pos) {
			_text = text;
			_text2 = text2;
			_matches = matches;
			_nameStart = nameStart;
			_f = f;
			_pos = pos;
			(_kindImage, _accessImage) = sym.ImageResource();
		}
		
		#region ITreeViewItem
		
		object ITreeViewItem.Image => _kindImage;
		
		string ITreeViewItem.DisplayText => _text + "\n" + _text2; //for tooltip only
		
		int ITreeViewItem.MesureTextWidth(GdiTextRenderer tr) => Math.Max(tr.MeasureText(_text).width, tr.MeasureText(_text2).width);
		
		int ITreeViewItem.SelectedColor(TVColorInfo ci) => ci.isSelected ? 0xD5E1FF : -1;
		
		int ITreeViewItem.BorderColor(TVColorInfo ci) => ci.isSelected ? 0x91B1FF : -1;
		
		#endregion
		
		public void DrawText(TVDrawInfo d, GdiTextRenderer tr) {
			int y = d.yText;
			tr.MoveTo(d.xText, y);
			
			bool dark = d.colorInfo.isHighContrastDark && !d.colorInfo.isSelected;
			int textColor = dark ? 0xFFFFFF : 0;
			if (!_matches.IsDefault) {
				int i = 0;
				foreach (var v in _matches) {
					foreach (var t in v.MatchedSpans) {
						int from = _nameStart + t.Start, to = _nameStart + t.End;
						if (from > i) tr.DrawText(_text, 0, i..from);
						tr.DrawText(_text, textColor, from..to, dark ? 0x0080A0 : 0x80F0FF);
						i = to;
					}
				}
				if (i < _text.Length) tr.DrawText(_text, textColor, i..);
			} else {
				tr.DrawText(_text, textColor);
			}
			
			y = d.rect.top + d.lineHeight;
			tr.MoveTo(d.xText, y);
			tr.DrawText(_text2, dark ? textColor : 0x808080);
		}
		
		public void DrawMarginLeft(TVDrawInfo d, GdiTextRenderer tr) {
			if (_accessImage == null) return;
			var cxy = d.imageRect.Width;
			var ri = new System.Drawing.Rectangle(d.imageRect.left - cxy / 2, d.imageRect.top + cxy / 4, cxy, cxy);
			d.graphics.DrawImage(IconImageCache.Common.Get(_accessImage, d.dpi, isImage: true), ri);
		}
		
		public void Clicked() {
			App.Model.OpenAndGoTo(_f, columnOrPos: _pos);
		}
		
		public int CompareTo(_TvItem other) {
			var x = _matches;
			var y = other._matches;
			
			if (x.IsDefault) return y.IsDefault ? 0 : 1;
			if (y.IsDefault) return -1;
			
			//Debug.Assert(x.Length == y.Length); //Once. After restarting could not reproduce. The text was "MD5" or "MD5R", while editing an Au file. Then added `else ta.Clear();` in _Update().
			Debug_.PrintIf(x.Length != y.Length, $"x.Length={x.Length}, y.Length={y.Length}");
			if (x.Length != y.Length) return 0;
			
			int r = 0;
			for (int i = 0; i < x.Length; i++) r += x[i].CompareTo(y[i]);
			return r;
		}
	}
	
	class _TvDraw : ITVCustomDraw {
		TVDrawInfo _cd;
		GdiTextRenderer _tr;
		
		#region ITVCustomDraw
		
		public void Begin(TVDrawInfo cd, GdiTextRenderer tr) {
			_cd = cd;
			_tr = tr;
		}
		
		//public bool DrawBackground() {
		
		//	return default;
		//}
		
		//public bool DrawImage(System.Drawing.Bitmap image) {
		
		//	return default;
		//}
		
		public bool DrawText() {
			(_cd.item as _TvItem).DrawText(_cd, _tr);
			return true;
		}
		
		public void DrawMarginLeft() {
			(_cd.item as _TvItem).DrawMarginLeft(_cd, _tr);
		}
		
		//public void DrawMarginRight() {
		
		//}
		
		//public void End() {
		
		//}
		
		#endregion
	}
	
	const SymbolDisplayMiscellaneousOptions s_miscDisplayOptions =
		SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral
		| SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
		//| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier //?
		//| SymbolDisplayMiscellaneousOptions.IncludeNotNullableReferenceTypeModifier //!
		| SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName
		| SymbolDisplayMiscellaneousOptions.UseSpecialTypes;
	const SymbolDisplayParameterOptions s_parameterDisplayOptions =
		SymbolDisplayParameterOptions.IncludeType
		| SymbolDisplayParameterOptions.IncludeName
		| SymbolDisplayParameterOptions.IncludeParamsRefOut
		//| SymbolDisplayParameterOptions.IncludeOptionalBrackets
		;
	
	internal static readonly SymbolDisplayFormat s_symbolFormat = new(
		SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
		SymbolDisplayGenericsOptions.IncludeTypeParameters,
		SymbolDisplayMemberOptions.IncludeParameters,
		SymbolDisplayDelegateStyle.NameAndSignature,
		SymbolDisplayExtensionMethodStyle.Default,
		s_parameterDisplayOptions,
		SymbolDisplayPropertyStyle.NameOnly,
		SymbolDisplayLocalOptions.IncludeType,
		SymbolDisplayKindOptions.None,
		s_miscDisplayOptions
		);
	
	protected override void OnPreviewKeyDown(KeyEventArgs e) {
		if (e.Source != _tv && e.Key is Key.Enter or Key.Down or Key.Up or Key.PageDown or Key.PageUp) {
			_tv.ProcessKey(e.Key);
			e.Handled = true;
		} else if (e.Key is Key.Escape) {
			Close();
			e.Handled = true;
		}
		base.OnPreviewKeyDown(e);
	}
	
	protected override void OnDeactivated(EventArgs e) {
		base.OnDeactivated(e);
		if (IsVisible && !_cKeepOpen.IsChecked) {
			//need timer, else something activates wrong window, even with InvokeAsync
			timer.after(25, _ => { if (!IsActive) Close(); });
		}
	}
	
	void _Options(WBButtonClickArgs e) {
		var m = new popupMenu();
		
		m.AddCheck("Double-click", App.Settings.ci_findgoDclick, _ => {
			App.Settings.ci_findgoDclick ^= true;
			_tv.SingleClickActivate = !App.Settings.ci_findgoDclick;
		});
		
		m.Show(owner: this);
	}
}
