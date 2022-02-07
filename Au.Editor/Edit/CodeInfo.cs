//#define NO_COMPL_CORR_SIGN

using System.Linq;
using Au.Compiler;
using Au.Controls;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Windows.Input;
using System.Windows;
using Microsoft.CodeAnalysis.Completion;
//using Microsoft.CodeAnalysis.Options;

static class CodeInfo
{
	internal static readonly CiCompletion _compl = new();
	internal static readonly CiSignature _signature = new();
	internal static readonly CiAutocorrect _correct = new();
	internal static readonly CiQuickInfo _quickInfo = new();
	internal static readonly CiStyling _styling = new();
	internal static readonly CiErrors _diag = new();
	internal static readonly CiTools _tools = new();
	//internal static readonly CiFavorite _favorite = new();

	static Solution _solution;
	static ProjectId _projectId;
	static DocumentId _documentId;
	static Document _document;
	static MetaComments _meta;
	static string _metaText;
	static bool _isWarm;
	static bool _isUI;
	static RECT _sciRect;

	public static void UiLoaded() {
		//This code warms up Roslyn. It can take several s.
		//	During that time the window is visible (except document) but disabled.

		perf.next('u');
		//don't allow users to make any changes until Roslyn loaded. It can be dangerous.
		App.Hmain.Enable(false);
		wnd[] aEnable = null;
		App.Dispatcher.InvokeAsync(() => { //disable floating panels too
			aEnable = wnd.getwnd.threadWindows(process.thisThreadId, onlyVisible: true);
			//print.it(aEnable);
			for (int i = 0; i < aEnable.Length; i++)
				if (aEnable[i].IsEnabled()) aEnable[i].Enable(false); else aEnable[i] = default;
		});
		var doc = Panels.Editor.ZActiveDoc;
		if (doc != null) doc.Visibility = Visibility.Hidden; //hide document window. The black unfolded text is distracting. Does not have sense to show it.

		Task.Run(() => {
			//using var p1 = perf.local();
			try {
				var code = @"using Au; print.it(""t"" + 1);";

				var refs = new MetaReferences().Refs;
				ProjectId projectId = ProjectId.CreateNewId();
				DocumentId documentId = DocumentId.CreateNewId(projectId);
				using var ws = new AdhocWorkspace();
				var sol = ws.CurrentSolution
					.AddProject(projectId, "p", "p", LanguageNames.CSharp)
					.AddMetadataReferences(projectId, refs)
					.AddDocument(documentId, "f.cs", code);
				var document = sol.GetDocument(documentId);
				//p1.Next();

				var semo = document.GetSemanticModelAsync().Result;
				//p1.Next('s');

				//let the coloring and folding in editor start working immediately
				Microsoft.CodeAnalysis.Classification.Classifier.GetClassifiedSpansAsync(document, new TextSpan(0, code.Length)).Wait();
				//p1.Next('c');

				App.Dispatcher.InvokeAsync(() => {
					_isWarm = true;
					ReadyForStyling?.Invoke();
					Panels.Editor.ZActiveDocChanged += Stop;
					App.Timer025sWhenVisible += _Timer025sWhenVisible;
					_Finally();
				});
				//p1.Next();

				500.ms();
				//p1.Next();
				var compl = CompletionService.GetService(document);
				compl.GetCompletionsAsync(document, code.IndexOf(".it") + 1); //not necessary, but without it sometimes the first completion list is too slow if the user types fast
																			  //p1.Next('C');

				Compiler.Warmup(document); //not necessary, but it's better when the first compilation is 200 ms instead of 500

				//EdUtil.MinimizeProcessPhysicalMemory(500); //with this later significantly slower
			}
			catch (Exception ex) {
				print.it(ex);
				App.Dispatcher.InvokeAsync(_Finally);
			}
		});

		void _Finally() {
			if (doc != null) doc.Visibility = Visibility.Visible;
			App.Hmain.Enable(true);
			if (aEnable != null) {
				for (int i = 0; i < aEnable.Length; i++)
					if (!aEnable[i].Is0) aEnable[i].Enable(true);
			}
			perf.nw('R');
		}
	}

	/// <summary>
	/// Code styling and folding already can work after program starts.
	/// </summary>
	public static bool IsReadyForStyling => _isWarm;

