using Au.Controls;
using Au.Tools;
using System.Windows;
using System.Windows.Controls;

static class Menus {
	public const string
		black = " #505050|#EEEEEE",
		blue = " #4080FF|#99CCFF",
		//darkBlue = " #5060FF|#7080FF",
		//lightBlue = " #B0C0FF|#D0E0FF",
		green = " #99BF00|#A7D000",
		green2 = " #40B000|#4FD200",
		brown = " #9F5300|#EEEEEE",
		purple = " #A040FF|#D595FF",
		darkYellow = " #EABB00",
		orange = " #FFA500",
		red = " #FF4040|#FF9595"
		;
	public const string
		iconBack = "*EvaIcons.ArrowBack" + black,
		iconTrigger = "*Codicons.SymbolEvent" + blue,
		iconIcons = "*FontAwesome.IconsSolid" + green,
		iconUndo = "*Ionicons.UndoiOS" + brown,
		iconPaste = "*Material.ContentPaste" + brown,
		//iconReferences = "*Codicons.References"
		iconReferences = "*Material.MapMarkerMultiple" + blue, //or MapMarkerMultipleOutline
		iconRegex = "*FileIcons.Regex" + blue
		;
	
	[Command(target = "Files")]
	public static class File {
		[Command(target = "", image = "*EvaIcons.FileAddOutline" + blue)]
		public static class New {
			static FileNode _New(string name) => App.Model.NewItem(name, beginRenaming: true);
			
			[Command('s', keys = "Ctrl+N", image = EdResources.c_iconScript, tooltip = "A script is a C# code file that can be executed like a program.")]
			public static void New_script() { _New("Script.cs"); }
			
			[Command('c', image = EdResources.c_iconClass, tooltip = "Class files contain C# classes/functions that can be used in other C# files.")]
			public static void New_class() { _New("Class.cs"); }
			
			[Command('t', image = "*FeatherIcons.FileText" + black, tooltip = "Text files contain any text except C# code.\nTo change the file type, edit the \"txt\" in the filename.")]
			public static void New_text_file() { _New("File.txt"); }
			
			[Command('f', image = EdResources.c_iconFolder)]
			public static void New_folder() { _New(null); }
		}
		
		[Command("Delete...", separator = true, keysText = "Delete", image = "*Modern.Delete" + black)]
		public static void Delete() { App.Model.DeleteSelected(); }
		
		[Command(keys = "F2", image = "*BoxIcons.RegularRename @15" + black)]
		public static void Rename() { App.Model.RenameSelected(); }
		
		[Command(image = "*RemixIcon.ChatSettingsLine" + green)]
		public static void Properties() { App.Model.Properties(); }
		
		[Command("Copy, paste")]
		public static class CopyPaste {
			[Command("Multi-select", checkable = true, image = "*Modern.ListTwo" + green, tooltip = "If checked:\n    - You can select multiple items with Ctrl or Shift.\n    - Double-click to open.")]
			public static void MultiSelect_files() { Panels.Files.TreeControl.SetMultiSelect(toggle: true); }
			
			[Command("Cu_t", separator = true, keysText = "Ctrl+X")]
			public static void Cut_file() { App.Model.CutCopySelected(true); }
			
			[Command("Copy", keysText = "Ctrl+C")]
			public static void Copy_file() { App.Model.CutCopySelected(false); }
			
			[Command("Paste", keysText = "Ctrl+V")]
			public static void Paste_file() { App.Model.Paste(); }
			
			[Command("Cancel Cut/Copy", keysText = "Esc")]
			public static void CancelCutCopy_file() { App.Model.Uncut(); }
			
			[Command('r', separator = true, tooltip = @"Path in this workspace (in the Files list), like ""\Folder\File.cs"".")]
			public static void Copy_relative_path() { App.Model.SelectedCopyPath(false); }
			
			[Command('f', tooltip = "Full path of the file.")]
			public static void Copy_full_path() { App.Model.SelectedCopyPath(true); }
		}
		
		[Command("Open, close")]
		public static class OpenClose {
			[Command(keysText = "Enter")]
			public static void Open() { App.Model.OpenSelected(1); }
			
			[Command(image = "*MaterialDesign.OpenInNew" + black)]
			public static void Open_in_default_app() { App.Model.OpenSelected(3); }
			
