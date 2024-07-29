using static Au.wnd.Internal_;

namespace Au;

/// <summary>
/// Finds window controls (child windows). Contains name and other parameters of controls to find.
/// </summary>
/// <remarks>
/// Can be used instead of <see cref="wnd.Child"/> or <see cref="wnd.ChildAll"/>.
/// Also can be used to find window that contains certain control, like in examples.
/// </remarks>
/// <example>
/// Find window that contains certain control, and get the control too.
/// <code><![CDATA[
/// var cf = new wndChildFinder("Password*", "Static");
/// wnd w = wnd.find(cn: "#32770", also: t => t.HasChild(cf));
/// print.it(w);
/// print.it(cf.Result);
/// ]]></code>
/// The same with parameter <i>contains</i>.
/// <code><![CDATA[
/// var cf = new wndChildFinder("Password*", "Static");
/// wnd w = wnd.find(cn: "#32770", contains: cf);
/// print.it(w);
/// print.it(cf.Result);
/// ]]></code>
/// </example>
public class wndChildFinder {
	enum _NameIs : byte { name, text, elmName, wfName }

	readonly wildex _name;
	readonly wildex _className;
	readonly Func<wnd, bool> _also;
	WinformsControlNames _wfControls;
	readonly int _skipCount;
	readonly WCFlags _flags;
	readonly int? _id;
	readonly _NameIs _nameIs;

	/// <summary>
	/// See <see cref="wnd.Child"/>.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// - <i>name</i> starts with <c>"***"</c>, but the prefix is invalid.
	/// - <i>cn</i> is <c>""</c>. To match any, use <c>null</c>.
	/// - Invalid wildcard expression (<c>"**options "</c> or regular expression).
	/// </exception>
	/// <inheritdoc cref="wnd.Child(string, string, WCFlags, int?, Func{wnd, bool}, int)" path="/param"/>
	public wndChildFinder(
		[ParamString(PSFormat.Wildex)] string name = null,
		[ParamString(PSFormat.Wildex)] string cn = null,
		WCFlags flags = 0, int? id = null, Func<wnd, bool> also = null, int skip = 0
		) {
		if (cn != null) {
			if (cn.Length == 0) throw new ArgumentException("Class name cannot be \"\". Use null.");
			_className = cn;
		}
		if (name != null) {
			switch (StringUtil.ParseParam3Stars_(ref name, "text", "elmName", "wfName"/*, "label"*/)) {
			case -1: throw new ArgumentException("Invalid name prefix. Can be: \"***text \", \"***elmName \", \"***wfName \"."); //, \"***label \"
			case 1: _nameIs = _NameIs.text; break;
			case 2: _nameIs = _NameIs.elmName; break;
			case 3: _nameIs = _NameIs.wfName; break;
				//case 4: _nameIs = _NameIs.label; break;
			}
			_name = name;
		}
		_flags = flags;
		_id = id;
		_also = also;
		_skipCount = skip;
	}

	/// <summary>
	/// The found control.
	/// </summary>
	public wnd Result { get; internal set; }

	/// <summary>
	/// Finds the specified child control, like <see cref="wnd.Child"/>.
	/// </summary>
	/// <returns>If found, returns <see cref="Result"/>, else <c>default(wnd)</c>.</returns>
	/// <param name="wParent">Direct or indirect parent window. Can be top-level window or control.</param>
	/// <exception cref="AuWndException">Invalid <i>wParent</i>.</exception>
	/// <remarks>
	/// Functions <b>Find</b> and <b>Exists</b> differ only in their return types.
	/// </remarks>
	public wnd Find(wnd wParent) => Exists(wParent) ? Result : default;

	/// <summary>
	/// Finds the specified child control, like <see cref="wnd.Child"/>. Can wait and throw <b>NotFoundException</b>.
	/// </summary>
	/// <returns>If found, returns <see cref="Result"/>. Else throws exception or returns <c>default(wnd)</c> (if <i>wait</i> negative).</returns>
	/// <param name="wParent">Direct or indirect parent window. Can be top-level window or control.</param>
	/// <param name="wait">The wait timeout, seconds. If 0, does not wait. If negative, does not throw exception when not found.</param>
	/// <exception cref="AuWndException">Invalid <i>wParent</i>.</exception>
	/// <exception cref="NotFoundException" />
	/// <remarks>
	/// Functions <b>Find</b> and <b>Exists</b> differ only in their return types.
	/// </remarks>
	public wnd Find(wnd wParent, Seconds wait) => Exists(wParent, wait) ? Result : default;

	/// <returns>If found, sets <see cref="Result"/> and returns <c>true</c>, else <c>false</c>.</returns>
	/// <inheritdoc cref="Find(wnd)"/>
	public bool Exists(wnd wParent) {
		using var k = new WndList_(_AllChildren(wParent));
		return _FindInList(wParent, k) >= 0;
	}

	/// <returns>If found, sets <see cref="Result"/> and returns <c>true</c>. Else throws exception or returns <c>false</c> (if <i>wait</i> negative).</returns>
	/// <inheritdoc cref="Find(wnd, Seconds)"/>
	public bool Exists(wnd wParent, Seconds wait) {
		var r = wait.Exists_() ? Exists(wParent) : Au.wait.until(wait, () => Exists(wParent));
		return r || wait.ReturnFalseOrThrowNotFound_();
	}