	/// <summary>
	/// When code styling and folding already can work after program starts.
	/// Runs in main thread.
	/// </summary>
	public static event Action ReadyForStyling;

	static bool _CanWork(SciCode doc) {
		if (!_isWarm) return false;
		if (doc == null) return false;
		if (!doc.ZFile.IsCodeFile) return false;
		if (doc != Panels.Editor.ZActiveDoc) { _Uncache(); return false; } //maybe changed an inactive file that participates in current compilation //FUTURE: what if isn't open?
		return true;
	}

	static void _Uncache() {
		//print.it("_Uncache");
		CurrentWorkspace = null;
		_solution = null;
		_projectId = null;
		_documentId = null;
		_document = null;
		_meta = null;
		_metaText = null;
	}

	public static void Stop() {
		Cancel();
		_Uncache();
	}

	public static void Cancel() {
		HideTextPopupAndTempWindows();
		_compl.Cancel();
		_signature.Cancel();
	}

	/// <summary>
	/// Called when files added, deleted, moved, copied, imported.
	/// Eg need to update styling when a meta c file became [un]available or when project folder structure changed.
	/// </summary>
	public static void FilesChanged() {
		Stop();
		_styling.Update();
	}

	public static void SciKillFocus(SciCode doc) {
		if (!_CanWork(doc)) return;
#if DEBUG
		if (Debugger.IsAttached) return;
#endif
		//hide code info windows, except when a code info window is focused. Code info window names start with "Ci.".
		var aw = wnd.thisThread.active;
		if (aw.Is0) Stop(); else if (!(KPopup.FromHwnd(aw) is KPopup p && p.Name.Starts("Ci."))) Cancel();
	}

	public static bool SciCmdKey(SciCode doc, KKey key, ModifierKeys mod) {
#if NO_COMPL_CORR_SIGN
		return false;
#endif
		if (!_CanWork(doc)) return false;
		switch ((key, mod)) {
		case (KKey.Space, ModifierKeys.Control):
			ShowCompletionList(doc);
			return true;
		case (KKey.Space, ModifierKeys.Control | ModifierKeys.Shift):
			ShowSignature(doc);
			return true;
		case (KKey.Escape, 0):
		case (KKey.Down, 0):
		case (KKey.Up, 0):
		case (KKey.PageDown, 0):
		case (KKey.PageUp, 0):
		case (KKey.Home, 0):
		case (KKey.End, 0):
			if ((HideTextPopup() || _tools.HideTempWindows()) && key == KKey.Escape) return true;
			//never mind: on Esc, if several popups, should hide the top popup.
			//	We instead hide less-priority popups when showing a popup, so that Escape will hide the correct popup in most cases.
			return _compl.OnCmdKey_SelectOrHide(key) || _signature.OnCmdKey(key);
		case (KKey.Tab, 0):
		case (KKey.Enter, 0):
			return _compl.OnCmdKey_Commit(doc, key) != CiComplResult.None || _correct.SciBeforeKey(doc, key, mod);
		case (KKey.Enter, ModifierKeys.Shift):
		case (KKey.Enter, ModifierKeys.Control):
		case (KKey.OemSemicolon, ModifierKeys.Control):
			var complResult = _compl.OnCmdKey_Commit(doc, key);
			//if(complResult == CiComplResult.Complex && mod==0) return true;
			return _correct.SciBeforeKey(doc, key, mod) | (complResult != CiComplResult.None);
		case (KKey.Back, 0):
		case (KKey.Delete, 0):
			return _correct.SciBeforeKey(doc, key, mod);
		}
		return false;
	}

	public static bool SciBeforeCharAdded(SciCode doc, char ch) {
#if NO_COMPL_CORR_SIGN
		return false;
#endif
		if (!_CanWork(doc)) return false;

		if (_correct.SciBeforeCharAdded(doc, ch, out var b)) {
			if (b == null) return true;

			if (_compl.IsVisibleUI) {
				int diff = b.newPosUtf8 - b.oldPosUtf8;
				_compl.SciCharAdding_Commit(doc, ch);
				b.newPosUtf8 = doc.zCurrentPos8 + diff;
			}

			doc.zCurrentPos8 = b.newPosUtf8;
			if (!b.dontSuppress) return true;
		} else if (_compl.IsVisibleUI) {
			if (CiComplResult.Complex == _compl.SciCharAdding_Commit(doc, ch)) return true;
		}
		return false;
	}

