extern alias CAW;

//#define NO_COMPL_CORR_SIGN

using System.Windows.Input;
using System.Windows;
using Au.Compiler;
using Au.Controls;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Classification;
using CAW::Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Completion;

static class CodeInfo {
	internal static readonly CiCompletion _compl = new();
	internal static readonly CiSignature _signature = new();
	internal static readonly CiAutocorrect _correct = new();
	internal static readonly CiQuickInfo _quickInfo = new();
	internal static readonly CiStyling _styling = new();
	internal static readonly CiErrors _diag = new();
	internal static readonly CiTools _tools = new();
	internal static readonly CiProjects _projects = new();
	
	static Solution _solution;
	static ProjectId _projectId;
	static DocumentId _documentId;
	static Document _document;
	static CompilationUnitSyntax _syntaxRoot;
	//static MetaComments _meta;
	static string _metaText;
	static bool _isWarm;
	static bool _isUI;
	static RECT _sciRect;
	
	public static void UiLoaded() {
		//This code warms up Roslyn. It can take several s.
		//	During that time the window is visible (except document) but disabled.
		
		//perf.next('u');
		//don't allow users to make any changes until Roslyn loaded. It can be dangerous.
		App.Hmain.Enable(false);
		wnd[] aEnable = null;
		App.Dispatcher.InvokeAsync(() => { //disable floating panels too
			aEnable = wnd.getwnd.threadWindows(process.thisThreadId, onlyVisible: true);
			//print.it(aEnable);
			for (int i = 0; i < aEnable.Length; i++)
				if (aEnable[i].IsEnabled()) aEnable[i].Enable(false); else aEnable[i] = default;
		});
		var doc = Panels.Editor.ActiveDoc;
		if (doc != null) doc.Visibility = Visibility.Hidden; //hide document window. The black unfolded text is distracting. Does not have sense to show it.
		
		_warmupTask = Task.Run(() => {
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
				CiUtil.GetClassifiedSpansAsync(document, 0, code.Length).Wait();
				//p1.Next('c');
				
				App.Dispatcher.InvokeAsync(() => {
					_isWarm = true;
					ReadyForStyling?.Invoke();
					ReadyForStyling = null; //GC
					Panels.Editor.ActiveDocChanged += Stop;
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
			//perf.nw('R');
			_warmupTask = null;
			
			IsReadyForEditing = true;
			if (ReadyForEditing != null) {
				try { ReadyForEditing(); } catch (Exception e1) { print.it(e1); } //used in editorExtension scripts
				ReadyForEditing = null; //GC
			}
			Git.AutoBackup(true);
			
#if DEBUG
			RoslynMod.Print.PrintItCallback = o => print.it(o);
#endif
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
	
	/// <summary>
	/// Main window already enabled after program starts.
	/// </summary>
	public static bool IsReadyForEditing { get; private set; }
	
	/// <summary>
	/// When main window enabled after program starts.
	/// Runs in main thread, after <b>ReadyForStyling</b>.
	/// </summary>
	public static event Action ReadyForEditing;
	
	/// <summary>
	/// If the warmup task is still running, which means that still not ready for styling etc and should not open files, waits until the task completes.
	/// </summary>
	public static void WaitUntilReadyForStyling() {
		if (_warmupTask == null) return;
		_warmupTask.Wait();
		wait.doEvents(); //run Dispatcher.InvokeAsync(_Finally)
	}
	static Task _warmupTask;
	
	static bool _CanWork(SciCode doc) {
		if (!_isWarm) return false;
		if (doc == null) return false;
		if (!doc.EFile.IsCodeFile) return false;
		if (doc != Panels.Editor.ActiveDoc) { _Uncache(); return false; } //maybe changed an inactive file that participates in current compilation //FUTURE: what if isn't open?
		return true;
	}
	
	static void _Uncache() {
		//print.it("_Uncache");
		CurrentWorkspace?.Dispose();
		CurrentWorkspace = null;
		_solution = null;
		_projectId = null;
		_documentId = null;
		_document = null;
		_syntaxRoot = null;
		//_meta = null;
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
	/// Similar to <see cref="FilesChanged"/>, but called when other conditions changed, eg a nuget package [un]installed.
	/// Not async, and does not fire an event.
	/// </summary>
	public static void StopAndUpdateStyling() {
		Stop();
		_styling.Update();
	}
	
	/// <summary>
	/// Called when files added, deleted, moved, copied, imported, renamed, text replaced.
	/// Eg need to update styling and diagnostics when a meta c file became [un]available or when project folder structure changed.
	/// Will update async, eg single updating for multiple calls. Will call <see cref="StopAndUpdateStyling"/> and <see cref="FilesChangedEvent"/>.
	/// </summary>
	public static void FilesChanged() {
		if (_filesChangedAsync) return; _filesChangedAsync = true;
		App.Dispatcher.InvokeAsync(() => {
			_filesChangedAsync = false;
			StopAndUpdateStyling();
			FilesChangedEvent?.Invoke();
		});
		//why async:
		//	1. Easier to consolidate multiple events.
		//	2. Easier to create files with custom text (the text is added after calling this).
		//	3. In some cases may be better/safer.
	}
	static bool _filesChangedAsync;
	
	/// <summary>
	/// When files added, deleted, moved, copied, imported, renamed, text replaced.
	/// Called through Dispatcher.InvokeAsync and may consolidate multiple changes.
	/// </summary>
	public static event Action FilesChangedEvent;
	
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
			//	We instead hide less-priority popups when showing a popup. Then Esc will hide the correct popup in most cases.
			if (_compl.OnCmdKey_SelectOrHide(key) || _signature.OnCmdKey(key)) return true;
			if (key == KKey.Escape) if (doc.SnippetMode_?.SciKey(key, mod) == true) return true;
			break;
		case (KKey.Tab, 0):
		case (KKey.Enter, 0):
			if (_compl.OnCmdKey_Commit(doc, key) != CiComplResult.None) return true;
			if (doc.SnippetMode_?.SciKey(key, mod) == true) return true;
			if (_correct.SciBeforeKey(doc, key, mod)) return true;
			break;
		case (KKey.Tab, ModifierKeys.Shift):
			if (doc.SnippetMode_?.SciKey(key, mod) == true) return true;
			break;
		case (KKey.Enter, ModifierKeys.Shift) or (KKey.Enter, ModifierKeys.Control) or (KKey.Enter, ModifierKeys.Control | ModifierKeys.Shift):
		case (KKey.OemSemicolon, ModifierKeys.Control):
			var complResult = _compl.OnCmdKey_Commit(doc, key);
			if (complResult == CiComplResult.Complex) return true;
			if (_correct.SciBeforeKey(doc, key, mod) | (complResult != CiComplResult.None)) return true;
			break;
		case (KKey.Back, 0):
		case (KKey.Delete, 0):
			if (_correct.SciBeforeKey(doc, key, mod)) return true;
			break;
		}
		return false;
	}
	
	public static bool SciBeforeCharAdded(SciCode doc, char ch) {
#if NO_COMPL_CORR_SIGN
		return false;
#endif
		if (!_CanWork(doc)) return false;
		
		if (_compl.IsVisibleUI) {
			if (CiComplResult.Complex == _compl.SciCharAdding_Commit(doc, ch)) return true;
		}
		
		return _correct.SciBeforeCharAdded(doc, ch);
	}
	
	public static bool SciModified(SciCode doc, in Sci.SCNotification n) {
		if (!_CanWork(doc)) return false;
		_document = null;
		_compl.SciModified(doc, in n);
		_styling.SciModified(doc, in n);
		_diag.SciModified(doc, in n);
		Panels.Outline.SciModified();
		return true;
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
	
	public static void SciUpdateUI(SciCode doc, Sci.UPDATE updated) {
#if NO_COMPL_CORR_SIGN
		return;
#endif
		//print.it("SciUpdateUI", modified, _tempNoAutoComplete);
		if (!_CanWork(doc)) return;
		
		if (updated.HasAny(Sci.UPDATE.SC_UPDATE_CONTENT | Sci.UPDATE.SC_UPDATE_SELECTION)) {
			//_compl.SciUpdateUI(doc);
			_signature.SciPositionChanged(doc);
			CiFind.SciUpdateUI(doc, updated.Has(Sci.UPDATE.SC_UPDATE_CONTENT));
		} else if (updated.HasAny(Sci.UPDATE.SC_UPDATE_V_SCROLL | Sci.UPDATE.SC_UPDATE_H_SCROLL)) {
			Cancel();
			//if (0 != (updated & 4)) { //vertically
			//	_styling.Timer250msWhenVisibleAndWarm(doc); //rejected. Uses much CPU. The 250 ms timer is OK.
			//}
		}
	}
	
	public static void ShowCompletionList(SciCode doc = null) {
		doc ??= Panels.Editor.ActiveDoc;
		if (!_CanWork(doc)) return;
		_compl.ShowList();
	}
	
	public static void ShowSignature(SciCode doc = null) {
		doc ??= Panels.Editor.ActiveDoc;
		if (!_CanWork(doc)) return;
		_signature.ShowSignature(doc);
	}
	
	/// <summary>
	/// Shows or hides quick info or/and error info.
	/// </summary>
	public static async void SciMouseDwellStarted(SciCode doc, int pos8) {
		if (!_CanWork(doc) || pos8 < 0) return;
		
		var text0 = doc.aaaText;
		int pos16 = doc.aaaPos16(pos8);
		var diag = _diag.GetPopupTextAt(doc, pos8, pos16, out var onLinkClick);
		var quick = await _quickInfo.GetTextAt(pos16);
		if (doc != Panels.Editor.ActiveDoc || (object)text0 != doc.aaaText) return; //changed while awaiting
		
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
	
	/// <summary>
	/// Call this before pasting, dropping or inserting text when may need special processing, eg auto-inserting 'using' directives.
	/// </summary>
	/// <param name="silent">Insert missing usings without showing dialog.</param>
	public static void Pasting(SciCode doc, string text, bool silent = false) {
		if (!_CanWork(doc)) return;
		_diag.Pasting(doc, silent);
	}
	
	/// <summary>
	/// Call this after <see cref="Pasting"/> and then inserting text.
	/// Caret must be at the end of the inserted text. The inserted text can be selected (eg when dropped).
	/// </summary>
	public static void Pasted(SciCode doc, string text) {
		if (!_CanWork(doc)) return;
		_correct.SciPasted(doc, text);
	}
	
	public class Context {
		public readonly SciCode sci;
		public readonly string code;
		public readonly StartEnd meta;
		public int pos;
		public readonly bool isCodeFile;
		
		public Document document { get; private set; }
		
		public CompilationUnitSyntax syntaxRoot { get; private set; }
		
		public SemanticModel semanticModel => document.GetSemanticModelAsync().Result; //only first time slow
		
		/// <summary>
		/// Initializes all fields except document.
		/// For <b>sci</b> uses <b>Panels.Editor.aaActiveDoc</b>.
		/// </summary>
		/// <param name="pos">If -1, gets current position. If -2, gets selection start.</param>
		public Context(int pos) {
			Debug.Assert(Environment.CurrentManagedThreadId == 1);
			
			sci = Panels.Editor.ActiveDoc;
			code = sci.aaaText;
			this.pos = pos switch { -1 => sci.aaaCurrentPos16, -2 => sci.aaaSelectionStart16, _ => pos };
			Debug.Assert((uint)this.pos <= code.Length);
			if (isCodeFile = sci.EFile.IsCodeFile) meta = MetaComments.FindMetaComments(code);
		}
		
		/// <summary>
		/// Initializes the document field.
		/// Creates or updates Solution if need.
		/// Returns false if fails, eg if code too big.
		/// </summary>
		public bool GetDocument() {
			if (_document != null) {
				document = _document;
				syntaxRoot = _syntaxRoot;
				return true;
			}
			//perf.first();
			
			//return false if code is too big. Eg Roslyn hangs if pasted 20 MB """XML""".
			if (code.Length > 10_000_000) {
				_Uncache();
				return false;
			}
			
			if (_solution != null && !code.Eq(meta.start..meta.end, _metaText)) {
				_Uncache();
				_styling.Update();
			}
			if (_solution == null) _metaText = code[meta.start..meta.end];
			
			try {
				if (_solution == null) {
					_CreateWorkspace(sci);
				} else {
					_solution = _solution.WithDocumentText(_documentId, SourceText.From(code, Encoding.UTF8));
				}
			}
			catch (Exception ex) {
				//Debug_.Print(ex);
				print.it(ex);
				_Uncache();
				return false;
			}
			
			document = _document = _solution.GetDocument(_documentId);
			//perf.next();
			
			//syntaxRoot protects the syntax tree from GC. Creating it is expensive.
			//	Roslyn keeps just a week reference, and could have to recompute it for every task.
			//	Note: now I can't reproduce. It seems TryGetSyntaxRoot etc always succeeds.
			syntaxRoot = _syntaxRoot = document.GetSyntaxRootSynchronously(default) as CompilationUnitSyntax;
			
			//When certain invalid code exists, the Roslyn's copy of file code (syntaxRoot.GetText()) may be different (usually much shorter).
			//	Then calling various Roslyn functions would throw exception. Better return false now.
			//	It seems it was caused by _ModifyTLS, now removed. //FUTURE: remove this code?
			if (syntaxRoot.EndOfFileToken.SpanStart != code.Length) {
#if DEBUG
				print.clear();
				Debug_.Print("---- bad code ----");
				foreach (var v in syntaxRoot.ChildNodes()) CiUtil.PrintNode(v);
#endif
				return false;
			}
			
			return true;
		}
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
		if (Panels.Editor.ActiveDoc == null) { r = null; return false; }
		r = new Context(position);
		if (!r.isCodeFile) return false;
		if (!metaToo && r.pos < r.meta.end && r.pos > r.meta.start) return false;
		return true;
	}
	
	/// <summary>
	/// Calls <see cref="GetContextAndDocument"/>, gets its syntax root and finds node.
	/// </summary>
	/// <param name="position">If -1, gets current position. If -2, gets selection start.</param>
	/// <param name="metaToo">Don't return false if position is in meta comments.</param>
	public static bool GetDocumentAndFindNode(out Context r, out SyntaxNode node, int position = -1, bool metaToo = false, bool findInsideTrivia = false) {
		if (!GetContextAndDocument(out r, position, metaToo)) { node = null; return false; }
		node = r.syntaxRoot.FindToken(r.pos, findInsideTrivia).Parent;
		return true;
	}
	
	/// <summary>
	/// Calls <see cref="GetContextAndDocument"/>, gets its syntax root and finds token.
	/// </summary>
	/// <param name="position">If -1, gets current position. If -2, gets selection start.</param>
	/// <param name="metaToo">Don't return false if position is in meta comments.</param>
	public static bool GetDocumentAndFindToken(out Context r, out SyntaxToken token, int position = -1, bool metaToo = false, bool findInsideTrivia = false) {
		if (!GetContextAndDocument(out r, position, metaToo)) { token = default; return false; }
		token = r.syntaxRoot.FindToken(r.pos, findInsideTrivia);
		return true;
	}
	
	public static Workspace CurrentWorkspace { get; private set; }
	
	//public static MetaComments Meta => _meta;
	
	static void _CreateWorkspace(SciCode sci) {
		//TODO3: use same workspace if project/solution not changed.
		//	Here "solution" means when a project or file uses project references.
		//	Now eg slow GetSemanticModelAsync when [re]opening a file in a large project/solution.
		
		_diag.ClearMetaErrors();
		TestInternal.CiClear();
		CurrentWorkspace = new AdhocWorkspace();
		
		Dictionary<FileNode, ProjectReference> dPR = null;
		
		_solution = CurrentWorkspace.CurrentSolution;
		_projectId = _AddProject(sci.EFile, true, isWpfPreview: sci.EIsWpfPreview);
		
		ProjectId _AddProject(FileNode f, bool isMain, bool isWpfPreview = false) {
			var fCurrentDoc = f;
			if (f.FindProject(out var projFolder, out var projMain)) f = projMain;
			
			var m = new MetaComments(MCFlags.ForCodeInfoInEditor | (isWpfPreview ? MCFlags.WpfPreview : 0));
			m.Parse(f, projFolder);
			//if (isMain) _meta = m;
			
			if (m.TestInternal is string[] testInternal) TestInternal.CiAdd(m.Name, testInternal);
			
			var projectId = ProjectId.CreateNewId();
			CiProjects.AttachFileOf(projectId, f);
			var adi = new List<DocumentInfo>();
			foreach (var f1 in m.CodeFiles) {
				var docId = DocumentId.CreateNewId(projectId);
				CiProjects.AttachFileOf(docId, f1.f);
				var tav = TextAndVersion.Create(SourceText.From(f1.code, Encoding.UTF8), VersionStamp.Default, f1.f.FilePath);
				adi.Add(DocumentInfo.Create(docId, f1.f.Name, null, SourceCodeKind.Regular, TextLoader.From(tav), f1.f.ItemPath));
				if (f1.f == fCurrentDoc && isMain) {
					_documentId = docId;
				}
			}
			//TODO3: reuse document+syntaxtree of global.cs and its meta c files if their text not changed.
			
			List<ProjectReference> aPR = null;
			if (m.ProjectReferences is { } a1) {
				dPR ??= new();
				foreach (var v in a1) {
					if (!dPR.TryGetValue(v.f, out var pr)) {
						pr = new ProjectReference(_AddProject(v.f, false));
						dPR.Add(v.f, pr);
					}
					(aPR ??= new()).Add(pr);
				}
			}
			
			var pi = ProjectInfo.Create(projectId, VersionStamp.Default, m.Name, m.Name, LanguageNames.CSharp, null, null,
				m.CreateCompilationOptions(),
				m.CreateParseOptions(),
				adi,
				aPR,
				m.References.Refs);
			
			_solution = _solution.AddProject(pi);
			//info: does not add to CurrentWorkspace.CurrentSolution. Now _solution != CurrentWorkspace.CurrentSolution. Even after Workspace.ApplyChanges.
			
			return projectId;
		}
	}
	
	private static void _Timer025sWhenVisible() {
		var doc = Panels.Editor.ActiveDoc;
		if (!_CanWork(doc)) {
			Panels.Outline.Clear();
			return;
		}
		
		//cancel if changed the screen rectangle of the document window
		if (_compl.IsVisibleUI || _signature.IsVisibleUI || _tpVisible) {
			var r = Panels.Editor.ActiveDoc.AaWnd.Rect;
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
	
	public class CharContext : IDisposable {
		public readonly SciCode doc;
		public char ch;
		/// <summary>Don't show completions, signature, etc.</summary>
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
