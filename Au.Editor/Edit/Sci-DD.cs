using static Au.Controls.Sci;
using System.Windows;
using Au.Tools;

partial class SciCode
{
	void _InitDragDrop() {
		Api.RevokeDragDrop(Hwnd); //of Scintilla
		Api.RegisterDragDrop(Hwnd, _dt = new _DragDrop(this));
		//Scintilla will call RevokeDragDrop when destroying window
	}

	_DragDrop _dt;

	class _DragDrop : Api.IDropTarget
	{
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
			string s = null;
			var b = new StringBuilder();
			int what = 0;

			if (_justText) {
				s = _data.text;
			} else {
				if (_sci._fn.IsCodeFile) {
					//var text = _sci.zText;
					//var meta = Au.Compiler.MetaComments.FindMetaComments(text);
					//if (meta.end > 0) {
					//	int pos1 = _sci.zPos16(pos8);
					//	if (pos1 > meta.start && pos1 < meta.end) return;
					//}

					string mi = _data.scripts
						? "1 var s = name;|2 var s = path;|3 script.run(path);|4 t[name] = o => script.run(path);"
						: "11 var s = path;|12 run.it(path);|13 t[name] = o => run.it(path);";
					what = popupMenu.showSimple(mi);
					if (what == 0) return;
				}

				if (_data.scripts) {
					var a = Panels.Files.TreeControl.DragDropFiles;
					if (a != null) {
						foreach (var f in a) {
							_AppendFile(f.ItemPath, f.Name, null, f);
						}
						s = b.ToString();
					}
				} else if (_data.files != null) {
					foreach (var path in _data.files) {
						if (what == 0) { _AppendFile(path, null); continue; }
						var r = new TUtil.PathInfo(path).GetCode();
						_AppendFile(r.path, r.name, r.args);
					}
					s = b.ToString();
				} else if (_data.shell != null) {
					_GetShell(_data.shell, out var shells, out var names);
					if (shells != null) {
						for (int i = 0; i < shells.Length; i++) {
							if (what == 0) { _AppendFile(shells[i], null); continue; }
							var r = new TUtil.PathInfo(shells[i]).GetCode();
							_AppendFile(r.path, names[i]);
						}
						s = b.ToString();
					}
				} else if (_data.linkName != null) {
					_AppendFile(_data.text, _GetLinkName(_data.linkName));
					s = b.ToString();
				}
			}

			if (!s.NE()) {
				var z = new Sci_DragDropData { x = xy.x, y = xy.y };
				var s8 = Encoding.UTF8.GetBytes(s);
				fixed (byte* p8 = s8) {
					z.text = p8;
					z.len = s8.Length;
					if (!_justText || 0 == ((DragDropEffects)effect & DragDropEffects.Move)) z.copy = 1;
					CodeInfo.Pasting(_sci);
					_sci.Call(SCI_DRAGDROP, 2, &z);
				}
				if (!_sci.IsFocused && _sci.Hwnd.Window.IsActive) { //note: don't activate window; let the drag source do it, eg Explorer activates on drag-enter.
					_sci._noModelEnsureCurrentSelected = true; //don't scroll treeview to currentfile
					_sci.Focus();
					_sci._noModelEnsureCurrentSelected = false;
				}
			} else {
				_sci.Call(SCI_DRAGDROP, 3);
			}

			void _AppendFile(string path, string name, string args = null, FileNode fn = null) {
				b.Append('\t', _sci.zLineIndentationFromPos(false, pos8));
				if (what == 0) {
					b.Append(path);
				} else {
					name = name.Escape();
					switch (what) {
					case 1: case 2: case 11: b.Append("var s = "); break;
					case 4: case 13: b.AppendFormat("t[\"{0}\"] = o => ", what == 4 ? name.RemoveSuffix(".cs") : name); break;
					}
					if (what == 12 || what == 13) b.Append("run.it(");
					if ((what == 11 || what == 12) && (path.Starts("\":: ") || path.Starts("folders.shell."))) b.AppendFormat("/* {0} */ ", name);
					if (!path.Ends('\"')) path = "@\"" + path + "\"";
					switch (what) {
					case 1: b.AppendFormat("\"{0}\"", name); break;
					case 2 or 11: b.Append(path); break;
					case 3 or 4: b.AppendFormat("script.run({0})", path); break;
					case 12 or 13:
						b.Append(path);
						if (!args.NE()) b.AppendFormat(", \"{0}\"", args.Escape());
						b.Append(')');
						break;
					}
					b.Append(';');
				}
				b.AppendLine();
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