	public static void SciModified(SciCode doc, in Sci.SCNotification n) {
		if (!_CanWork(doc)) return;
		_document = null;
		_compl.SciModified(doc, in n);
		_styling.SciModified(doc, in n);
		_diag.SciModified(doc, in n);
		Panels.Outline.SciModified();
	}

	public static void SciCharAdded(SciCode doc, char ch) {
#if NO_COMPL_CORR_SIGN
		return;
#endif
		if (!_CanWork(doc)) return;

		using var c = new CharContext(doc, ch);
		_correct.SciCharAdded(c); //sync adds or removes ')' etc if need.
		if (!c.ignoreChar) {
			_compl.SciCharAdded_ShowList(c); //async gets completions and shows popup list. If already showing, filters/selects items.
			_signature.SciCharAdded(c.doc, c.ch); //async shows signature help. Faster than _compl.
		}

		//Example: user types 'wri('.
		//	When typed 'w', _compl.SciCharAdded_ShowList shows popup list (async).
		//	While typing 'ri', _compl.SciModified in the list selects Write.
		//	When typed '(':
		//		_compl.SciCharAdded_Commit replaces 'wri(' with 'Write('. Caret is after '('.
		//		_correct adds ')'. Caret is still after '('.
		//		_signature shows signature help.
		//	If then user types 'tr)':
		//		_compl on 't' shows popup list and on ')' replaces 'tr)' with 'true)'.
		//		_correct deletes the ')' it added before.
		//		_signature not called because discardChar==true. To hide signature help are used temp ranges.
		//	Finally we have 'Write(true)', and caret is after it, and no double '))'.
		//	If instead types 'tr;':
		//		_correct on ';' moves caret after ')', and finally we have 'Write(true);', and caret after ';'.
	}

	public static void SciUpdateUI(SciCode doc, int updated) {
#if NO_COMPL_CORR_SIGN
		return;
#endif
		//print.it("SciUpdateUI", modified, _tempNoAutoComplete);
		if (!_CanWork(doc)) return;

		if (0 != (updated & 3)) { //text (1), selection/click (2)
			_compl.SciUpdateUI(doc);
			_signature.SciPositionChanged(doc);
		} else if (0 != (updated & 12)) { //scrolled
			Cancel();
			if (0 != (updated & 4)) { //vertically
									  //_styling.Timer250msWhenVisibleAndWarm(doc); //rejected. Uses much CPU. The 250 ms timer is OK.
			}
		}
	}

	public static void ShowCompletionList(SciCode doc) {
		if (!_CanWork(doc)) return;
		_compl.ShowList();
	}

	public static void ShowSignature(SciCode doc = null) {
		doc ??= Panels.Editor.ZActiveDoc;
		if (!_CanWork(doc)) return;
		_signature.ShowSignature(doc);
	}

	/// <summary>
	/// Shows or hides quick info or/and error info.
	/// </summary>
	public static async void SciMouseDwellStarted(SciCode doc, int pos8) {
		if (!_CanWork(doc) || pos8 < 0) return;

		var text0 = doc.zText;
		int pos16 = doc.zPos16(pos8);
		var diag = _diag.GetPopupTextAt(doc, pos8, pos16, out var onLinkClick);
		var quick = await _quickInfo.GetTextAt(pos16);
		if (doc != Panels.Editor.ZActiveDoc || (object)text0 != doc.zText) return; //changed while awaiting

		if (diag == null && quick == null) {
			HideTextPopup();
		} else {
			var text = diag ?? quick;
			if (quick != null && diag != null) {
				text.Blocks.Add(new System.Windows.Documents.BlockUIContainer(new System.Windows.Controls.Separator { Margin = new(4) }));
				text.Blocks.Add(quick);
			}
			_ShowTextPopup(doc, pos16, text, onLinkClick);
		}
	}

	public static void SciMouseDwellEnded(SciCode doc) {
		if (!_CanWork(doc)) return;
		//_diag.SciMouseDwellEnded(doc);
	}

	//public static void SciMouseMoved(SciCode doc, int x, int y)
	//{
	//	if(!_CanWork(doc)) return;
	//	_quickInfo.SciMouseMoved(x, y);
	//}

