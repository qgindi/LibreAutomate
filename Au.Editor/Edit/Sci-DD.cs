using static Au.Controls.Sci;
using System.Windows;
using Au.Tools;

partial class SciCode {
	void _InitDragDrop() {
		Api.RevokeDragDrop(AaWnd); //of Scintilla
		Api.RegisterDragDrop(AaWnd, _ddTarget = new _DragDrop(this));
		//Scintilla will call RevokeDragDrop when destroying window
	}
	
	_DragDrop _ddTarget;
	
	class _DragDrop : Api.IDropTarget {
		readonly SciCode _sci;
		DDData _data;
		bool _canDrop, _justText;
		
		public _DragDrop(SciCode sci) { _sci = sci; }
		
		void Api.IDropTarget.DragEnter(System.Runtime.InteropServices.ComTypes.IDataObject d, int grfKeyState, POINT pt, ref int effect) {
			_data = default;
			_justText = false;
			if (_canDrop = _data.GetData(d, getFileNodes: true)) {
				_justText = _data.text != null && _data.linkName == null;
			}
			effect = _GetEffect(effect, grfKeyState);
			CodeInfo.Cancel();
		}
		
		unsafe void Api.IDropTarget.DragOver(int grfKeyState, POINT p, ref int effect) {
			if ((effect = _GetEffect(effect, grfKeyState)) != 0) {
				_GetDropPos(ref p, out _);
				var z = new Sci_DragDropData { x = p.x, y = p.y };
				_sci.Call(SCI_DRAGDROP, 1, &z);
			}
		}
		
		void Api.IDropTarget.DragLeave() {
			if (_canDrop) {
				_canDrop = false;
				_sci.Call(SCI_DRAGDROP, 3);
			}
		}
		
		void Api.IDropTarget.Drop(System.Runtime.InteropServices.ComTypes.IDataObject d, int grfKeyState, POINT pt, ref int effect) {
			if ((effect = _GetEffect(effect, grfKeyState)) != 0) _Drop(pt, effect);
			_canDrop = false;
		}
		
		int _GetEffect(int effect, int grfKeyState) {
			if (!_canDrop) return 0;
			if (_sci.aaaIsReadonly) return 0;
			DragDropEffects r, ae = (DragDropEffects)effect;
			var ks = (DragDropKeyStates)grfKeyState;
			switch (ks & (DragDropKeyStates.ShiftKey | DragDropKeyStates.ControlKey | DragDropKeyStates.AltKey)) {
			case 0: r = DragDropEffects.Move; break;
			case DragDropKeyStates.ControlKey: r = DragDropEffects.Copy; break;
			default: return 0;
			}
			if (_data.text != null) r = 0 != (ae & r) ? r : ae;
			else if (0 != (ae & DragDropEffects.Link)) r = DragDropEffects.Link;
			else if (0 != (ae & DragDropEffects.Copy)) r = DragDropEffects.Copy;
			else r = ae;
			return (int)r;
		}
		
		void _GetDropPos(ref POINT p, out int pos) {
			_sci.AaWnd.MapScreenToClient(ref p);
			if (!_justText) { //if files etc, drop as lines, not anywhere
				pos = _sci.Call(SCI_POSITIONFROMPOINT, p.x, p.y);
				pos = _sci.aaaLineStartFromPos(false, pos);
				p.x = _sci.Call(SCI_POINTXFROMPOSITION, 0, pos);
				p.y = _sci.Call(SCI_POINTYFROMPOSITION, 0, pos);
			} else pos = 0;
		}
		