			[Command(image = "*Material.FolderMarker" + darkYellow)]
			public static void Select_in_Explorer() { App.Model.OpenSelected(4); }
			
			[Command(separator = true, target = "", keys = "Ctrl+F4", keysText = "M-click")]
			public static void Close() { App.Model.CloseEtc(FilesModel.ECloseCmd.CloseSelectedOrCurrent); }
			
			[Command(target = "")]
			public static void Close_all() { App.Model.CloseEtc(FilesModel.ECloseCmd.CloseAll); }
			
			[Command(separator = true, target = "", image = "*Codicons.CollapseAll" + black)]
			public static void Collapse_all_folders() { App.Model.CloseEtc(FilesModel.ECloseCmd.CollapseAllFolders); }
			
			[Command(target = "", image = "*Codicons.CollapseAll" + black)]
			public static void Collapse_inactive_folders() { App.Model.CloseEtc(FilesModel.ECloseCmd.CollapseInactiveFolders); }
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
			[Command(image = "*Material.Reload" + black)]
			public static void Reload_this_workspace() { FilesModel.LoadWorkspace(App.Model.WorkspaceDirectory); }
			
			[Command("...", separator = true)]
			public static void Open_workspace() { FilesModel.OpenWorkspaceUI(); }
			
			[Command("...")]
			public static void New_workspace() { FilesModel.NewWorkspaceUI(); }
			
			[Command("...", separator = true)]
			public static void Repair_workspace() { RepairWorkspace.Repair(false); }
			
			[Command("...")]
			public static void Repair_this_folder() { RepairWorkspace.Repair(true); }
			
			[Command(separator = true, keys = "Ctrl+S", image = "*BoxIcons.RegularSave" + black, tooltip = "Save all changes now (don't wait for auto-save). Editor text, files, settings etc.")]
			public static void Save_now() { App.Model?.Save.AllNowIfNeed(); }
		}
		
		[Command(target = "", image = "*Material.Git" + blue)]
		public static class Git {
			[Command(image = "*MaterialDesign.InfoOutline" + blue)]
			public static void Git_status() { global::Git.Status(); }
			
			[Command("Commit", image = "*RemixIcon.GitCommitLine" + blue)]
			public static void Git_commit() { global::Git.Commit(); }
			
			[Command("Push to GitHub", image = "*Unicons.CloudUpload" + blue)]
			public static void Git_push() { global::Git.Push(); }
			
			[Command("Pull from GitHub", image = "*Unicons.CloudDownload" + blue)]
			public static void Git_pull() { global::Git.Pull(); }
			
			[Command("GitHub Desktop", image = "*Codicons.Github" + purple, separator = true)]
			public static void Git_gui() { global::Git.RunGui(); }
			
			[Command("Cmd", image = "*Material.Console" + black)]
			public static void Git_cmd() { global::Git.RunCmd(); }
			
			[Command("Workspace folder", image = "*Material.Folder" + darkYellow)]
			public static void Git_workspace_folder() { global::Git.WorkspaceFolder(); }
			
			[Command("Reload workspace", image = "*Material.Reload" + black)]
			public static void Git_reload_workspace() { global::Git.ReloadWorkspace(); }
			
			[Command("GitHub sign out", separator = true)]
			public static void Git_sign_out() { global::Git.Signout(); }
			
			[Command("Maintenance...")]
			public static void Git_maintenance() { global::Git.Maintenance(); }
			
			[Command("...")]
			public static void Git_setup() { global::Git.Setup(); }
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
			
			[Command(separator = true, tooltip = "Undo a multi-file operation, such as renaming a symbol in multiple files.")]
			public static void Undo_in_files() { SciUndo.OfWorkspace.UndoRedoMultiFileReplace(false); }
			
			[Command(tooltip = "Redo a multi-file operation.")]
			public static void Redo_in_files() { SciUndo.OfWorkspace.UndoRedoMultiFileReplace(true); }
		}
		
		[Command("Find", separator = true)]
		public static class Find {
			[Command("Find text", keys = "Ctrl+F", image = "*Material.FindReplace" + blue)]
			public static void Find_text() { Panels.Find.CtrlF(Panels.Editor.ActiveDoc); }
			
			[Command("...", keys = "Ctrl+T", image = "*FontAwesome.SearchLocationSolid" + blue)]
			public static void Find_symbol() { CiFindGo.ShowSingle(); }
			