	/// <summary>
	/// Call this before pasting or inserting text when may need special processing, eg auto-inserting 'using' directives.
	/// </summary>
	/// <param name="silent">Insert missing usings without showing dialog.</param>
	public static void Pasting(SciCode doc, bool silent = false) {
		if (!_CanWork(doc)) return;
		_diag.Pasting(doc, silent);
	}

	public class Context
	{
		public Document document { get; private set; }
		public readonly SciCode sciDoc;
		public readonly string code;
		public readonly (int start, int end) meta;
		public int pos16;
		public readonly bool isCodeFile;

		/// <summary>
		/// Initializes all fields except document.
		/// For sciDoc uses Panels.Editor.ZActiveDoc.
		/// </summary>
		public Context(int pos) {
			Debug.Assert(Thread.CurrentThread.ManagedThreadId == 1);

			document = null;
			sciDoc = Panels.Editor.ZActiveDoc;
			code = sciDoc.zText;
			if (pos == -1) pos = sciDoc.zCurrentPos16; else if (pos == -2) pos = sciDoc.zSelectionStart16;
			pos16 = pos;
			if (isCodeFile = sciDoc.ZFile.IsCodeFile) meta = MetaComments.FindMetaComments(code);
		}

		/// <summary>
		/// Initializes the document field.
		/// Creates or updates Solution if need.
		/// Returns false if fails, unlikely.
		/// </summary>
		public bool GetDocument() {
			if (_document != null) {
				document = _document;
				return true;
			}

			if (_solution != null && !code.Eq(meta.start..meta.end, _metaText)) {
				_Uncache();
				_styling.Update();
			}
			if (_solution == null) _metaText = code[meta.start..meta.end];

			try {
				if (_solution == null) {
					_CreateWorkspace(sciDoc.ZFile);
				} else {
					_solution = _solution.WithDocumentText(_documentId, SourceText.From(code, Encoding.UTF8));
				}
			}
			catch (Exception ex) {
				//Debug_.Print(ex);
				print.it(ex);
				return false;
			}

			document = _document = _solution.GetDocument(_documentId);
			if (document == null) return false;

			//_ModifySource();

			return true;
		}

		//public bool GetDocumentAndSyntaxRoot(out SyntaxNode root)
		//{
		//	if(!GetDocument()) { root = null; return false; }
		//	root = document.GetSyntaxRootAsync().Result;
		//	return true;
		//}

		//public bool GetDocumentAndFindNode(out SyntaxNode node)
		//{
		//	if(!GetDocument()) { node = null; return false; }
		//	node = document.GetSyntaxRootAsync().Result.FindToken(position).Parent;
		//	return true;
		//}

		//this was a failed attempt to modify code of top-level-statements (TLS) script to avoid code info problems.
		//	I see 2 ways:
		//		1. The best would be to surround TLS with {  }.
		//		2. Surround with semicolons. But it solves only some problems. Also then no error if real semicolon is missing.
		//	Problem: can only replace code but not insert. Then all reported positions (styling, errors, etc) don't match positions in code editor. Would be too complex to make it work.
		//		For this reason cannot surround TLS with {  } if TLS starts at very start of code.
		//	Hope Roslyn in the future will support TLS better.
		//	FUTURE: try to work with this again if Roslyn still lazy. Eg if default template starts with comments, can surround with { }, and never mind if TLS stars from very start.
		//void _ModifySource() {
		//	var cu = document.GetSyntaxRootAsync().Result as CompilationUnitSyntax;
		//	//print.it("Externs:", r.Externs);
		//	//print.it("Usings:", r.Usings);
		//	//print.it("AttributeLists:", r.AttributeLists);
		//	//print.it("Members:", r.Members);
		//	if (cu.Members.FirstOrDefault() is not GlobalStatementSyntax) return;
		//	var ms = cu.Members.Span; CiUtil.HiliteRange(ms);
		//	//document.

		//	//	document = _document = document.With...;
		//}
	}

