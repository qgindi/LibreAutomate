using Au.Controls;
using Au.Tools;
using System.Windows;
using System.Windows.Controls;

static class Menus {
	public const string
		black = " #505050|#D0D0D0",
		blue = " #0080FF|#77C9FF",
		darkBlue = " #4040FF|#8080FF",
		green = " #99BF00|#A9CE13",
		brown = " #9F5300|#D0D0D0",
		purple = " #B340FF|#D595FF",
		darkYellow = " #EABB00",
		orange = " #FFA500",
		red = " #FF4040|#FF9595"
		;
	public const string
		iconBack = "*EvaIcons.ArrowBack" + black,
		iconTrigger = "*Codicons.SymbolEvent" + blue,
		iconIcons = "*FontAwesome.IconsSolid" + green,
		iconUndo = "*Ionicons.UndoiOS" + brown,
		iconPaste = "*Material.ContentPaste" + brown;
	
	[Command(target = "Files")]
	public static class File {
		[Command(target = "", image = "*EvaIcons.FileAddOutline" + blue)]
		public static class New {
			static FileNode _New(string name) => App.Model.NewItem(name, beginRenaming: true);
			
			[Command('s', keys = "Ctrl+N", image = FileNode.c_iconScript)]
			public static void New_script() { _New("Script.cs"); }
			
			[Command('c', image = FileNode.c_iconClass)]
			public static void New_class() { _New("Class.cs"); }
			
			[Command('t', image = "*FeatherIcons.FileText" + black)]
			public static void New_text_file() { _New("File.txt"); }
			
			[Command('f', image = FileNode.c_iconFolder)]
			public static void New_folder() { _New(null); }
			
			//CONSIDER: New_project. A simple dialog to make faster to enter names of the project folder and the main file. Also can display some info.
			
			public const int ItemCount = 4;
		}
		
		[Command("Delete...", separator = true, keysText = "Delete", image = "*Typicons.DocumentDelete" + black)]
		public static void Delete() { App.Model.DeleteSelected(); }
		
		[Command(keys = "F2", image = "*BoxIcons.RegularRename" + blue)]
		public static void Rename() { App.Model.RenameSelected(); }
		
		[Command(image = "*RemixIcon.ChatSettingsLine" + green)]
		public static void Properties() { App.Model.Properties(); }
		
		[Command("Copy, paste")]
		public static class CopyPaste {
			[Command("Multi-select", checkable = true, image = "*Modern.ListTwo" + green, tooltip = "Multi-select (with Ctrl or Shift).\nDouble click to open.")]
			public static void MultiSelect_files() { Panels.Files.TreeControl.SetMultiSelect(toggle: true); }
			
			[Command("Cu_t", separator = true, keysText = "Ctrl+X")]
			public static void Cut_file() { App.Model.CutCopySelected(true); }
			
			[Command("Copy", keysText = "Ctrl+C")]
			public static void Copy_file() { App.Model.CutCopySelected(false); }
			
			[Command("Paste", keysText = "Ctrl+V")]
			public static void Paste_file() { App.Model.Paste(); }
			
			[Command("Cancel Cut/Copy", keysText = "Esc")]
			public static void CancelCutCopy_file() { App.Model.Uncut(); }
			
			[Command('r', separator = true)]
			public static void Copy_relative_path() { App.Model.SelectedCopyPath(false); }
			
			[Command('f')]
			public static void Copy_full_path() { App.Model.SelectedCopyPath(true); }
		}
		
		[Command("Open, close, go")]
		public static class OpenCloseGo {
			[Command(keysText = "Enter")]
			public static void Open() { App.Model.OpenSelected(1); }
			
			[Command]
			public static void Open_in_default_app() { App.Model.OpenSelected(3); }
			
			[Command]
			public static void Select_in_explorer() { App.Model.OpenSelected(4); }
			
			[Command(separator = true, target = "", keys = "Ctrl+F4", keysText = "M-click")]
			public static void Close() { App.Model.CloseEtc(FilesModel.ECloseCmd.CloseSelectedOrCurrent); }
			
			[Command(target = "")]
			public static void Close_all() { App.Model.CloseEtc(FilesModel.ECloseCmd.CloseAll); }
			
