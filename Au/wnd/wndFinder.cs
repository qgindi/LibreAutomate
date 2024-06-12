using static Au.wnd.Internal_;

//TODO3: if no name, prefer non-tooltip window. Now often finds tooltip instead of the wanted window. Eg Java menus.

namespace Au;

/// <summary>
/// Finds top-level windows (<see cref="wnd"/>). Contains name and other parameters of windows to find.
/// </summary>
/// <remarks>
/// Can be used instead of <see cref="wnd.find"/> or <see cref="wnd.findAll"/>.
/// These codes are equivalent:
/// <code><![CDATA[wnd w = wnd.find(a, b, c, d, e); if(!w.Is0) print.it(w);]]></code>
/// <code><![CDATA[var p = new wndFinder(a, b, c, d, e); if(p.Exists()) print.it(p.Result);]]></code>
/// Also can find in a list of windows.
/// </remarks>
public class wndFinder {
	readonly wildex _name;
	readonly wildex _cn;
	readonly wildex _program;
	readonly int _processId;
	readonly int _threadId;
	readonly wnd _owner;
	readonly Func<wnd, bool> _also;
	readonly WFlags _flags;
	readonly WContains _contains;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	/// <summary>
	/// Parsed parameter values. All read-only.
	/// </summary>
	public TProps Props => new(this);

	[NoDoc]
	public struct TProps {
		readonly wndFinder _f;
		internal TProps(wndFinder f) { _f = f; }

		public wildex name => _f._name;
		public wildex cn => _f._cn;
		public wildex program => _f._program;
		public int processId => _f._processId;
		public int threadId => _f._threadId;
		public wnd owner => _f._owner;
		public WFlags flags => _f._flags;
		public Func<wnd, bool> also => _f._also;
		public WContains contains => _f._contains;

		/// <summary>
		/// After unsuccessful <see cref="IsMatch"/> indicates the parameter that does not match.
		/// </summary>
		public EProps DoesNotMatch => _f._stopProp;
	}

	EProps _stopProp;

	[NoDoc]
	public enum EProps { name = 1, cn, of, also, contains, visible, cloaked, }

	public override string ToString() {
		using (new StringBuilder_(out var b)) {
			_Append("name", _name);
			_Append("cn", _cn);
			if (_program != null) _Append("program", _program); else if (_processId != 0) _Append("processId", _processId); else if (_threadId != 0) _Append("threadId", _threadId);
			if (_also != null) _Append("also", "");
			_Append("contains", _contains.Value);
			return b.ToString();

			void _Append(string k, object v) {
				if (v == null) return;
				if (b.Length != 0) b.Append(", ");
				b.Append(k).Append('=').Append(v);
			}
		}
	}
#pragma warning restore CS1591

	/// <inheritdoc cref="wnd.find" path="//param|//exception"/>
	public wndFinder(
		[ParamString(PSFormat.Wildex)] string name = null,
		[ParamString(PSFormat.Wildex)] string cn = null,
		[ParamString(PSFormat.Wildex)] WOwner of = default,
		WFlags flags = 0, Func<wnd, bool> also = null, WContains contains = default) {
		_name = name;
		if (cn != null) _cn = cn.Length != 0 ? cn : throw new ArgumentException("cn cannot be \"\". Use null.");
		of.GetValue(out _program, out _processId, out _threadId, out _owner);
		_flags = flags;
		_also = also;
		_contains = contains;
	}

