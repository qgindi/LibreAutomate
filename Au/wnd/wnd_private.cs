namespace Au {
	public unsafe partial struct wnd {
		/// <summary>
		/// <c>if(!IsOfThisThread) { Thread.Sleep(15); SendTimeout(1000, 0); }</c>
		/// </summary>
		internal void MinimalSleepIfOtherThread_() {
			if (!IsOfThisThread) MinimalSleepNoCheckThread_();
		}

		/// <summary>
		/// <c>Thread.Sleep(15); SendTimeout(1000, 0);</c>
		/// </summary>
		internal void MinimalSleepNoCheckThread_() {
			Debug.Assert(!IsOfThisThread);
			//perf.first();
			Thread.Sleep(15);
			SendTimeout(1000, out _, 0);
			//perf.nw();
		}

		/// <summary>
		/// On Win10+, if <i>w</i> is <c>"ApplicationFrameWindow"</c>, returns the real app window <c>"Windows.UI.Core.CoreWindow"</c> hosted by <i>w</i>.
		/// If <i>w</i> is minimized, cloaked (eg on other desktop) or the app is starting, the <c>"Windows.UI.Core.CoreWindow"</c> is not its child. Then searches for a top-level window named like <i>w</i>. It is unreliable, but no API for this.
		/// Info: <c>"Windows.UI.Core.CoreWindow"</c> windows hosted by <c>"ApplicationFrameWindow"</c> belong to separate processes. All <c>"ApplicationFrameWindow"</c> windows belong to a single process.
		/// </summary>
		static wnd _WindowsStoreAppFrameChild(wnd w) {
			bool retry = false;
			string name;
		g1:
			if (!osVersion.minWin10 || !w.ClassNameIs("ApplicationFrameWindow")) return default;
			wnd c = Api.FindWindowEx(w, default, "Windows.UI.Core.CoreWindow", null);
			if (!c.Is0) return c;
			if (retry) return default;

			name = w.NameTL_; if (name.NE()) return default;

			for (; ; ) {
				c = Api.FindWindowEx(default, c, "Windows.UI.Core.CoreWindow", name); //I could not find API for it
				if (c.Is0) break;
				if (c.IsCloaked) return c; //else probably it is an unrelated window
			}

			retry = true;
			goto g1;
		}

		//not used
		///// <summary>
		///// The reverse of <b>_WindowsStoreAppFrameChild</b>.
		///// </summary>
		//static wnd _WindowsStoreAppHost(wnd w)
		//{
		//	if(!osVersion.minWin10 || !w.ClassNameIs("Windows.UI.Core.CoreWindow")) return default;
		//	wnd wo = w.Get.DirectParent; if(!wo.Is0 && wo.ClassNameIs("ApplicationFrameWindow")) return wo;
		//	string s = w.GetText(false, false); if(s.NE()) return default;
		//	return Api.FindWindow("ApplicationFrameWindow", s);
		//}

		internal static partial class Internal_ {
			/// <summary>
			/// Calls API <b>SetProp</b>/<b>GetProp</b> to set/get misc flags for a window.
			/// Currently unused.
			/// </summary>
			internal static class WinFlags {
				static readonly ushort s_atom = Api.GlobalAddAtom("Au.WFlags_"); //atom is much faster than string
																				 //note: cannot delete atom, eg in static dtor. Deletes even if currently used by a window prop, making the prop useless.

				internal static bool Set(wnd w, WFlags_ flags, bool? setAddRem = null) {
					switch (setAddRem) {
					case true: flags = Get(w) | flags; break;
					case false: flags = Get(w) & ~flags; break;
					}
					return w.Prop.Set(s_atom, (int)flags);
				}

				internal static WFlags_ Get(wnd w) {
					return (WFlags_)(int)w.Prop[s_atom];
				}

				internal static WFlags_ Remove(wnd w) {
					return (WFlags_)(int)w.Prop.Remove(s_atom);
				}

				[Flags]
				internal enum WFlags_ {
					//these were used by elm.
					//ChromeYes = 1,
					//ChromeNo = 2,
				}
			}

			//internal class LastWndProps
			//{
			//	wnd _w;
			//	long _time;
			//	string _class, _programName, _programPath;
			//	int _tid, _pid;

			//	void _GetCommon(wnd w)
			//	{
			//		var t = perf.ms;
			//		if(w != _w || t - _time > 100) { _w = w; _class = _programName= _programPath = null; _tid = _pid = 0; }
			//		_time = t;
			//	}

			//	//internal string GetName(wnd w) { _GetCommon(w); return _name; }

			//	internal string GetClass(wnd w) { _GetCommon(w); return _class; }

			//	internal string GetProgram(wnd w, bool fullPath) { _GetCommon(w); return fullPath ? _programPath : _programName; }

			//	internal int GetTidPid(wnd w, out int pid) { _GetCommon(w); pid = _pid; return _tid; }

			//	//internal void SetName(string s) => _name = s;

			//	internal void SetClass(string s) => _class = s;

			//	internal void SetProgram(string s, bool fullPath) { if(fullPath) _programPath = s; else _programName = s; }

			//	internal void SetTidPid(int tid, int pid) { _tid = tid; _pid = pid; }

			//	[ThreadStatic] static LastWndProps _ofThread;
			//	internal static LastWndProps OfThread => _ofThread ??= new LastWndProps();
			//}

			/// <summary>
			/// Returns <c>true</c> if <i>w</i> contains a non-zero special handle value (<see cref="SpecHWND"/>).
			/// Note: <b>SpecHWND.TOP</b> is 0.
			/// </summary>
			public static bool IsSpecHwnd(wnd w) {
				int i = (int)w;
				return (i <= 1 && i >= -3) || i == 0xffff;
			}

			/// <summary>
			/// Converts object to <b>wnd</b>.
			/// Object can contain <c>null</c>, <b>wnd</b>, <b>Control</b>, or <b>System.Windows.DependencyObject</b> (must be in element 0 of <c>object[]</c>).
			/// Avoids loading Forms and WPF dlls when not used.
			/// </summary>
			public static wnd FromObject(object o) => o switch {
				null => default,
				wnd w => w,
				object[] a => _Wpf(a[0]),
				_ => _Control(o)
			};

			[MethodImpl(MethodImplOptions.NoInlining)] //prevents loading Forms dlls when don't need
			static wnd _Control(object o) => (o as System.Windows.Forms.Control).Hwnd();

			[MethodImpl(MethodImplOptions.NoInlining)] //prevents loading WPF dlls when don't need
			static wnd _Wpf(object o) => (o as System.Windows.DependencyObject).Hwnd();

			/// <summary>
			/// If <i>w</i> is handle of a WPF element (<b>Window</b>, <b>Popup</b>, <b>HwndHost</b>-ed control, <b>HwndSource.RootVisual</b>), returns that element, else <c>null</c>.
			/// Slow if <b>HwndHost</b>-ed control.
			/// <i>w</i> can be <c>default</c>.
			/// </summary>
			public static System.Windows.FrameworkElement ToWpfElement(wnd w) {
				if (!w.Is0) {
					if (System.Windows.Interop.HwndSource.FromHwnd(w.Handle) is System.Windows.Interop.HwndSource hs) return hs.RootVisual as System.Windows.FrameworkElement;
					for (var p = w; !(p = p.Get.DirectParent).Is0; w = p) {
						if (System.Windows.Interop.HwndSource.FromHwnd(p.Handle)?.RootVisual is System.Windows.Media.Visual v) {
							return v.FindVisualDescendant(d => d is System.Windows.Interop.HwndHost hh && hh.Handle == w.Handle, orSelf: true) as System.Windows.FrameworkElement; //speed: 200 mcs
						}
					}
				}
				return null;
			}

			/// <summary>
			/// An enumerable list of <b>wnd</b> for <see cref="wndFinder._FindOrMatch"/> and <see cref="wndChildFinder._FindInList"/>.
			/// Holds <b>ArrayBuilder_</b> or <b>IEnumerator</b> or single <b>wnd</b> or none.
			/// Must be disposed if it is <b>ArrayBuilder_</b> or <b>IEnumerator</b>, else disposing is optional.
			/// </summary>
			internal struct WndList_ : IDisposable {
				internal enum ListType { None, ArrayBuilder, Enumerator, SingleWnd }

				ListType _t;
				int _i;
				wnd _w;
				IEnumerator<wnd> _en;
				ArrayBuilder_<wnd> _ab;

				internal WndList_(ArrayBuilder_<wnd> ab) {
					_ab = ab;
					_t = ListType.ArrayBuilder;
				}

				internal WndList_(IEnumerable<wnd> en) {
					var e = en?.GetEnumerator();
					if (e != null) {
						_en = e;
						_t = ListType.Enumerator;
					}
				}

				internal WndList_(wnd w) {
					if (!w.Is0) {
						_w = w;
						_t = ListType.SingleWnd;
					}
				}

				internal ListType Type => _t;

				internal bool Next(out wnd w) {
					w = default;
					switch (_t) {
					case ListType.ArrayBuilder:
						if (_i == _ab.Count) return false;
						w = _ab[_i++];
						break;
					case ListType.Enumerator:
						if (!_en.MoveNext()) return false;
						w = _en.Current;
						break;
					case ListType.SingleWnd:
						if (_i > 0) return false;
						_i = 1; w = _w;
						break;
					default:
						return false;
					}
					return true;
				}

				public void Dispose() {
					switch (_t) {
					case ListType.ArrayBuilder: _ab.Dispose(); break;
					case ListType.Enumerator: _en.Dispose(); break;
					}
				}
			}
		}
	}
}