		unsafe void _Drop(POINT xy, int effect) {
			_GetDropPos(ref xy, out int pos8);
			var z = new Sci_DragDropData { x = xy.x, y = xy.y };
			string s = null;
			var b = new StringBuilder();
			bool isCodeFile = _sci.EFile.IsCodeFile;
			
			if (_justText) {
				s = _data.text;
			} else {
				_sci.Call(SCI_DRAGDROP, 2, &z); //just hides the drag indicator and sets caret position
				
				if (_data.scripts) {
					int what = 0;
					if (isCodeFile) {
						var sm = "1 string s = name;|2 string s = path;";
						if (Panels.Files.TreeControl.DragDropFiles.All(o => o.IsCodeFile)) {
							sm += "|3 script.run(path);|4 t[name] = o => script.run(path);";
							if (Panels.Files.TreeControl.DragDropFiles.All(o => o.IsClass)) {
								sm += Panels.Files.TreeControl.DragDropFiles.All(o => o.GetClassFileRole() == FNClassFileRole.Library) ? "|101 /* pr path; */" : "|100 /* c path; */";
							}
						} else {
							sm += "|102 /* resource path; */";
							sm += "|103 /* file path; */";
						}
						what = popupMenu.showSimple(sm);
						if (what == 0) return;
					}
					var a = Panels.Files.TreeControl.DragDropFiles;
					if (a != null) {
						if (what >= 100) { //meta comment
							MetaCommentsParser m = new(_sci.EFile) { };
							var list = what switch { 100 => m.c, 101 => m.pr, 102 => m.resource, _ => m.file };
							foreach (var v in a) list.Add(v.ItemPath);
							m.Apply();
							return;
						}
						for (int i = 0; i < a.Length; i++)
							_AppendScriptOrLink(what, a[i].ItemPath, a[i].Name, i + 1, a[i]);
					}
				} else if (_data.files != null || _data.shell != null) {
					string[] files = null, names = null;
					files = _data.shell != null ? _GetShell(_data.shell, out names) : _data.files;
					if (isCodeFile) {
						var what = TUtil.PathInfo.InsertCodeMenu(files, _sci.AaWnd);
						if (what == 0) return;
						for (int i = 0; i < files.Length; i++) {
							if (i > 0) b.AppendLine();
							var k = new TUtil.PathInfo(files[i], names?[i]);
							b.Append(k.FormatCode(what, i + 1));
						}
					} else {
						for (int i = 0; i < files.Length; i++) {
							if (i > 0) b.AppendLine();
							b.Append(files[i]);
						}
					}
				} else if (_data.linkName != null) {
					int what = 0;
					if (isCodeFile) {
						what = popupMenu.showSimple("11 string s = URL;|12 run.it(URL);|13 t[name] = o => run.it(URL);");
						if (what == 0) return;
					}
					_AppendScriptOrLink(what, _data.text, _GetLinkName(_data.linkName));
				}
				s = b.ToString();
			}
			
			if (!s.NE()) {
				if (_justText) { //a simple drag-drop inside scintilla or text-only from outside
					var s8 = Encoding.UTF8.GetBytes(s);
					fixed (byte* p8 = s8) {
						z.text = p8;
						z.len = s8.Length;
						if (0 == ((DragDropEffects)effect & DragDropEffects.Move)) z.copy = 1;
						CodeInfo.Pasting(_sci);
						_sci.Call(SCI_DRAGDROP, 2, &z);
					}
				} else { //file, script or URL
					if (isCodeFile) InsertCode.Statements(s, ICSFlags.NoFocus);
					else if (!_sci.aaaIsReadonly) _sci.aaaReplaceSel(s + "\r\n");
					else print.it(s);
				}
				if (!_sci.IsFocused && _sci.AaWnd.Window.IsActive) { //note: don't activate window; let the drag source do it, eg Explorer activates on drag-enter.
					_sci._noModelEnsureCurrentSelected = true; //don't scroll treeview to currentfile
					_sci.Focus();
					_sci._noModelEnsureCurrentSelected = false;
				}
			} else {
				_sci.Call(SCI_DRAGDROP, 3);
			}
			
			void _AppendScriptOrLink(int what, string path, string name, int index = 0, FileNode fn = null) {
				if (b.Length > 0) b.AppendLine();
				if (what == 0) {
					b.Append(path);
				} else {
					if (what == 4) name = name.RemoveSuffix(".cs");
					name = name.Escape();
					
					if (what is 4 or 13) {
						var t = InsertCodeUtil.GetNearestLocalVariableOfType("Au.toolbar", "Au.popupMenu");
						b.Append($"{t?.Name ?? "t"}[\"{name}\"] = o => ");
					} else if (what is 1 or 2 or 11) {
						b.Append($"string s{index} = ");
					}
					
					b.Append(what switch {
						1 => $"\"{name}\";",
						2 or 11 => $"@\"{path}\";",
						3 or 4 => $"script.run(@\"{path}\");",
						_ => $"run.it(@\"{path}\");"
					});
				}
			}
			
			static unsafe string[] _GetShell(byte[] b, out string[] names) {
				fixed (byte* p = b) {
					int* pi = (int*)p;
					int n = *pi++;
					var paths = new string[n];
					names = new string[n];
					if (n > 0) {
						IntPtr pidlFolder = (IntPtr)(p + *pi++);
						for (int i = 0; i < n; i++) {
							using var pidl = new Pidl(pidlFolder, (IntPtr)(p + pi[i]));
							//if from Winstore apps folder, get "shell:..."
							if (folders.shell.pidlApps_Win8.ValueEquals(pidlFolder)) {
								//var s11 = pidl.ToShellString(SIGDN.DESKTOPABSOLUTEPARSING); //same as PARENTRELATIVEPARSING, not full
								var s1 = pidl.ToShellString(SIGDN.PARENTRELATIVEPARSING);
								if (s1 != null && !s1.Starts('{') && s1.Contains('!')) paths[i] = @"shell:AppsFolder\" + s1;
								//if (s1 != null) {
								//	if (s1.Starts('{')) paths[i] = s1; //like "{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\charmap.exe". run.it fails etc. Cannot get path. The ITEMIDLIST is insanely long.
								//	else if (s1.Contains('!')) paths[i] = @"shell:AppsFolder\" + s1;
								//}
							}
							paths[i] ??= pidl.ToString();
							names[i] = pidl.ToShellString(SIGDN.NORMALDISPLAY);
						}
					}
					return paths;
				}
			}
			
			static unsafe string _GetLinkName(byte[] b) {
				if (b.Length != 596) return null; //sizeof(FILEGROUPDESCRIPTORW) with 1 FILEDESCRIPTORW
				fixed (byte* p = b) { //FILEGROUPDESCRIPTORW
					if (*(int*)p != 1) return null; //count of FILEDESCRIPTORW
					var s = new string((char*)(p + 76));
					if (!s.Ends(".url", true)) return null;
					return s[..^4];
				}
			}
		}
	}
}