	//rejected. Better slightly longer code than unclear and possibly ambiguous code where need to learn string parsing rules.
	///// <summary>
	///// Implicit conversion from string that can contain window name, class name, program and/or a <i>contains</i> object.
	///// Examples: <c>"name,cn,program"</c>, <c>"name"</c>, <c>",cn"</c>, <c>"*,,program"</c>, <c>"name,cn"</c>, <c>"name,,program"</c>, <c>",cn,program"</c>, <c>"name,,,object"</c>.
	///// </summary>
	///// <param name="s">
	///// One or more comma-separated window properties: name, class, program and/or a <i>contains</i> object. Empty name means <c>""</c> (for any use *); other empty parts mean <c>null</c>.
	///// The same as parameters of <see cref="wnd.find"/>. The first 3 parts are <i>name</i>, <i>cn</i> and <i>of</i>. The last part is <i>contains</i> as string; can specify a UI element, control or image.
	///// The first 3 comma-separated parts cannot contain commas. Alternatively, parts can be separated by <c>'\0'</c> characters, like <c>"name\0"+"cn\0"+"program\0"+"object"</c>. Then parts can contain commas. Example: <c>"*one, two, three*\0"</c> (name with commas).
	///// </param>
	///// <exception cref="ArgumentException">See <see cref="wnd.find"/>.</exception>
	///// <exception cref="Exception">If specifies a <i>contains</i> object: exceptions of constructor of <see cref="wndChildFinder"/> or <see cref="elmFinder"/> or <see cref="uiimageFinder"/>.</exception>
	//public static implicit operator wndFinder(string s) {
	//	string name = null, cn = null, prog = null, contains = null;
	//	char[] sep = null; if (s.Contains('\0')) sep = s_sepZero; else if (s.Contains(',')) sep = s_sepComma;
	//	if (sep == null) name = s;
	//	else {
	//		var ra = s.Split(sep, 4);
	//		name = ra[0];
	//		if (ra[1].Length > 0) cn = ra[1];
	//		if (ra.Length > 2 && ra[2].Length > 0) prog = ra[2];
	//		if (ra.Length > 3 && ra[3].Length > 0) contains = ra[3];
	//	}
	//	return new wndFinder(name, cn, prog, contains: contains);
	//}
	//static readonly char[] s_sepComma = { ',' }, s_sepZero = { '\0' };

	/// <summary>
	/// The found window.
	/// </summary>
	public wnd Result { get; internal set; }

	/// <summary>
	/// Finds the specified window, like <see cref="wnd.find"/>.
	/// </summary>
	/// <returns>If found, returns <see cref="Result"/>, else <c>default(wnd)</c>.</returns>
	/// <remarks>
	/// Functions <b>Find</b> and <b>Exists</b> differ only in their return types.
	/// </remarks>
	public wnd Find() => Exists() ? Result : default;

	/// <summary>
	/// Finds the specified window, like <see cref="wnd.find"/>. Can wait and throw <b>NotFoundException</b>.
	/// </summary>
	/// <returns>If found, returns <see cref="Result"/>. Else throws exception or returns <c>default(wnd)</c> (if <i>wait</i> negative).</returns>
	/// <param name="wait">The wait timeout, seconds. If 0, does not wait. If negative, does not throw exception when not found.</param>
	/// <exception cref="NotFoundException" />
	/// <remarks>
	/// Functions <b>Find</b> and <b>Exists</b> differ only in their return types.
	/// </remarks>
	public wnd Find(Seconds wait) => Exists(wait) ? Result : default;

	/// <returns>If found, sets <see cref="Result"/> and returns <c>true</c>, else <c>false</c>.</returns>
	/// <inheritdoc cref="Find()"/>
	public bool Exists() {
		using var k = new WndList_(_AllWindows());
		return _FindOrMatch(k) >= 0;
	}

	/// <returns>If found, sets <see cref="Result"/> and returns <c>true</c>. Else throws exception or returns <c>false</c> (if <i>wait</i> negative).</returns>
	/// <inheritdoc cref="Find(Seconds)"/>
	public bool Exists(Seconds wait) {
		var r = wait.Exists_() ? Exists() : !Wait(wait, false).Is0;
		return r || wait.ReturnFalseOrThrowNotFound_();
	}

	/// <summary>
	/// Waits until window exists or is active.
	/// </summary>
	/// <returns>Returns <see cref="Result"/>. On timeout returns <c>default(wnd)</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="active">The window must be the active window (<see cref="wnd.active"/>), and not minimized.</param>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
	/// <remarks>
	/// Same as <see cref="Find(Seconds)"/>, except:
	/// - 0 timeout means infinite.
	/// - on timeout throws <b>TimeoutException</b>, not <b>NotFoundException</b>.
	/// - has parameter <i>active</i>.
	/// </remarks>
	public wnd Wait(Seconds timeout, bool active) {
		var loop = new WaitLoop(timeout);
		for (; ; ) {
			if (active) {
				wnd w = wnd.active;
				if (IsMatch(w) && !w.IsMinimized) return Result = w;
				//CONSIDER: also wait until released mouse buttons.
			} else {
				if (Exists()) return Result;
			}
			if (!loop.Sleep()) return default;
		}
	}

	ArrayBuilder_<wnd> _AllWindows() {
		//FUTURE: optimization: if cn not wildcard etc, at first find atom.
		//	If not found, don't search. If found, compare atom, not class name string.

		var f = _threadId != 0 ? EnumAPI.EnumThreadWindows : EnumAPI.EnumWindows;
		return EnumWindows2(f, 0 == (_flags & WFlags.HiddenToo), true, wParent: _owner, threadId: _threadId);
	}