			[Command(separator = true, target = "", image = "*Codicons.CollapseAll" + black)]
			public static void Collapse_all_folders() { App.Model.CloseEtc(FilesModel.ECloseCmd.CollapseAllFolders); }
			
			[Command(target = "", image = "*Codicons.CollapseAll" + black)]
			public static void Collapse_inactive_folders() { App.Model.CloseEtc(FilesModel.ECloseCmd.CollapseInactiveFolders); }
			
			[Command(separator = true, target = "", keys = "Ctrl+Tab")]
			public static void Previous_document() { var a = App.Model.OpenFiles; if (a.Count > 1) App.Model.SetCurrentFile(a[1]); }
			
			[Command(keys = "Alt+Left", target = "", image = iconBack)]
			public static void Go_back() { App.Model.EditGoBack.GoBack(); }
			
			[Command(keys = "Alt+Right", target = "", image = "*EvaIcons.ArrowForward" + black)]
			public static void Go_forward() { App.Model.EditGoBack.GoForward(); }
		}
		
		//[Command]
		//public static class More
		//{
		//	//[Command("...", separator = true)]
		//	//public static void Print_setup() { }
		
		//	//[Command("...")]
		//	//public static void Print() { }
		//}
		
		[Command("Export, import", separator = true)]
		public static class ExportImport {
			[Command("Export as .zip...")]
			public static void Export_as_zip() { App.Model.ExportSelected(zip: true); }
			
			[Command("...")]
			public static void Export_as_workspace() { App.Model.ExportSelected(zip: false); }
			
			[Command("Import .zip...", separator = true)]
			public static void Import_zip() {
				var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDA}") { Title = "Import .zip", FileTypes = "Zip files|*.zip" };
				if (d.ShowOpen(out string s, App.Hmain))
					App.Model.ImportWorkspace(s);
			}
			
			[Command("...")]
			public static void Import_workspace() {
				var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDA}") { Title = "Import workspace" };
				if (d.ShowOpen(out string s, App.Hmain, selectFolder: true))
					App.Model.ImportWorkspace(s);
			}
			
			[Command("...")]
			public static void Import_files() { App.Model.ImportFiles(false); }
			