	ArrayBuilder_<wnd> _AllChildren(wnd wParent) {
		wParent.ThrowIfInvalid();
		return EnumWindows2(EnumAPI.EnumChildWindows,
			onlyVisible: 0 == (_flags & WCFlags.HiddenToo),
			sortFirstVisible: true,
			wParent: wParent,
			directChild: 0 != (_flags & WCFlags.DirectChild));
	}

	/// <summary>
	/// Finds the specified control in a list of controls.
	/// The <see cref="Result"/> property will be the control.
	/// </summary>
	/// <returns>0-based index, or -1 if not found.</returns>
	/// <param name="a">List of controls, for example returned by <see cref="wnd.getwnd.Children"/>.</param>
	/// <param name="wParent">Direct or indirect parent window. Used only for flag <b>DirectChild</b>.</param>
	public int FindInList(IEnumerable<wnd> a, wnd wParent = default) {
		using var k = new WndList_(a);
		return _FindInList(wParent, k);
	}

	/// <summary>
	/// Finds all matching child controls, like <see cref="wnd.ChildAll"/>.
	/// </summary>
	/// <returns>Array containing zero or more <b>wnd</b>.</returns>
	/// <param name="wParent">Direct or indirect parent window. Can be top-level window or control.</param>
	/// <exception cref="AuWndException">Invalid <i>wParent</i>.</exception>
	public wnd[] FindAll(wnd wParent) {
		return _FindAll(new WndList_(_AllChildren(wParent)), wParent);
	}

	/// <summary>
	/// Finds all matching controls in a list of controls.
	/// </summary>
	/// <returns>Array containing zero or more <b>wnd</b>.</returns>
	/// <param name="a">List of controls, for example returned by <see cref="wnd.getwnd.Children"/>.</param>
	/// <param name="wParent">Direct or indirect parent window. Used only for flag <b>DirectChild</b>.</param>
	public wnd[] FindAllInList(IEnumerable<wnd> a, wnd wParent = default) {
		return _FindAll(new WndList_(a), wParent);
	}

	wnd[] _FindAll(WndList_ k, wnd wParent) {
		using (k) {
			using var ab = new ArrayBuilder_<wnd>();
			_FindInList(wParent, k, w => ab.Add(w)); //CONSIDER: ab could be part of _WndList. Now the delegate creates garbage.
			return ab.ToArray();
		}
	}

	/// <summary>
	/// Returns index of matching element or -1.
	/// </summary>
	/// <param name="wParent">Parent window. Can be <c>default(wnd)</c> if <i>inList</i> and no <b>DirectChild</b> flag and not using winforms name.</param>
	/// <param name="a">List of <b>wnd</b>. Does not dispose it.</param>
	/// <param name="getAll">If not <c>null</c>, calls it for all matching and returns -1.</param>
	int _FindInList(wnd wParent, WndList_ a, Action<wnd> getAll = null) {
		Result = default;
		if (a.Type == WndList_.ListType.None) return -1;
		bool inList = a.Type != WndList_.ListType.ArrayBuilder;
		int skipCount = _skipCount;

		try { //will need to dispose something
			for (int index = 0; a.Next(out wnd w); index++) {
				if (w.Is0) continue;

				if (inList) { //else the enum function did this
					if (!_flags.Has(WCFlags.HiddenToo)) {
						if (!w.IsVisibleIn_(wParent)) continue;
					}

					if (_flags.Has(WCFlags.DirectChild) && !wParent.Is0) {
						if (w.ParentGWL_ != wParent) continue;
					}
				}

				if (_id != null) {
					if (w.ControlId != _id.Value) continue;
				}

				if (_className != null) {
					if (!_className.Match(w.ClassName)) continue;
				}

				if (_name != null) {
					string s;
					switch (_nameIs) {
					case _NameIs.text:
						s = w.ControlText;
						break;
					case _NameIs.elmName:
						s = w.NameElm;
						break;
					case _NameIs.wfName:
						if (_wfControls == null) {
							try {
								_wfControls = new WinformsControlNames(wParent.Is0 ? w : wParent);
							}
							catch (AuWndException) { //invalid parent window
								return -1;
							}
							catch (AuException e) { //probably process of higher UAC integrity level
								print.warning($"Failed to get winforms control names. {e.Message}");
								return -1;
							}
						}
						s = _wfControls.GetControlName(w);
						break;
					//case _NameIs.label:
					//	s = w.NameLabel;
					//	break;
					default:
						s = w.Name;
						break;
					}

					if (!_name.Match(s)) continue;
				}

				if (_also != null && !_also(w)) continue;

				if (getAll != null) {
					getAll(w);
					continue;
				}

				if (skipCount-- > 0) continue;

				Result = w;
				return index;
			}
		}
		finally {
			if (_wfControls != null) { _wfControls.Dispose(); _wfControls = null; }
		}

		return -1;
	}

	/// <summary>
	/// Returns <c>true</c> if control <c>c</c> properties match the specified properties.
	/// </summary>
	/// <param name="c">A control. Can be 0/invalid, then returns <c>false</c>.</param>
	/// <param name="wParent">Direct or indirect parent window. If used, returns <c>false</c> if it isn't parent (also depends on flag <b>DirectChild</b>).</param>
	public bool IsMatch(wnd c, wnd wParent = default) {
		if (!wParent.Is0 && !c.IsChildOf(wParent)) {
			Result = default;
			return false;
		}
		return 0 == _FindInList(wParent, new WndList_(c));
	}
}