	/// <summary>
	/// Finds the specified window in a list of windows, and sets <see cref="Result"/>.
	/// </summary>
	/// <returns>Returns 0-based index, or -1 if not found.</returns>
	/// <param name="a">Array or list of windows, for example returned by <see cref="wnd.getwnd.allWindows"/>.</param>
	public int FindInList(IEnumerable<wnd> a) {
		using var k = new WndList_(a);
		return _FindOrMatch(k);
	}

	/// <summary>
	/// Finds all matching windows, like <see cref="wnd.findAll"/>.
	/// </summary>
	/// <returns>Array containing 0 or more window handles as <b>wnd</b>.</returns>
	public wnd[] FindAll() {
		return _FindAll(new WndList_(_AllWindows()));
	}

	/// <summary>
	/// Finds all matching windows in a list of windows.
	/// </summary>
	/// <returns>Array containing 0 or more window handles as <b>wnd</b>.</returns>
	/// <param name="a">Array or list of windows, for example returned by <see cref="wnd.getwnd.allWindows"/>.</param>
	public wnd[] FindAllInList(IEnumerable<wnd> a) {
		return _FindAll(new WndList_(a));
	}

	wnd[] _FindAll(WndList_ k) {
		using (k) {
			using var ab = new ArrayBuilder_<wnd>();
			_FindOrMatch(k, w => ab.Add(w)); //CONSIDER: ab could be part of WndList_. Now the delegate creates garbage.
			return ab.ToArray();
		}
	}