			[Command(keys = "Shift+F12", image = iconReferences, separator = true)]
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
		
		[Command]
		public static class Navigate {
			[Command(image = "*Material.Bookmark @16" + darkYellow)]
			public static void Toggle_bookmark() { Panels.Bookmarks.ToggleBookmark(); }
			
			[Command(image = "*JamIcons.ArrowSquareUp" + black, keys = "Alt+Up", noIndirectDisable = true)]
			public static void Previous_bookmark() { Panels.Bookmarks.NextBookmark(true); }
			
			[Command(image = "*JamIcons.ArrowSquareDown" + black, keys = "Alt+Down", noIndirectDisable = true)]
			public static void Next_bookmark() { Panels.Bookmarks.NextBookmark(false); }
			
			[Command(keys = "Alt+Left", target = "", image = iconBack, separator = true, noIndirectDisable = true)]
			public static void Go_back() { App.Model.EditGoBack.GoBack(); }
			
			[Command(keys = "Alt+Right", target = "", image = "*EvaIcons.ArrowForward" + black, noIndirectDisable = true)]
			public static void Go_forward() { App.Model.EditGoBack.GoForward(); }
			
			[Command(separator = true, target = "", keys = "Ctrl+Tab", noIndirectDisable = true)]
			public static void Previous_document() { var a = App.Model.OpenFiles; if (a.Count > 1) App.Model.SetCurrentFile(a[1]); }
		}
		
		[Command(separator = true)]
		public static class Selection {
			[Command(keys = "Ctrl+/", image = "*BoxIcons.RegularCommentAdd" + brown)]
			public static void Comment_selection() { ModifyCode.Comment(true); }
			
			[Command(keys = "Ctrl+\\", image = "*BoxIcons.RegularCommentMinus" + brown)]
			public static void Uncomment_selection() { ModifyCode.Comment(false); }
			
			[Command(keys = "Ctrl+Shift+/", image = "*BoxIcons.RegularComment" + brown)]
			public static void Toggle_comment() { ModifyCode.Comment(null); }
			
			[Command(keysText = "R-click margin", keys = "Ctrl+Alt+/", image = "*BoxIcons.RegularComment" + brown)]
			public static void Toggle_line_comment() { ModifyCode.Comment(null, notSlashStar: true); }
			
			[Command("...", image = "*RemixIcon.BracesLine" + brown, separator = true)]
			public static void Surround() { CiSnippets.Surround(); }
			
			[Command("...")]
			public static void Documentation_tags() { run.itSafe("https://www.libreautomate.com/forum/showthread.php?tid=7461"); }
			
			[Command(separator = true, keysText = "Ctrl+A")]
			public static void Select_all() { Panels.Editor.ActiveDoc.Call(Sci.SCI_SELECTALL); }
		}
		
		[Command]
		public static class Tidy_code {
			[Command(image = "*PixelartIcons.AlignLeft" + brown)]
			public static void Format_document() { ModifyCode.Format(false); }
			
			[Command(image = "*PixelartIcons.AlignLeft" + brown)]
			public static void Format_selection() { ModifyCode.Format(true); }
			
			[Command]
			public static void Disable_format_selection() { InsertCode.SurroundPragmaWarningFormat(); }
			
			[Command("Indent selected lines", keysText = "Tab", image = "*Material.FormatIndentIncrease" + brown, separator = true)]
			public static void Indent() { Panels.Editor.ActiveDoc.Call(Sci.SCI_TAB); }
			//TODO3: now does not indent empty lines if was no indent.
			
			[Command("Unindent selected lines", keysText = "Shift+Tab", image = "*Material.FormatIndentDecrease" + brown)]
			public static void Unindent() { Panels.Editor.ActiveDoc.Call(Sci.SCI_BACKTAB); }
			
			[Command("Deduplicate wnd.find", separator = true, image = "*Material.Broom" + brown, tooltip = "Remove duplicate `wnd.find` statements in all or selected text.")]
			public static void Deduplicate_wnd_find() { ModifyCode.CleanupWndFind(); }
			
			[Command(tooltip = "Remove screenshots in all or selected text.\nScreenshot images are saved in text as hidden comments.")]
			public static void Remove_screenshots() { Panels.Editor.ActiveDoc.EImageRemoveScreenshots(); }
		}
		