	/// <summary>
	/// Creates new Context and calls its GetDocument.
	/// Returns false if: 1. Not a code file; 2. position is in meta comments (unless metaToo==true); 3. Fails to create/update Solution (unlikely). Then r.document is null.
	/// If returns true, r.document is Document for Panels.Editor.ActiveDoc. If need, parses meta, creates Project, Solution, etc.
	/// Always sets other r fields.
	/// </summary>
	/// <param name="position">If -1, gets current position. If -2, gets selection start.</param>
	/// <param name="metaToo">Don't return false if position is in meta comments.</param>
	public static bool GetContextAndDocument(out Context r, int position = -1, bool metaToo = false) {
		if (!GetContextWithoutDocument(out r, position, metaToo)) return false;
		return r.GetDocument();
	}

	/// <summary>
	/// Creates new Context with document=null. Even if returns false.
	/// Returns false if: 1. Not a code file; 2. position is in meta comments (unless metaToo==true).
	/// </summary>
	/// <param name="position">If -1, gets current position. If -2, gets selection start.</param>
	/// <param name="metaToo">Don't return false if position is in meta comments.</param>
	public static bool GetContextWithoutDocument(out Context r, int position = -1, bool metaToo = false) {
		r = new Context(position);
		if (!r.isCodeFile) return false;
		if (!metaToo && r.pos16 < r.meta.end && r.pos16 > r.meta.start) return false;
		return true;
	}

	/// <summary>
	/// Calls <see cref="GetContextAndDocument"/>, gets its syntax root and finds node.
	/// </summary>
	/// <param name="position">If -1, gets current position. If -2, gets selection start.</param>
	/// <param name="metaToo">Don't return false if position is in meta comments.</param>
	public static bool GetDocumentAndFindNode(out Context r, out SyntaxNode node, int position = -1, bool metaToo = false, bool findInsideTrivia = false) {
		if (!GetContextAndDocument(out r, position, metaToo)) { node = null; return false; }
		node = r.document.GetSyntaxRootAsync().Result.FindToken(r.pos16, findInsideTrivia).Parent;
		return true;
	}

	/// <summary>
	/// Calls <see cref="GetContextAndDocument"/>, gets its syntax root and finds token.
	/// </summary>
	/// <param name="position">If -1, gets current position. If -2, gets selection start.</param>
	/// <param name="metaToo">Don't return false if position is in meta comments.</param>
	public static bool GetDocumentAndFindToken(out Context r, out SyntaxToken token, int position = -1, bool metaToo = false, bool findInsideTrivia = false) {
		if (!GetContextAndDocument(out r, position, metaToo)) { token = default; return false; }
		token = r.document.GetSyntaxRootAsync().Result.FindToken(r.pos16, findInsideTrivia);
		return true;
	}

	public static Workspace CurrentWorkspace { get; private set; }

	public static MetaComments Meta => _meta;

	static void _CreateWorkspace(FileNode f) {
		_diag.ClearMetaErrors();
		InternalsVisible.Clear();
		CurrentWorkspace = new AdhocWorkspace();

		//how to remove option "ShowRemarksInQuickInfo"? Never mind, our CiQuickInfo just skips it.
		//var opt = CurrentWorkspace.Options
		//	.WithChangedOption(Microsoft.CodeAnalysis.QuickInfo.QuickInfoOptions.ShowRemarksInQuickInfo, "C#", false); //used to work, but now the property is removed
		//	.WithChangedOption(Microsoft.CodeAnalysis.QuickInfo.QuickInfoOptions.Metadata.ShowRemarksInQuickInfo, "C#", false); //now the property is in Metadata, but does not work
		//	.WithChangedOption(new PerLanguageOption2<bool>("QuickInfoOptions", "ShowRemarksInQuickInfo", false), "C#", false); //does not work too
		//CurrentWorkspace.SetOptions(opt);

		_solution = CurrentWorkspace.CurrentSolution;
		_projectId = _AddProject(f, true);

		static ProjectId _AddProject(FileNode f, bool isMain) {
			var f0 = f;
			if (f.FindProject(out var projFolder, out var projMain)) f = projMain;

			var m = new MetaComments();
			m.Parse(f, projFolder, EMPFlags.ForCodeInfo);
			if (isMain) _meta = m;
			if (m.TestInternal is string[] testInternal) InternalsVisible.Add(f.Name, testInternal);

			var projectId = ProjectId.CreateNewId();
			var adi = new List<DocumentInfo>();
			foreach (var f1 in m.CodeFiles) {
				var docId = DocumentId.CreateNewId(projectId);
				var tav = TextAndVersion.Create(SourceText.From(f1.code, Encoding.UTF8), VersionStamp.Default, f1.f.FilePath);
				adi.Add(DocumentInfo.Create(docId, f1.f.Name, null, SourceCodeKind.Regular, TextLoader.From(tav), f1.f.ItemPath));
				if (f1.f == f0 && isMain) {
					_documentId = docId;
				}
			}
			//SHOULDDO: reuse document+syntaxtree of global.cs and its meta c files if their text not changed.

			var pi = ProjectInfo.Create(projectId, VersionStamp.Default, f.Name, f.Name, LanguageNames.CSharp, null, null,
				m.CreateCompilationOptions(),
				m.CreateParseOptions(),
				adi,
				m.ProjectReferences?.Select(f1 => new ProjectReference(_AddProject(f1, false))),
				m.References.Refs);

			_solution = _solution.AddProject(pi);
			//info: does not add to CurrentWorkspace.CurrentSolution. Now _solution != CurrentWorkspace.CurrentSolution. Even after Workspace.ApplyChanges.

			return projectId;
		}
	}