	/// <summary>
	/// Returns index of matching element or -1.
	/// Returns -1 if using <i>getAll</i>.
	/// </summary>
	/// <param name="a">List of <b>wnd</b>. Does not dispose it.</param>
	/// <param name="getAll">If not <c>null</c>, calls it for all matching and returns -1.</param>
	/// <param name="cache"></param>
	int _FindOrMatch(WndList_ a, Action<wnd> getAll = null, WFCache cache = null) {
		Result = default;
		_stopProp = 0;
		if (a.Type == WndList_.ListType.None) return -1;
		bool inList = a.Type != WndList_.ListType.ArrayBuilder;
		bool ignoreVisibility = cache?.IgnoreVisibility ?? false;
		bool mustBeVisible = inList && (_flags & WFlags.HiddenToo) == 0 && !ignoreVisibility;
		bool isOwner = inList && !_owner.Is0;
		int ownerTid = 0;
		bool isTid = inList ? _threadId != 0 : false;
		List<int> pids = null; bool programNamePlanB = false; //variables for faster getting/matching program name

		int index = 0;
		for (; a.Next(out wnd w); index++) {
			if (w.Is0) continue;

			//Speed of getting properties of 1000 windows with hot CPU:
			//name 1000, class 1000
			//foreign tid/pid 900/1800,
			//visible 30, cloaked 1100,
			//owner 40, style 40, exstyle 40,
			//isOwned(2) 2000, isOwned(2, ref tid) 1000,
			//rect 500,
			//GetProp(randomString) 1700, GetProp(existingString) 3000, GetProp(atom) 1000, GlobalFindAtom 1700,
			//program 6000

			if (mustBeVisible) {
				if (!w.IsVisible) { _stopProp = EProps.visible; continue; }
			}

			if (isOwner) {
				if (!w.IsOwnedBy2_(_owner, 2, ref ownerTid)) { _stopProp = EProps.of; continue; }
			}

			cache?.Begin(w);

			if (_name != null) {
				var s = cache != null && cache.CacheName ? (cache.Name ??= w.NameTL_) : w.NameTL_;
				if (!_name.Match(s)) { _stopProp = EProps.name; continue; }
				//note: name is before classname. It makes faster in slowest cases (HiddenToo), because most windows are nameless.
			}

			if (_cn != null) {
				var s = cache != null ? (cache.Class ?? (cache.Class = w.ClassName)) : w.ClassName;
				if (!_cn.Match(s)) { _stopProp = EProps.cn; continue; }
			}

			if (0 == (_flags & WFlags.CloakedToo) && !ignoreVisibility) {
				if (w.IsCloaked) { _stopProp = EProps.cloaked; continue; }
			}

			int pid = 0, tid = 0;
			if (_program != null || _processId != 0 || isTid) {
				if (cache != null) {
					if (cache.Tid == 0) cache.Tid = w.GetThreadProcessId(out cache.Pid);
					tid = cache.Tid; pid = cache.Pid;
				} else tid = w.GetThreadProcessId(out pid);
				if (tid == 0) { _stopProp = EProps.of; continue; }
				//speed: with foreign processes the same speed as getting name or class name. Much faster if same process.
			}

			if (isTid) {
				if (_threadId != tid) { _stopProp = EProps.of; continue; }
			}

			if (_processId != 0) {
				if (_processId != pid) { _stopProp = EProps.of; continue; }
			}

			if (_program != null) {
				//Getting program name is one of slowest parts.
				//Usually it does not slow down much because need to do it only 1 or several times, only when window name, class etc match.
				//The worst case is when only program is specified, and the very worst case is when also using flag HiddenToo.
				//We are prepared for the worst case.
				//Normally we call process.getName. In most cases it is quite fast.
				//Anyway, we use this optimization:
				//	Add pid of processes that don't match the specified name in the pids list (bad pids).
				//	Next time, if pid is in the bad pids list, just continue, don't need to get program name again.
				//However in the worst case we would encounter some processes that process.getName cannot get name using the fast API.
				//For each such process it would then use the much slower 'get all processes' API, which is almost as slow as Process.GetProcessById(pid).ProgramName.
				//To solve this:
				//We tell process.getName to not use the slow API, but just return null when the fast API fails.
				//When it happens (process.getName returns null):
				//	If need full path: continue, we cannot do anything more.
				//	Switch to plan B and no longer use all the above. Plan B:
				//	Get list of pids of all processes that match _program. For it we call process.GetProcessesByName_, which uses the same slow API, but we call it just one time.
				//	If it returns null (it means there are no matching processes), break (window not found).
				//	From now, in each loop will need just to find pid in the returned list, and continue if not found.

				_stopProp = EProps.of;
				g1:
				if (programNamePlanB) {
					if (!pids.Contains(pid)) continue;
				} else {
					if (pids != null && pids.Contains(pid)) continue; //is known bad pid?

					string pname = cache != null ? (cache.Program ?? (cache.Program = _Program())) : _Program();
					string _Program() => process.getName(pid, false, true);
					//string _Program() => process.getName(pid, 0!=(_flags&WFlags.ProgramPath), true);

					if (pname == null) {
						//if(0!=(_flags&WFlags.ProgramPath)) continue;

						//switch to plan B
						process.GetProcessesByName_(ref pids, _program);
						if (pids.NE_()) break;
						programNamePlanB = true;
						goto g1;
					}

					if (!_program.Match(pname)) {
						if (a.Type == WndList_.ListType.SingleWnd) break;
						pids ??= new List<int>(16);
						pids.Add(pid); //add bad pid
						continue;
					}
				}
				_stopProp = 0;
			}

			if (_also != null) {
				bool ok = false;
				try { ok = _also(w); }
				catch (AuWndException) { } //don't throw if w destroyed
				if (!ok) { _stopProp = EProps.also; continue; }
			}

			if (_contains.Value != null) {
				bool found = false;
				try {
					switch (_contains.Value) {
					case elmFinder f: found = w.HasElm(f); break;
					case wndChildFinder f: found = f.Exists(w); break;
					case uiimageFinder f: found = f.Exists(w); break;
					case ocrFinder f: found = f.Exists(w); break;
					}
				}
				catch (Exception ex) {
					if (!(ex is AuWndException)) print.warning("Exception when tried to find the 'contains' object. " + ex, -1);
				}
				if (!found) { _stopProp = EProps.contains; continue; }
			}

			if (getAll != null) {
				getAll(w);
				continue;
			}

			Result = w;
			return index;
		}

		if (index == 0 && !inList) if (!_owner.Is0 || _threadId != 0) _stopProp = EProps.of;

		return -1;
	}

	/// <summary>
	/// Returns <c>true</c> if window <i>w</i> properties match the specified properties.
	/// </summary>
	/// <param name="w">A top-level window. If 0 or invalid, returns <c>false</c>.</param>
	/// <param name="cache">Can be used to make faster when multiple <b>wndFinder</b> variables are used with same window. The function gets window name/class/program once, and stores in <i>cache</i>; next time it gets these strings from <i>cache</i>.</param>
	/// <seealso cref="wnd.IsMatch"/>
	public bool IsMatch(wnd w, WFCache cache = null) {
		return 0 == _FindOrMatch(new WndList_(w), cache: cache);
	}
}