		[Command]
		public static class Generate {
			[Command(keys = "Ctrl+Shift+D", image = "*Material.Lambda" + brown)]
			public static void Create_delegate() { GenerateCode.CreateDelegate(); }
			
			[Command("Implement interface/abstract", tooltip = "Implement members of interface or abstract base class.")]
			public static void Implement_interface() { GenerateCode.ImplementInterfaceOrAbstractClass(); }
			
			[Command]
			public static void Create_event_handlers() { GenerateCode.CreateEventHandlers(); }
			
			[Command]
			public static void Create_overrides() { GenerateCode.CreateOverrides(); }
		}
		
		[Command(separator = true)]
		public static class View {
			[Command(checkable = true, keys = "Ctrl+W", image = "*Codicons.WordWrap" + green)]
			public static void Wrap_lines() { SciCode.EToggleView_call_from_menu_only_(SciCode.EView.Wrap); }
			
			[Command(checkable = true, image = "*Material.TooltipImageOutline" + green, tooltip = "If checked, displays images (file icons, screenshots, \"image:\", \"*icon\").\nAlso captures screenshots when recording etc.")]
			public static void Images_in_code() { SciCode.EToggleView_call_from_menu_only_(SciCode.EView.Images); }
			
			[Command(checkable = true, image = "*Codicons.Preview" + green)]
			public static void WPF_preview(MenuItem mi) { SciCode.WpfPreviewStartStop(mi); }
			
			[Command("Customize...", separator = true)]
			public static void Customize_edit_context_menu() { DCustomizeContextMenu.Dialog("Edit", "code editor"); }
		}
	}
	
	[Command(target = "Edit", tooltip = "Tools for creating code")]
	public static class Code {
		[Command(underlined: 'r', image = "*Material.RecordRec" + blue)]
		public static void Input_recorder() { DInputRecorder.ShowRecorder(); }
		
		[Command("Find _window", image = "*BoxIcons.SolidWindowAlt" + blue)]
		public static void wnd() { Dwnd.Dialog(); }
		
		[Command("Find UI _element", image = "*Material.CheckBoxOutline @15" + blue)]
		public static void elm() { Delm.Dialog(); }
		
		[Command("Find _image", image = "*Material.ImageSearchOutline" + blue)]
		public static void uiimage() { Duiimage.Dialog(); }
		
		[Command("Find _OCR text", image = "*Material.Ocr" + blue)]
		public static void ocr() { Docr.Dialog(); }
		
		[Command(image = "*Material.FolderOutline" + blue)]
		public static void Get_files_in_folder() { DEnumDir.Dialog(); }
		
		[Command(keysText = "Ctrl+Space in string", image = "*Material.KeyboardOutline" + blue)]
		public static void Keys() { CiTools.CmdShowKeysWindow(); }
		
		[Command(underlined: 'x', image = iconRegex, keysText = "Ctrl+Space in string")]
		public static void Regex() { CiTools.CmdShowRegexWindow(); }
		
		[Command(image = "*MaterialDesign.ColorLens" + blue)]
		public static void Color() { KColorPicker.ColorTool(s => { clipboard.text = s; print.it($"Clipboard: {s}"); }, App.Wmain, modal: false, add0xRgbButton: true, addBgrButton: true); }
		
		[Command(underlined: 'A', image = "*Material.Api" + blue)]
		public static void Windows_API() { new DWinapi().Show(); }
		
		[Command(separator = true)]
		public static class Simple {
			[Command]
			public static void Add_function_Main() { InsertCode.AddClassProgram(); }
			
			[Command("Create GUID")]
			public static void Create_GUID() { var s = Guid.NewGuid().ToString(); clipboard.text = s; print.it($"Clipboard: {s}"); }
			
			[Command]
			public static void Quick_capturing_info() { QuickCapture.Info(); }
		}
	}
	
	[Command("T\x2009T", target = "", tooltip = "Triggers and toolbars")]
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
		[Command(image = "*VaadinIcons.Compile" + blue)]
		public static void Compile() { CompileRun.CompileAndRun(false, App.Model.CurrentFile); }
		
		[Command("Run", image = "*Codicons.DebugStart" + green2)]
		public static void Run_script() { CompileRun.CompileAndRun(true, App.Model.CurrentFile, runFromEditor: true); }
		