			[Command("...")]
			public static void Import_folder() { App.Model.ImportFiles(true); }
		}
		
		[Command(target = "")]
		public static class Workspace {
			[Command]
			public static void Reload_this_workspace() { FilesModel.LoadWorkspace(App.Model.WorkspaceDirectory); }
			
			[Command("...", separator = true)]
			public static void Open_workspace() { FilesModel.OpenWorkspaceUI(); }
			
			[Command("...")]
			public static void New_workspace() { FilesModel.NewWorkspaceUI(); }
		
			[Command("...", separator = true)]
			public static void Repair_workspace() { RepairWorkspace.Repair(false); }
			
			[Command("...")]
			public static void Repair_this_folder() { RepairWorkspace.Repair(true); }
			
			[Command(separator = true, keys = "Ctrl+S", image = "*BoxIcons.RegularSave" + black)]
			public static void Save_now() { App.Model?.Save.AllNowIfNeed(); }
		}
		
		[Command(separator = true, target = "", keysText = "Alt+F4")]
		public static void Close_window() { if (App.Settings.runHidden) App.Wmain.Hide_(); else App.Wmain.Close(); }
		
		[Command(target = "")]
		public static void Exit() { Application.Current.Shutdown(); }
	}
	
	[Command(target = "Edit")]
	public static class Edit {
		[Command]
		public static class Clipboard {
			[Command('t', keys = "Ctrl+X", image = "*Zondicons.EditCut" + brown)]
			public static void Cut() { Panels.Editor.ActiveDoc.Call(Sci.SCI_CUT); }
			
			[Command(keys = "Ctrl+C", image = "*Material.ContentCopy" + brown)]
			public static void Copy() { Panels.Editor.ActiveDoc.ECopy(); }
			
			[Command(keys = "Ctrl+V", image = iconPaste)]
			public static void Paste() { Panels.Editor.ActiveDoc.EPaste(); }
			
			[Command(image = "*Material.ForumOutline" + brown, text = "Copy _forum code", separator = true)]
			public static void Forum_copy() { Panels.Editor.ActiveDoc.ECopy(SciCode.ECopyAs.Forum); }
			
			[Command("Copy HTML <span style>")]
			public static void Copy_HTML_span_style() { Panels.Editor.ActiveDoc.ECopy(SciCode.ECopyAs.HtmlSpanStyle); }
			
			[Command("Copy HTML <span class> and CSS")]
			public static void Copy_HTML_span_class_CSS() { Panels.Editor.ActiveDoc.ECopy(SciCode.ECopyAs.HtmlSpanClassCss); }
			
			[Command("Copy HTML <span class>")]
			public static void Copy_HTML_span_class() { Panels.Editor.ActiveDoc.ECopy(SciCode.ECopyAs.HtmlSpanClass); }
			
			[Command]
			public static void Copy_markdown() { Panels.Editor.ActiveDoc.ECopy(SciCode.ECopyAs.Markdown); }
			
			[Command]
			public static void Copy_without_screenshots() { Panels.Editor.ActiveDoc.ECopy(SciCode.ECopyAs.TextWithoutScreenshots); }
		}
		
		[Command("Undo, redo")]
		public static class UndoRedo {
			[Command(keys = "Ctrl+Z", image = iconUndo)]
			public static void Undo() { SciUndo.OfWorkspace.UndoRedo(false); }
			
			[Command(keys = "Ctrl+Y", image = "*Ionicons.RedoiOS" + brown)]
			public static void Redo() { SciUndo.OfWorkspace.UndoRedo(true); }
			
			[Command(separator = true)]
			public static void Undo_in_files() { SciUndo.OfWorkspace.UndoRedoMultiFileReplace(false); }
			
			[Command]
			public static void Redo_in_files() { SciUndo.OfWorkspace.UndoRedoMultiFileReplace(true); }
		}
		
		[Command("Find", separator = true)]
		public static class Find {
			[Command("Find text", keys = "Ctrl+F", image = "*Material.FindReplace" + blue)]
			public static void Find_text() { Panels.Find.CtrlF(Panels.Editor.ActiveDoc); }
			
			[Command("...", keys = "Ctrl+T", image = "*FontAwesome.SearchLocationSolid" + blue)]
			public static void Find_symbol() { CiFindGo.ShowSingle(); }
			
			[Command(keys = "Shift+F12", image = "*Codicons.References" + blue, separator = true)]
			public static void Find_references() { CiFind.FindReferencesOrImplementations(false); }
			
			[Command(image = "*Material.InformationVariant" + blue)]
			public static void Find_implementations() { CiFind.FindReferencesOrImplementations(true); }
			
			[Command(keys = "F12", image = "*RemixIcon.WalkFill" + blue, separator = true)]
			public static void Go_to_definition() { CiGoTo.GoToDefinition(); }
			
			[Command]
			public static void Go_to_base() { CiGoTo.GoToBase(); }
			
			[Command(keys = "F2", image = "*PicolIcons.Edit" + blue, separator = true)]
			public static void Rename_symbol() { CiFind.RenameSymbol(); }
		}
		
		[Command]
		public static class Intellisense {
			[Command(keysText = "Ctrl+Space", image = "*FontAwesome.ListUlSolid" + blue)]
			public static void Autocompletion_list() { CodeInfo.ShowCompletionList(); }
			
			[Command(keysText = "Ctrl+Shift+Space", image = "*RemixIcon.ParenthesesLine" + blue)]
			public static void Parameter_info() { CodeInfo.ShowSignature(); }
		}
		
		[Command(separator = true)]
		public static class Document {
			[Command(image = "*Material.CommentEditOutline" + brown)]
			public static void Add_file_description() { InsertCode.AddFileDescription(); }
			
			[Command(image = "*Codicons.SymbolClass" + brown)]
			public static void Add_class_Program() { InsertCode.AddClassProgram(); }
			
			[Command(image = "*PixelartIcons.AlignLeft" + brown, separator = true)]
			public static void Format_document() { ModifyCode.Format(false); }
			
			//CONSIDER: script.setup.
		}
		
		[Command]
		public static class Selection {
			[Command(keys = "Ctrl+/", image = "*BoxIcons.RegularCommentAdd" + brown)]
			public static void Comment() { ModifyCode.CommentLines(true); }
			
			[Command(keys = "Ctrl+\\", image = "*BoxIcons.RegularCommentMinus" + brown)]
			public static void Uncomment() { ModifyCode.CommentLines(false); }
			
			[Command(keys = "Ctrl+Shift+/", image = "*BoxIcons.RegularCommentAdd" + brown)]
			public static void Toggle_comment() { ModifyCode.CommentLines(null); }
			
			[Command(keysText = "R-click margin", keys = "Ctrl+Alt+/", image = "*BoxIcons.RegularCommentAdd" + brown)]
			public static void Toggle_line_comment() { ModifyCode.CommentLines(null, notSlashStar: true); }
			
			[Command(keysText = "Tab", image = "*Material.FormatIndentIncrease" + brown)]
			public static void Indent() { Panels.Editor.ActiveDoc.Call(Sci.SCI_TAB); }
			//SHOULDDO: now does not indent empty lines if was no indentation.
			
			[Command(keysText = "Shift+Tab", image = "*Material.FormatIndentDecrease" + brown)]
			public static void Unindent() { Panels.Editor.ActiveDoc.Call(Sci.SCI_BACKTAB); }
			
			//[Command(keysText = "Ctrl+D")]
			//public static void Duplicate() { Panels.Editor.ActiveDoc.Call(Sci.SCI_SELECTIONDUPLICATE); }
			
			[Command]
			public static void Format_selection() { ModifyCode.Format(true); }
			
			[Command]
			public static void Remove_screenshots() { Panels.Editor.ActiveDoc.EImageRemoveScreenshots(); }
			
			[Command(separator = true, keysText = "Ctrl+A")]
			public static void Select_all() { Panels.Editor.ActiveDoc.Call(Sci.SCI_SELECTALL); }
		}
		
		[Command]
		public static class Surround {
			[Command("for (repeat)", image = "*Typicons.ArrowLoop" + brown)]
			public static void Surround_for() { InsertCode.SurroundFor(); }
			
			[Command("try (catch exceptions)", image = "*MaterialDesign.ErrorOutline" + brown)]
			public static void Surround_try_catch() { InsertCode.SurroundTryCatch(); }
			
			[Command("...", separator = true)]
			public static void Documentation_tags() { run.itSafe("https://www.libreautomate.com/forum/showthread.php?tid=7461"); }
		}
		
		[Command]
		public static class Generate {
			[Command(keys = "Ctrl+Shift+D", image = "*Material.Lambda" + brown)]
			public static void Create_delegate() { GenerateCode.CreateDelegate(); }
			
			[Command(tooltip = "Implement members of interface or abstract base class")]
			public static void Implement_interface() { GenerateCode.ImplementInterfaceOrAbstractClass(); }
		}
		
		[Command(separator = true)]
		public static class View {
			[Command(checkable = true, keys = "Ctrl+W", image = "*Codicons.WordWrap" + green)]
			public static void Wrap_lines() { SciCode.EToggleView_call_from_menu_only_(SciCode.EView.Wrap); }
			
			[Command(checkable = true, image = "*Material.TooltipImageOutline" + green)]
			public static void Images_in_code() { SciCode.EToggleView_call_from_menu_only_(SciCode.EView.Images); }
			
			[Command(checkable = true, image = "*Codicons.Preview" + green)]
			public static void WPF_preview(MenuItem mi) { SciCode.WpfPreviewStartStop(mi); }
			
			[Command("Customize...", separator = true)]
			public static void Customize_edit_context_menu() { DCustomizeContextMenu.Dialog("Edit", "code editor"); }
		}
	}
	
	[Command(target = "Edit")]
	public static class Code {
		[Command(underlined: 'r', image = "*Material.RecordRec" + blue)]
		//[Command(underlined: 'r', image = "*BoxIcons.RegularVideoRecording" + blue)]
		public static void Input_recorder() { DInputRecorder.ShowRecorder(); }
		
		[Command("Find _window", image = "*BoxIcons.SolidWindowAlt" + blue)]
		public static void wnd() { Dwnd.Dialog(); }
		
		[Command("Find UI _element", image = "*Material.CheckBoxOutline" + blue)]
		public static void elm() { Delm.Dialog(); }
		
		[Command("Find _image", image = "*Material.ImageSearchOutline" + blue)]
		public static void uiimage() { Duiimage.Dialog(); }
		
		//[Command("Find _OCR text", image = "*Material.TextSearch" + blue)]
		[Command("Find _OCR text", image = "*Material.Ocr" + blue)]
		public static void ocr() { Docr.Dialog(); }
		
		[Command]
		public static void Get_files_in_folder() { DEnumDir.Dialog(); }
		
		[Command]
		public static void Quick_capturing() { QuickCapture.Info(); }
		
		[Command(keysText = "Ctrl+Space in string", image = "*Material.KeyboardOutline" + blue)]
		public static void Keys() { CiTools.CmdShowKeysWindow(); }
		
		[Command(underlined: 'x', keysText = "Ctrl+Space in string")]
		public static void Regex() { CiTools.CmdShowRegexWindow(); }
		
		[Command(underlined: 'A')]
		public static void Windows_API() { new DWinapi().Show(); }
	}
	
	[Command("T\x2009T", target = ""/*, tooltip = "Triggers and toolbars"*/)] //FUTURE: support tooltip for menu items
	public static class TT {
		[Command('k'/*, separator = true*/)]
		public static void Hotkey_triggers() { TriggersAndToolbars.Edit(@"Triggers\Hotkey triggers.cs"); }
		
		[Command]
		public static void Autotext_triggers() { TriggersAndToolbars.Edit(@"Triggers\Autotext triggers.cs"); }
		
		[Command]
		public static void Mouse_triggers() { TriggersAndToolbars.Edit(@"Triggers\Mouse triggers.cs"); }
		
		[Command]
		public static void Window_triggers() { TriggersAndToolbars.Edit(@"Triggers\Window triggers.cs"); }
		
		[Command("...", image = iconTrigger)]
		public static void New_trigger() { TriggersAndToolbars.NewTrigger(); }
		
		//rejected. It's in the quick capturing menu.
		//[Command("...")]
		//public static void Trigger_scope() { TriggersAndToolbars.TriggerScope(); }
		
		//[Command("...")]
		//public static void Active_triggers() {  }
		
		[Command]
		public static void Other_triggers() { TriggersAndToolbars.Edit(@"Triggers\Other triggers.cs"); Panels.Cookbook.OpenRecipe("Other triggers"); }
		
		[Command(separator = true)]
		public static void Toolbars() { TriggersAndToolbars.GoToToolbars(); }
		
		[Command("...", image = "*Material.ShapeRectanglePlus" + blue)]
		public static void New_toolbar() { TriggersAndToolbars.NewToolbar(); }
		
		[Command("...")]
		public static void Toolbar_trigger() { TriggersAndToolbars.SetToolbarTrigger(); }
		
		[Command]
		public static void Active_toolbars() { TriggersAndToolbars.ShowActiveTriggers(); }
		
		[Command(separator = true)]
		public static void Disable_triggers() { TriggersAndToolbars.DisableTriggers(null); }
		
		[Command]
		public static void Restart_TT_script() { TriggersAndToolbars.Restart(); }
		
		[Command(separator = true)]
		public static void Script_triggers() { DCommandline.ShowSingle(); }
	}
	
	[Command(target = "Edit")]
	public static class Run {
		[Command("Run", keys = "F5", image = "*Codicons.DebugStart #40B000|#4FD200")]
		public static void Run_script() { CompileRun.CompileAndRun(true, App.Model.CurrentFile, runFromEditor: true); }
		
		[Command(image = "*FontAwesome.StopCircleRegular" + black)]
		public static void End_task() {
			var f = App.Model.CurrentFile;
			if (f != null) {
				f = f.GetProjectMainOrThis();
				if (App.Tasks.EndTasksOf(f)) return;
			}
			var a = App.Tasks.Items;
			if (a.Count > 0) {
				var m = new popupMenu { RawText = true };
				m.Submenu("End task", m => {
					foreach (var t in a) m[t.f.DisplayName] = o => App.Tasks.EndTask(t);
				});
				m.Show();
			}
		}
		
		//[Command(image = "")]
		//public static void Pause() { }
		
		[Command(image = "*VaadinIcons.Compile" + blue)]
		public static void Compile() { CompileRun.CompileAndRun(false, App.Model.CurrentFile); }
		
		[Command("...", image = "*BoxIcons.RegularHistory" + blue)]
		public static void Recent() { RecentTT.Show(); }
		
		[Command(separator = true)]
		public static class Debugger {
			[Command("Insert script.debug (wait for debugger to attach)")]
			public static void Debug_attach() { InsertCode.Statements("script.debug();\r\nDebugger.Break();"); }
			
			[Command("Insert Debugger.Break (debugger step mode)")]
			public static void Debug_break() { InsertCode.Statements("Debugger.Break();"); }
			
			[Command("Insert Debugger.Launch (launch VS debugger)")]
			public static void Debug_launch() { InsertCode.Statements("Debugger.Launch();"); }
		}
	}
	
	[Command(target = "")]
	public static class Tools {
		[Command(image = "*PicolIcons.Settings" + green)]
		public static void Options() { DOptions.AaShow(); }
		
		[Command(image = iconIcons)]
		public static void Icons() { DIcons.ShowSingle(); }
		
		[Command(image = "*SimpleIcons.NuGet" + green)]
		public static void NuGet() { DNuget.ShowSingle(); }
		
		[Command(image = "*Codicons.SymbolSnippet" + green)]
		public static void Snippets() { DSnippets.ShowSingle(); }
		
		[Command]
		public static void Customize() { DCustomize.ShowSingle(); }
		
		[Command]
		public static void Portable() { DPortable.ShowSingle(); }
		
		[Command(separator = true, target = "Output")]
		public static class Output {
			[Command(keysText = "M-click")]
			public static void Clear() { Panels.Output.Clear(); }
			
			[Command("Copy", keysText = "Ctrl+C")]
			public static void Copy_output() { Panels.Output.Copy(); }
			
			[Command(keys = "Ctrl+F")]
			public static void Find_selected_text() { Panels.Output.Find(); }
			
			[Command]
			public static void History() { Panels.Output.History(); }
			
			[Command("Wrap lines", separator = true, checkable = true)]
			public static void Wrap_lines_in_output() { Panels.Output.WrapLines ^= true; }
			
			[Command("White space", checkable = true)]
			public static void White_space_in_output() { Panels.Output.WhiteSpace ^= true; }
			
			[Command(checkable = true)]
			public static void Topmost_when_floating() { Panels.Output.Topmost ^= true; }
		}
	}
	
	[Command(target = "")]
	public static class Help {
		[Command(image = "*FontAwesome.QuestionCircleRegular" + darkYellow)]
		public static void Program_help() { HelpUtil.AuHelp(""); }
		
		[Command(image = "*BoxIcons.RegularLibrary" + darkYellow)]
		public static void Library_help() { HelpUtil.AuHelp("api/"); }
		
		[Command(text = "C# help", image = "*Modern.LanguageCsharp" + darkYellow)]
		public static void CSharp_help() { run.itSafe("https://learn.microsoft.com/en-us/dotnet/csharp/"); }
		
		[Command(keys = "F1", image = "*Unicons.MapMarkerQuestion" + purple)]
		public static void Context_help() {
			var w = Api.GetFocus();
			if (w.ClassNameIs("HwndWrapper*")) {
				//var e = Keyboard.FocusedElement as FrameworkElement;
				
			} else if (Panels.Editor.ActiveDoc is SciCode doc && w == doc.AaWnd) {
				CiUtil.OpenSymbolEtcFromPosHelp();
			}
		}
		
		[Command(image = "*Material.Forum" + blue)]
		public static void Forum() { run.itSafe("https://www.libreautomate.com/forum/"); }
		
		[Command]
		public static void Email() { run.itSafe($"mailto:support@quickmacros.com?subject={App.AppNameShort} {App.Version}"); }
		
		[Command]
		public static void About() {
			print.it($@"<>---- {App.AppNameLong} ----
Version: {App.Version}
Download: <link>https://www.libreautomate.com/<>
Source code: <link>https://github.com/qgindi/LibreAutomate<>
Uses C# 11, <link https://dotnet.microsoft.com/download>.NET 6<>, <link https://github.com/dotnet/roslyn>Roslyn<>, <link https://www.scintilla.org/>Scintilla 5.1.5<>, <link https://www.pcre.org/>PCRE 10.42<>, <link https://www.sqlite.org/index.html>SQLite 3.42.0<>, <link https://github.com/MahApps/MahApps.Metro.IconPacks>MahApps.Metro.IconPacks<>, <link https://github.com/dotnet/docfx>DocFX<>, <link https://github.com/google/diff-match-patch>DiffMatchPatch<>, <link https://github.com/DmitryGaravsky/ILReader>ILReader<>, <link https://github.com/nemec/porter2-stemmer>Porter2Stemmer<>.
Folders: <link {folders.Workspace}>Workspace<>, <link {folders.ThisApp}>ThisApp<>, <link {folders.ThisAppDocuments}>ThisAppDocuments<>, <link {folders.ThisAppDataLocal}>ThisAppDataLocal<>, <link {folders.ThisAppTemp}>ThisAppTemp<>.
{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright}.
-----------------------");
		}
	}
	
#if TRACE
	//[Command(target = "", keys = "F11")] //no, dangerous, eg can accidentally press instead of F12
	[Command]
	public static void TEST() { Test.FromMenubar(); }
	
	[Command]
	public static void gc() {
		GC.Collect();
	}
#endif
	
	//Used by ScriptEditor.InvokeCommand/GetCommandState.
	//flags: 1 check, 2 uncheck, 4 activate window, 8 dontWait, 16 editorExtension.
	internal static int Invoke(string command, bool getState, int flags = 0) {
		var w = App.Hmain;
		bool loading = w.Is0;
		if (loading) {
			//this func can't work if the main window still not created
			App.ShowWindow();
			//let the script wait and retry. Some commands don't work until inited codeinfo etc. Until then w is disabled.
		} else {
			//few commands work or have sense when the window is invisible. Show it even when getState.
			if (0 != (flags & 4)) App.ShowWindow(); //show and activate
			else { //just show
				WndUtil.EnableActivate(); //enable activating if need, eg tool windows
				if (!App.Wmain.IsVisible) App.Wmain.Show();
			}
		}
		
		if (App.Commands.TryFind(command, out var c)) {
			if (loading || !w.IsEnabled()) {
				if (0 != (flags & 16)) return getState ? (int)ECommandState.Disabled : 0; //editorExtension, can't wait
				return -1; //let the script wait while disabled, even if getState
			}
			
			if (getState) {
				ECommandState r = c.CanExecute(null) ? 0 : ECommandState.Disabled;
				if (c.Checked) r |= ECommandState.Checked;
				return (int)r;
			} else if (c.CanExecute(null)) {
				if (0 != (flags & 8)) { //dontWait
					if (0 != (flags & 16)) { //editorExtension
						App.Dispatcher.InvokeAsync(_Invoke);
						return 1;
					}
					Api.ReplyMessage(1);
				}
				_Invoke();
				return 1;
				
				void _Invoke() {
					int check = flags & 3;
					if (check is 1 or 2) c.Checked = check == 1; else c.Execute(null);
				}
			}
		} else {
			var names = new StringBuilder();
			foreach (var v in Panels.Menu.Items) _Menu(v, 0);
			void _Menu(object o, int level) {
				if (o is MenuItem mi && mi.Command is KMenuCommands.Command c) {
					names.Append('\t', level).AppendLine(c.Name);
					foreach (var v in mi.Items) _Menu(v, level + 1);
				}
			}
			
			print.it(command.NE() ? "Commands:\r\n" + names : $"Unknown command '{command}'. Commands:\r\n{names}");
		}
		return 0;
	}
}
