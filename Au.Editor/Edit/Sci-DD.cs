using static Au.Controls.Sci;
using System.Windows;
using Au.Tools;

partial class SciCode {
	void _InitDragDrop() {
		Api.RevokeDragDrop(Hwnd); //of Scintilla
		Api.RegisterDragDrop(Hwnd, _ddTarget = new _DragDrop(this));
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
			if (_sci.zIsReadonly) return 0;
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
			_sci.Hwnd.MapScreenToClient(ref p);
			if (!_justText) { //if files etc, drop as lines, not anywhere
				pos = _sci.Call(SCI_POSITIONFROMPOINT, p.x, p.y);
				pos = _sci.zLineStartFromPos(false, pos);
				p.x = _sci.Call(SCI_POINTXFROMPOSITION, 0, pos);
				p.y = _sci.Call(SCI_POINTYFROMPOSITION, 0, pos);
			} else pos = 0;
		}

		unsafe void _Drop(POINT xy, int effect) {
			_GetDropPos(ref xy, out int pos8);
			var z = new Sci_DragDropData { x = xy.x, y = xy.y };
			string s = null;
			var b = new StringBuilder();
			int what = 0, index = 0;

			if (_justText) {
				s = _data.text;
			} else {
				_sci.Call(SCI_DRAGDROP, 2, &z); //just hides the drag indicator and sets caret position

				if (_sci._fn.IsCodeFile) {
					string mi = _data.scripts
						? "1 string s = name;|2 string s = path;|3 script.run(path);|4 t[name] = o => script.run(path);"
						: "11 string s = path;|12 run.it(path);|13 t[name] = o => run.it(path);";
					what = popupMenu.showSimple(mi);
					if (what == 0) return;
				}

				if (_data.scripts) {
					var a = Panels.Files.TreeControl.DragDropFiles;
					if (a != null) {
						foreach (var f in a) {
							_AppendScriptOrLink(f.ItemPath, f.Name, f);
						}
					}
				} else if (_data.files != null) {
					foreach (var path in _data.files) _AppendFileOrShell(path);
				} else if (_data.shell != null) {
					_GetShell(_data.shell, out var shells, out var names);
					if (shells != null) {
						for (int i = 0; i < shells.Length; i++) _AppendFileOrShell(shells[i], names[i]);
					}
				} else if (_data.linkName != null) {
					_AppendScriptOrLink(_data.text, _GetLinkName(_data.linkName));
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
					InsertCode.Statements(s, ICSFlags.NoFocus);
				}
				if (!_sci.IsFocused && _sci.Hwnd.Window.IsActive) { //note: don't activate window; let the drag source do it, eg Explorer activates on drag-enter.
					_sci._noModelEnsureCurrentSelected = true; //don't scroll treeview to currentfile
					_sci.Focus();
					_sci._noModelEnsureCurrentSelected = false;
				}
			} else {
				_sci.Call(SCI_DRAGDROP, 3);
			}

			void _AppendFileOrShell(string path, string name = null) {
				if (b.Length > 0) b.AppendLine();
				var pi = new TUtil.PathInfo(path, name);
				b.Append(pi.FormatCode(what - 11, ++index));
			}

			void _AppendScriptOrLink(string path, string name, FileNode fn = null) {
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
						b.Append($"string s{++index} = ");
					}

					b.Append(what switch {
						1 => $"\"{name}\";",
						2 or 11 => $"@\"{path}\";",
						3 or 4 => $"script.run(@\"{path}\");",
						_ => $"run.it(@\"{path}\");"
					});
				}
			}

			static unsafe void _GetShell(byte[] b, out string[] shells, out string[] names) {
				shells = names = null;
				fixed (byte* p = b) {
					int* pi = (int*)p;
					int n = *pi++; if (n < 1) return;
					shells = new string[n]; names = new string[n];
					IntPtr pidlFolder = (IntPtr)(p + *pi++);
					for (int i = 0; i < n; i++) {
						using var pidl = new Pidl(pidlFolder, (IntPtr)(p + pi[i]));
						shells[i] = pidl.ToString();
						names[i] = pidl.ToShellString(SIGDN.NORMALDISPLAY);
					}
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