		[Command(image = "*Material.SquareOutline @14" + black)]
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
				m.Show(owner: App.Hmain);
			}
		}
		
		[Command("...", image = "*BoxIcons.RegularHistory" + blue)]
		public static void Recent() { RecentTT.Show(); }
		
		[Command(image = "*Material.Bug" + green2/*, separator = true*/)]
		public static void Debug_run() { Panels.Debug.Start(); }
		
		[Command("...", image = "*Entypo.Publish" + blue, separator = true)]
		public static void Publish() { new XPublish().Publish(); }
	}
	
	[Command(target = "")]
	public static class Tools {
		[Command(image = "*PicolIcons.Settings" + green)]
		public static void Options() { DOptions.AaShow(); }
		
		[Command(image = iconIcons)]
		public static void Icons() { DIcons.ShowSingle(); }
		
		[Command(tooltip = "Clear icon caches")]
		public static void Update_icons() { IconImageCache.ClearAll(); }
		
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
			[Command("Clear", keysText = "M-click")]
			public static void Output_clear() { Panels.Output.Clear(); }
			
			[Command("Copy", keysText = "Ctrl+C")]
			public static void Output_copy() { Panels.Output.Copy(); }
			
			[Command("History")]
			public static void Output_history() { Panels.Output.History(); }
			
			[Command("Wrap lines", separator = true, checkable = true)]
			public static void Output_wrap_lines() { Panels.Output.WrapLines ^= true; }
			
			[Command("White space", checkable = true)]
			public static void Output_white_space() { Panels.Output.WhiteSpace ^= true; }
		}
	}
	
	[Command(target = "")]
	public static class Help {
		[Command(image = "*Modern.Home" + blue)]
		public static void Website() { HelpUtil.AuHelp(""); }
		
		[Command(image = "*BoxIcons.RegularLibrary" + darkYellow)]
		public static void Library_help() { HelpUtil.AuHelp("api/"); }
		
		[Command(text = "C# help", image = "*Modern.LanguageCsharp" + darkYellow)]
		public static void CSharp_help() { run.itSafe("https://learn.microsoft.com/en-us/dotnet/csharp/"); }
		
		[Command(keys = "F1", image = "*Unicons.MapMarkerQuestion" + darkYellow)]
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
		public static void Email() { run.itSafe($"mailto:support@quickmacros.com?subject={App.AppNameShort} {Au_.Version}"); }
		
		[Command]
		public static void Donate() { run.itSafe("https://github.com/sponsors/qgindi"); }
		
		[Command]
		public static void About() {
			print.it($@"<>---- {App.AppNameLong} ----
Version: {Au_.Version}
Download: <link>https://www.libreautomate.com/<>
Source code: <link>https://github.com/qgindi/LibreAutomate<>
Uses C# 12, <link https://dotnet.microsoft.com/download>.NET {Environment.Version}<>, <link https://github.com/dotnet/roslyn>Roslyn<>, <link https://www.scintilla.org/>Scintilla 5.1.5<>, <link https://www.pcre.org/>PCRE 10.42<>, <link https://www.sqlite.org/index.html>SQLite 3.42.0<>, <link https://github.com/MahApps/MahApps.Metro.IconPacks>MahApps.Metro.IconPacks 4.11<>, <link https://github.com/dotnet/docfx>DocFX<>, <link https://github.com/Samsung/netcoredbg>Samsung/netcoredbg<>, <link https://github.com/google/diff-match-patch>DiffMatchPatch<>, <link https://github.com/DmitryGaravsky/ILReader>ILReader<>, <link https://github.com/nemec/porter2-stemmer>Porter2Stemmer<>, <link https://github.com/xoofx/markdig>Markdig<>.
Folders: <link {folders.Workspace}>Workspace<>, <link {folders.ThisApp}>ThisApp<>, <link {folders.ThisAppDocuments}>ThisAppDocuments<>, <link {folders.ThisAppDataLocal}>ThisAppDataLocal<>, <link {folders.ThisAppTemp}>ThisAppTemp<>.
{typeof(App).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright}.
-----------------------");
		}
	}
	
#if DEBUG
	//[Command(target = "", keys = "F11")] //no, dangerous, eg can accidentally press instead of F12
	[Command]
	public static void TEST() { Test.FromMenubar(); }
	
	[Command]
	public static void gc() {
		GC.Collect();
		GC.WaitForPendingFinalizers(); //GC.Collect does not free much memory without this
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