	private static void _Timer025sWhenVisible() {
		var doc = Panels.Editor.ZActiveDoc;
		if (!_CanWork(doc)) {
			Panels.Outline.Clear();
			return;
		}

		//cancel if changed the screen rectangle of the document window
		if (_compl.IsVisibleUI || _signature.IsVisibleUI || _tpVisible) {
			var r = Panels.Editor.ZActiveDoc.Hwnd().Rect;
			if (!_isUI) {
				_isUI = true;
				_sciRect = r;
			} else if (r != _sciRect) { //moved/resized top-level window or eg moved some splitter
				_isUI = false;
				Cancel();
			}
		} else if (_isUI) {
			_isUI = false;
		}

		_styling.Timer250msWhenVisibleAndWarm(doc);
		Panels.Outline.Timer025sWhenVisible();
	}

	static CiPopupText _textPopup;
	static bool _tpVisible;

	static void _ShowTextPopup(SciCode doc, int pos16, System.Windows.Documents.Section text, Action<CiPopupText, string> onLinkClick = null) {
		_textPopup ??= new CiPopupText(CiPopupText.UsedBy.Info, onHiddenOrDestroyed: (_, _) => _tpVisible = false);
		_textPopup.Text = text;
		_textPopup.OnLinkClick = onLinkClick;
		_textPopup.Show(doc, pos16, hideIfOutside: true);
		_tpVisible = true;
	}

	//CONSIDER: option to show tooltip: below mouse (like now), above mouse, top/bottom (which is farther), maybe above Output etc.
	//	This test version shows above Output.
	//static void _ShowTextPopup(SciCode doc, int pos16, System.Windows.Documents.Section text, Action<CiPopupText, string> onLinkClick = null) {
	//	_textPopup ??= new CiPopupText(CiPopupText.UsedBy.Info, onHiddenOrDestroyed: (_, _) => _tpVisible = false);
	//	_textPopup.Text = text;
	//	_textPopup.OnLinkClick = onLinkClick;
	//	if (keys.isScrollLock && Panels.Output.IsVisible) {
	//		var r = Panels.Output.RectInScreen();
	//		_textPopup.Show(doc, r, null);
	//	} else {
	//		_textPopup.Show(doc, pos16, hideIfOutside: true);
	//	}
	//	_tpVisible = true;
	//}

	internal static bool HideTextPopup() {
		if (_tpVisible) { _textPopup.Hide(); return true; }
		return false;
	}

	internal static void HideTextPopupAndTempWindows() {
		HideTextPopup();
		_tools.HideTempWindows();
	}

	public class CharContext : IDisposable
	{
		public readonly SciCode doc;
		public char ch;
		public bool ignoreChar;
		//bool _undoStarted;

		public CharContext(SciCode doc, char ch) {
			this.doc = doc;
			this.ch = ch;
		}

		//public void BeginUndoAction()
		//{
		//	if(!_undoStarted) {
		//		_undoStarted = true;
		//		doc.Call(Sci.SCI_BEGINUNDOACTION);
		//	}
		//}

		public void Dispose() {
			//if(_undoStarted) {
			//	_undoStarted = false;
			//	doc.Call(Sci.SCI_ENDUNDOACTION);
			//}
		}
	}

	//public static void Test()
	//{

	//}
}
