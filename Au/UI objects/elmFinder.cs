namespace Au;

/// <summary>
/// Finds UI elements (<see cref="elm"/>). Contains name and other parameters of elements to find.
/// </summary>
/// <example>
/// Find link "Example" in web page, and click. Wait max 5 s. Exception if not found.
/// <code><![CDATA[
/// var w = wnd.find(0, "* Chrome");
/// w.Elm["web:LINK", "Example"].Find(5).Invoke();
/// ]]></code>
/// Find window that contains certain UI element, and get the UI element too.
/// <code><![CDATA[
/// var f = new elmFinder("BUTTON", "Apply"); //or var f = elm.path["BUTTON", "Apply"];
/// wnd w = wnd.find(cn: "#32770", also: t => f.In(t).Exists()); //or t => t.HasElm(f)
/// print.it(w);
/// print.it(f.Result);
/// ]]></code>
/// </example>
public unsafe class elmFinder {
	readonly string _role, _name, _prop, _navig;
	readonly EFFlags _flags;
	readonly int _skip;
	readonly Func<elm, bool> _also;
	Cpp.Cpp_AccFindCallbackT _also2;
	elmFinder _next;
	char _resultProp;
	wnd _wnd;
	elm _elm;

	/// <summary>
	/// The found UI element.
	/// null if not found or if used <see cref="ResultGetProperty"/>.
	/// </summary>
	public elm Result { get; private set; }

	/// <summary>
	/// The requested propery of the found UI element, depending on <see cref="ResultGetProperty"/>.
	/// null if: 1. UI element not found. 2. <b>ResultGetProperty</b> not used or is '-'. 3. Failed to get the property.
	/// </summary>
	/// <remarks>
	/// The type depends on the property. Most properties are <b>String</b>. Others: <see cref="elm.Rect"/>, <see cref="elm.State"/>, <see cref="elm.WndContainer"/>, <see cref="elm.HtmlAttributes"/>.
	/// </remarks>
	public object ResultProperty { get; private set; }

	/// <summary>
	/// Set this when you need only some property of the UI element (name, etc) and not the UI element itself.
	/// The value is a character like with <see cref="elm.GetProperties"/>, for example 'n' for Name. Use '-' if you don't need any property.
	/// </summary>
	/// <exception cref="ArgumentException">Used parameter <i>also</i>, <i>navig</i> or <i>next</i>.</exception>
	public char ResultGetProperty {
		set {
			if (_also != null) throw new ArgumentException("ResultGetProperty cannot be used with parameter 'also'.");
			if (_navig != null) throw new ArgumentException("ResultGetProperty cannot be used with parameter 'navig'.");
			if (_next != null) throw new ArgumentException("ResultGetProperty cannot be used with a path finder.");
			_resultProp = value;
		}
	}

	void _ClearResult() {
		Result = null;
		ResultProperty = null;
	}

	/// <summary>
	/// Stores the specified UI element properties in this object.
	/// Parameters are like of <see cref="this"/>.
	/// </summary>
	/// <inheritdoc cref="this" path="/param"/>
	public elmFinder(string role = null,
		[ParamString(PSFormat.Wildex)] string name = null,
		Strings prop = default, EFFlags flags = 0, Func<elm, bool> also = null,
		int skip = 0, string navig = null
		) {
		_role = role;
		_name = name;
		_prop = prop.Value switch { null => null, string s => s.Replace('|', '\0'), _ => string.Join('\0', prop.ToArray()) };
		_flags = flags;
		_also = also;
		_skip = skip >= -1 ? skip : throw new ArgumentOutOfRangeException(nameof(skip));
		_navig = navig;
	}

	internal elmFinder(wnd w, elm e) {
		_wnd = w;
		_elm = e;
		_flags = _EFFlags_Empty;
	}

	const EFFlags _EFFlags_Empty = (EFFlags)0x10000000;

	/// <summary>
	/// Creates an <see cref="elmFinder"/> for finding a UI element. Supports path like <c>var e = w.Elm["ROLE1", "Name1"]["ROLE2", "Name2"].Find();</c>.
	/// </summary>
	/// <returns>The new finder or the first finder in path.</returns>
	/// <param name="role">
	/// UI element role (<see cref="elm.Role"/>), like <c>"LINK"</c>.
	/// Can have prefix <c>"web:"</c>, <c>"firefox:"</c> or <c>"chrome:"</c> which means "search only in web page" and enables Chrome UI elements.
	/// Case-sensitive. Not wildcard. null means 'can be any'. Cannot be "".
	/// More info in Remarks.
	/// </param>
	/// <param name="name">
	/// UI element name (<see cref="elm.Name"/>).
	/// String format: [](xref:wildcard_expression).
	/// null means 'any'. "" means 'empty or unavailable'.
	/// </param>
	/// <param name="prop">
	/// Other UI element properties and search settings.
	/// Examples: <c>"value=xxx|@href=yyy"</c>, <c>new("value=xxx", "@href=yyy")</c>.
	/// More info in Remarks.
	/// </param>
	/// <param name="flags"></param>
	/// <param name="also">
	/// Callback function. Called for each matching UI element. Let it return true if this is the wanted UI element.
	/// Example: the UI element must contain point x y: <c>o => o.GetRect(out var r, o.WndTopLevel) &amp;&amp; r.Contains(266, 33)</c>
	/// </param>
	/// <param name="skip">
	/// 0-based index of matching UI element to use. Will skip this number of matching elements.
	/// Value -1 means "any", and can be useful when this finder is intermediate (ie not the last) in a path or when it has <i>navig</i>. If intermediate, will search for next element in all matching intermediate elements. If has <i>navig</i>, will retry with other matching elements if fails to navigate in the first found. It is slower and not so often useful, therefore the default value of this parameter is 0, not -1.
	/// </param>
	/// <param name="navig">If not null, after finding the specified UI element will call <see cref="elm.Navigate"/> with this string and use its result instead of the found element.</param>
	/// <exception cref="ArgumentException"><i>flags</i> contains <b>UIA</b> or <b>ClientArea</b> when appending (only the first finder can have these flags).</exception>
	/// <remarks>
	/// To create code for this function, use dialog "Find UI element".
	/// 
	/// In wildcard expressions supports PCRE regular expressions (prefix <c>"**r "</c>) but not .NET regular expressions (prefix <c>"**R "</c>). They are similar.
	/// 
	/// When using path like <c>["ROLE1", "Name1"]["ROLE2", "Name2"]["ROLE3", "Name3"]</c>, multiple finders are linked like finder1 -> finder2 -> finder3, so that the chain of finders will find UI element specified by the last finder.
	/// 
	/// More info in <see cref="elm"/> topic.
	/// 
	/// <h5>About the <i>role</i> parameter</h5>
	/// 
	/// Can be standard role (see <see cref="ERole"/>) like <c>"LINK"</c> or custom role like <c>"div"</c>. See <see cref="elm.Role"/>.
	/// 
	/// Can have a prefix:
	/// - <c>"web:"</c> - search only in the visible web page, not in whole window. Example: <c>"web:LINK"</c>.\
	///   Supports Firefox, Chrome, Internet Explorer (IE) and apps that use same code (Edge, Opera...). With other windows, searches in the first found visible UI element that has DOCUMENT role.\
	///   Tip: To search only NOT in web pages, use <i>prop</i> <c>"notin=DOCUMENT"</c> (Chrome, Firefox) or <c>"notin=PANE"</c> (IE).
	/// - <c>"firefox:"</c> - search only in the visible web page of Firefox or Firefox-based web browser. If w window class name starts with "Mozilla", can be used <c>"web:"</c> instead.
	/// - <c>"chrome:"</c> - search only in the visible web page of Chrome or Chrome-based web browser. If w window class name starts with "Chrome", can be used <c>"web:"</c> instead.
	/// 
	/// <note>Chrome web page UI elements normally are disabled (don't exist). Use prefix <c>"web:"</c> or <c>"chrome:"</c> to enable.</note>
	/// 
	/// Prefix cannot be used:
	/// - if <i>prop</i> contains <c>"id"</c> or <c>"class"</c>;
	/// - with flags <b>UIA</b>, <b>ClientArea</b>;
	/// - when searching in <b>elm</b>.
	/// 
	/// <h5>About the <i>prop</i> parameter</h5>
	/// 
	/// Format: one or more <c>"name=value"</c> strings, like <c>new("key=xxx", "@href=yyy")</c> or <c>"key=xxx|@href=yyy"</c>. Names must match case. Values of most string properties are wildcard expressions.
	/// 
	/// - <c>"class"</c> - search only in child controls that have this class name (see <see cref="wnd.ClassName"/>).\
	///   Cannot be used when searching in a UI element.
	/// - <c>"id"</c> - search only in child controls that have this id (see <see cref="wnd.ControlId"/>). If the value is not a number - Windows Forms control name (see <see cref="wnd.NameWinforms"/>); case-sensitive, not wildcard.\
	///   Cannot be used when searching in a UI element.
	/// - <c>"value"</c> - <see cref="elm.Value"/>.
	/// - <c>"desc"</c> - <see cref="elm.Description"/>.
	/// - <c>"state"</c> - <see cref="elm.State"/>. List of states the UI element must have and/or not have.\
	///   Example: <c>"state=CHECKED, FOCUSABLE, !DISABLED"</c>.\
	///   Example: <c>"state=0x100010, !0x1"</c>.\
	///   Will find UI element that has all states without <c>"!"</c> prefix and does not have any of states with <c>"!"</c> prefix.
	/// - <c>"rect"</c> - <see cref="elm.GetRect(out RECT, bool)"/> with <i>raw</i> true. Can be specified left, top, width and/or height, using <see cref="RECT.ToString"/> format.\
	///   Example: <c>"rect={L=1155 T=1182 W=132 H=13}"</c>.
	///   Example: <c>"rect={W=132 T=1182}"</c>.
	///   The L T coordinates are relative to the primary screen.
	/// - <c>"level"</c> - level (see <see cref="elm.Level"/>) at which the UI element can be found. Can be exact level, or minimal and maximal level separated by space.\
	///   The default value is 0 1000.
	/// - <c>"item"</c> - <see cref="elm.Item"/>.
	/// - <c>"action"</c> - <see cref="elm.DefaultAction"/>.
	/// - <c>"key"</c> - <see cref="elm.KeyboardShortcut"/>.
	/// - <c>"help"</c> - <see cref="elm.Help"/>.
	/// - <c>"uiaid"</c> - <see cref="elm.UiaId"/>.
	/// - <c>"maxcc"</c> - when searching, skip children of UI elements that have more than this number of direct children. Default 10000, min 1, max 1000000.\
	///   It can make faster. It also prevents hanging or crashing when a UI element in the UI element tree has large number of children. For example OpenOffice Calc TABLE has one billion children.
	/// - <c>"notin"</c> - when searching, skip children of UI elements that have these roles. It can make faster.\
	///   Example: <c>"notin=TREE,LIST,TOOLBAR"</c>.\
	///   Roles in the list must be separated with <c>","</c> or <c>", "</c>. Case-sensitive, not wildcard. See also: <see cref="EFFlags.MenuToo"/>.
	/// - <c>"@attr"</c> - <see cref="elm.HtmlAttribute"/>. Here "attr" is any attribute name. Example: <c>"@href=example"</c>.
	/// </remarks>
	/// <seealso cref="elm.path"/>
	/// <seealso cref="Next"/>
	public elmFinder this[string role = null,
		[ParamString(PSFormat.Wildex)] string name = null,
		Strings prop = default, EFFlags flags = 0, Func<elm, bool> also = null,
		int skip = 0, string navig = null
		] {
		get {
			var f = new elmFinder(role, name, prop, flags, also, skip, navig);
			if (_flags == _EFFlags_Empty) { f._wnd = _wnd; f._elm = _elm; return f; }
			var n = this; while (n._next != null) n = n._next;
			n.Next = f;
			return this;
		}
	}
	//rejected: default skip = -1. Rarely need.
	//	In some cases could find an unexpected element. Better to not find than to find (and click etc) wrong element.
	//	Instead the tool gives info to try -1 when not found or found wrong element.

	/// <summary>
	/// Gets or sets next finder in path (immediately after this finder).
	/// </summary>
	/// <exception cref="ArgumentException"><i>flags</i> contains <b>UIA</b> or <b>ClientArea</b>.</exception>
	/// <remarks>
	/// The setter creates or modifies a path (a chain of linked finders). Unlike <see cref="this"/>, which appends to the last finder in path, this function appends to this finder.
	/// </remarks>
	public elmFinder Next {
		get => _next;
		set {
			if (_flags.Has(_EFFlags_Empty)) throw new InvalidOperationException();
			if (value != _next) {
				if (value != null) {
					if (value._flags.HasAny(EFFlags.UIA | EFFlags.ClientArea)) throw new ArgumentException("Don't use flags UIA and ClientArea when searching in elm.");
				}
				_next = value;
				_also2 = null;
			}
		}
	}

	/// <summary>
	/// Sets or changes window or control where <see cref="Find"/> etc will search.
	/// </summary>
	/// <returns>This.</returns>
	/// <seealso cref="elm.path"/>
	public elmFinder In(wnd w) {
		if (_flags.Has(_EFFlags_Empty)) throw new InvalidOperationException();
		_wnd = w;
		return this;
	}

	/// <summary>
	/// Sets or changes parent UI element where <see cref="Find"/> etc will search.
	/// </summary>
	/// <returns>This.</returns>
	/// <seealso cref="elm.path"/>
	public elmFinder In(elm e) {
		if (_flags.Has(_EFFlags_Empty)) throw new InvalidOperationException();
		_elm = e;
		return this;
	}

	/// <summary>
	/// Finds the first matching descendant UI element in the window or UI element.
	/// </summary>
	/// <returns>If found, returns <see cref="Result"/>, else null.</returns>
	/// <exception cref="ArgumentException">
	/// - <i>role</i> is "" or invalid.
	/// - <i>name</i> is invalid wildcard expression (<c>"**options "</c> or regular expression).
	/// - <i>prop</i> contains unknown property names or errors in wildcard expressions.
	/// - <i>navig</i> string is invalid.
	/// - <i>flags</i> has <b>UIA</b> or <b>ClientArea</b> when searching in web page (role prefix <c>"web:"</c> etc) or <b>elm</b>.
	/// - <i>role</i> has a prefix ("web:" etc) when searching in <b>elm</b>.
	/// - <see cref="elm.Item"/> not 0 when searching in <b>elm</b>.
	/// </exception>
	/// <exception cref="AuWndException">Invalid window handle (0 or closed). See also <see cref="In(wnd)"/>.</exception>
	/// <exception cref="AuException">Failed. For example, window of a higher [](xref:uac) integrity level process.</exception>
	/// <remarks>
	/// To create code for this function, use dialog "Find UI element".
	/// 
	/// More info in <see cref="elm"/> topic.
	/// </remarks>
	/// <example>
	/// Find link "Example" in web page, and click. Wait max 5 s. Throw <b>NotFoundException</b> if not found.
	/// <code><![CDATA[
	/// var w = wnd.find(0, "* Chrome");
	/// w.Elm["web:LINK", "Example"].Find(5).Invoke();
	/// ]]></code>
	/// Try to find link "Example" in web page. Return if not found. Click if found.
	/// <code><![CDATA[
	/// var w = wnd.find(0, "* Chrome");
	/// var e = w.Elm["web:LINK", "Example"].Find();
	/// //var e = w.Elm["web:LINK", "Example"].Find(-5); //waits max 5 s
	/// if(e == null) { print.it("not found"); return; }
	/// e.Invoke();
	/// ]]></code>
	/// </example>
	public elm Find() => Exists() ? Result : null;

	/// <summary>
	/// Finds the first matching descendant UI element in the window or UI element. Can wait and throw <b>NotFoundException</b>.
	/// </summary>
	/// <returns>If found, returns <see cref="Result"/>. Else throws exception or returns null (if <i>waitS</i> negative).</returns>
	/// <param name="waitS">The wait timeout, seconds. If 0, does not wait. If negative, does not throw exception when not found.</param>
	/// <exception cref="ArgumentException"></exception>
	/// <exception cref="AuWndException"></exception>
	/// <exception cref="AuException"></exception>
	/// <exception cref="NotFoundException" />
	public elm Find(double waitS) => Exists(waitS) ? Result : null;

	/// <summary>
	/// Finds the first matching descendant UI element in the window or UI element. Like <see cref="Find"/>, just different return type.
	/// </summary>
	/// <returns>If found, sets <see cref="Result"/> and returns true, else false.</returns>
	/// <remarks></remarks>
	/// <inheritdoc cref="Find()" path="/exception"/>
	public bool Exists() => Find_(_elm != null, _wnd, _elm);

	/// <summary>
	/// Finds the first matching descendant UI element in the window or UI element. Can wait and throw <b>NotFoundException</b>. Like <see cref="Find(double)"/>, just different return type.
	/// </summary>
	/// <returns>If found, sets <see cref="Result"/> and returns true. Else throws exception or returns false (if <i>waitS</i> negative).</returns>
	/// <inheritdoc cref="Find(double)" path="//param|//exception"/>
	public bool Exists(double waitS) {
		if (Find_(_elm != null, _wnd, _elm, waitS == 0d ? null : waitS < 0 ? waitS : -waitS)) return true;
		return double.IsNegative(waitS) ? false : throw new NotFoundException();
	}

	/// <summary>
	/// Waits for a matching descendant UI element to appear in the window or UI element.
	/// </summary>
	/// <returns>If found, returns <see cref="Result"/>. On timeout returns null if <i>secondsTimeout</i> is negative; else exception.</returns>
	/// <param name="secondsTimeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <exception cref="ArgumentException"></exception>
	/// <exception cref="AuWndException"></exception>
	/// <exception cref="AuException"></exception>
	/// <exception cref="TimeoutException" />
	/// <remarks>
	/// Same as <see cref="Find(double)"/>, except:
	/// - 0 timeout means infinite.
	/// - on timeout throws <b>TimeoutException</b>, not <b>NotFoundException</b>.
	/// </remarks>
	public elm Wait(double secondsTimeout) => Find_(_elm != null, _wnd, _elm, secondsTimeout) ? Result : null;

	internal bool Find_(bool inElm, wnd w, elm eParent, double? waitS = null, bool isNext = false) {
		if (_flags.Has(_EFFlags_Empty)) throw new InvalidOperationException();
		if (_flags.Has(EFFlags.UIA | EFFlags.ClientArea)) throw new ArgumentException("Don't use flags UIA and ClientArea together.");
		if (_role != null) {
			if (_role == "") throw new ArgumentException("role cannot be \"\".");
			if (inElm && 0 != _role.Starts(false, "web:", "chrome:", "firefox:")) throw new ArgumentException("Don't use role prefix when searching in elm.");
		}

		Cpp.Cpp_Acc aParent; Cpp.Cpp_Acc* pParent = null;
		EFFlags flags = _flags;
		if (inElm) {
			if (!isNext) {
				if (_flags.HasAny(EFFlags.UIA | EFFlags.ClientArea)) throw new ArgumentException("Don't use flags UIA and ClientArea when searching in elm.");
				if (eParent == null) throw new ArgumentNullException();
				eParent.ThrowIfDisposed_();
				if (eParent.Item != 0) throw new ArgumentException("Item not 0.");
			}
			aParent = new(eParent); pParent = &aParent;
			if (!eParent.MiscFlags.Has(EMiscFlags.InProc)) flags |= EFFlags.NotInProc;
		} else {
			w.ThrowIfInvalid();
			aParent = default;
		}

		elm.WarnInSendMessage_();

		bool inProc = !flags.Has(EFFlags.NotInProc);

		_ClearResult();

		var ap = new Cpp.Cpp_AccFindParams(_role, _name, _prop, flags, Math.Max(0, _skip), _resultProp);

		if (!inElm) ap.RolePrefix(w); //converts role prefix to flags (C++ internal) and may enable Chrome AOs

		//if used skip<0 and path or navig, need to search in all possible paths. For it we use the 'also' callback.
		//FUTURE: optimize. Add part of code to the C++ dll. Now can walk same tree branches multiple times.
		Cpp.Cpp_AccFindCallbackT also = null;
		bool allPaths = _skip < 0 && (_next != null || _navig != null);
		if (allPaths) {
			also = _also2 ??= ca => {
				var e = new elm(ca);
				if (!_AlsoNavigNext(ref e)) return 0;
				Result = e;
				return 1;
			};
		} else {
			if (_also != null) also = _also2 ??= ca => _also(new elm(ca)) ? 1 : 0;
		}

		var to = new wait.Loop(waitS ?? -1, inProc ? s_owait10 : s_owait40);
		for (bool doneUAC = false, doneThread = false; ;) {
			var hr = Cpp.Cpp_AccFind(w, pParent, ap, also, out var ca, out string sResult);

			if (hr == 0) {
				switch (_resultProp) {
				case '\0':
					if (!allPaths) {
						var e = new elm(ca);
						if (_AlsoNavigNext(ref e, noAlso: true)) Result = e; else hr = Cpp.EError.NotFound;
					}
					break;
				case 'r' or 'D' or 's' or 'w' or '@':
					if (sResult == null) break;
					unsafe {
						fixed (char* p = sResult) {
							switch (_resultProp) {
							case 'r' or 'D': ResultProperty = *(RECT*)p; break;
							case 's': ResultProperty = *(EState*)p; break;
							case 'w': ResultProperty = (wnd)(*(int*)p); break;
							case '@': ResultProperty = elm.AttributesToDictionary_(p, sResult.Length); break;
							}
						}
					}
					break;
				default:
					ResultProperty = sResult;
					break;
				}
				if (hr == 0) return true;
			}

			if (hr == Cpp.EError.InvalidParameter) throw new ArgumentException(sResult);
			if ((hr == Cpp.EError.WindowClosed) || (!w.Is0 && !w.IsAlive)) return false; //FUTURE: check if a is disconnected etc. Or then never wait.

			if (!doneUAC) {
				doneUAC = true;
				w.UacCheckAndThrow_(); //CONSIDER: don't throw. Maybe show warning.
			}

			//print.it(hr > 0 ? $"hr={hr}" : $"hr={(int)hr:X}");
			switch (hr) {
			case Cpp.EError.NotFound:
				if (waitS == null) return false;
				break;
			default:
				Debug.Assert(!Cpp.IsCppError((int)hr));
				if (hr == (Cpp.EError)Api.RPC_E_SERVER_CANTMARSHAL_DATA && !_flags.Has(EFFlags.NotInProc))
					throw new AuException((int)hr, "For this UI element need flag NotInProc");
				throw new AuException((int)hr);
			}

			if (!doneThread) {
				doneThread = true;
				if (!w.Is0 && w.IsOfThisThread) return false;
			}

			if (!to.Sleep()) return false;
			GC.KeepAlive(eParent);
		}
	}

	static readonly OWait s_owait10 = new(period: 10), s_owait40 = new(period: 40);

	bool _AlsoNavigNext(ref elm e, bool noAlso = false) {
		if (!noAlso & _also != null && !_also(e)) return false;
		if (_navig != null) {
			var e2 = e.Navigate(_navig);
			if (e2 == null) return false;
			if (t_navigResult.need) t_navigResult = (true, e, e2);
			e = e2;
		}
		if (_next != null) {
			if (e.Item != 0) return false;
			if (!_next.Find_(true, default, e, isNext: true)) return false;
			e = _next.Result;
		}
		return true;
	}

	/// <summary>
	/// Finds all matching descendant UI elements in the window or UI element.
	/// </summary>
	/// <returns>Array of 0 or more elements.</returns>
	/// <example>
	/// <code><![CDATA[
	/// var w = wnd.find(1, "", "Shell_TrayWnd");
	/// print.it("---- buttons ----");
	/// print.it(w.Elm["BUTTON"].FindAll());
	/// print.it("---- all ----");
	/// print.it(w.Elm.FindAll());
	/// print.it("---- all buttons in elm at level 0 ----");
	/// var e = w.Elm["TOOLBAR", "Running applications"].Find();
	/// print.it(e.Elm["BUTTON", prop: "level=0"].FindAll());
	/// print.it("---- all in elm ----");
	/// print.it(e.Elm.FindAll());
	/// ]]></code>
	/// </example>
	/// <remarks>See <see cref="Find"/>.</remarks>
	/// <inheritdoc cref="Find" path="/exception"/>
	public elm[] FindAll() {
		var a = new List<elm>();
		var f = new elmFinder(_role, _name, _prop, _flags & ~_EFFlags_Empty, o => { //need new finder because cannot change _also or this finder
			if (_AlsoNavigNext(ref o)) a.Add(o);
			return false;
		});
		f.Find_(_elm != null, _wnd, _elm);
		return a.ToArray();
	}

	///
	public override string ToString() {
		using (new StringBuilder_(out var b)) {
			for (var n = this; n != null; n = n._next) n._ToString(b);
			return b.ToString();
		}
	}

	void _ToString(StringBuilder b) {
		b.Append('[');
		int n = 0;
		_Add(0, null, _role);
		_Add(1, "name", _name);
		_Add(2, "prop", _prop?.Replace('\0', '|'));
		if (_flags != 0) _Add(3, "flags", _flags.ToString(), false);
		if (_also != null) _Add(4, "also", "...", false);
		if (_skip != 0) _Add(5, "skip", _skip.ToString(), false);
		_Add(6, "navig", _navig);
		b.Append(']');

		void _Add(int i, string name, string value, bool isString = true) {
			if (value == null) return;
			if (n > 0) b.Append(", ");
			if (i > n++) b.Append(name).Append(": ");
			if (isString) value = value.Escape(limit: 50, quote: true);
			b.Append(value);
		}
	}

	[ThreadStatic] internal static (bool need, elm before, elm after) t_navigResult;

	//rejected. Rarely used. Maybe in the future, but different API, maybe ...In(controls).Find(), and move the code into Find_.
	///// <summary>
	///// Finds UI element in the specified control of window <i>w</i>.
	///// </summary>
	///// <returns>If found, returns <see cref="Result"/>, else null.</returns>
	///// <param name="w">Window that contains the control.</param>
	///// <param name="controls">Control properties. This functions searches in all matching controls.</param>
	///// <exception cref="Exception">Exceptions of <see cref="Find(wnd)"/>.</exception>
	///// <remarks>
	///// Functions <b>Find</b> and <b>Exists</b> differ only in their return types.
	///// 
	///// Alternatively you can specify control class name or id in role. How this function is different: 1. Allows to specify more control properties. 2. Works better/faster when the control is of a different process or thread than the parent window; else slightly slower.
	///// </remarks>
	//public elm Find(wnd w, wndChildFinder controls) => Exists(w, controls) ? Result : null;

	///// <returns>If found, sets <see cref="Result"/> and returns true, else false.</returns>
	///// <inheritdoc cref="Find(wnd, wndChildFinder)"/>
	//public bool Exists(wnd w, wndChildFinder controls) {
	//	w.ThrowIfInvalid();
	//	foreach (var c in controls.FindAll(w)) {
	//		try {
	//			if (Find_(false, c, null)) {
	//				controls.Result = c;
	//				return true;
	//			}
	//		}
	//		catch (AuException ex) when (!c.IsAlive) { Debug_.Print(ex.Message); } //don't throw AuWndException/AuException if the window or a control is destroyed while searching, but throw AuException if eg access denied
	//	}
	//	return false;
	//}

	//rejected. Better slightly longer code than unclear and possibly ambiguous code where you have to learn string parsing rules.
	//public static implicit operator elmFinder(string roleNameProp) {
	//	if (roleNameProp.NE()) throw new ArgumentException();
	//	int i = roleNameProp.FindAny(",\0");
	//	if (i < 0) return new(roleNameProp);
	//	var a = roleNameProp.Split(roleNameProp[i], 3);
	//	return new(a[0].Length > 0 ? a[0] : null, a[1], a.Length < 3 ? null : a[2]);
	//}
}

partial class elm {
	/// <summary>
	/// Gets an <see cref="elmFinder"/> for finding UI elements in a window or UI element that can be set later with <see cref="elmFinder.In"/>.
	/// Example: <c>var e = elm.path["ROLE", "Name"].In(w).Find();</c>. Same as <c>var e = w.Elm["ROLE", "Name"].Find();</c>.
	/// Example: <c>var e = elm.path["ROLE1", "Name1"]["ROLE2", "Name2"]["ROLE3", "Name3"].In(w).Find();</c>.
	/// </summary>
	/// <seealso cref="elmFinder.this"/>
	public static elmFinder path { get; } = new(default, null);

	/// <summary>
	/// Gets an <see cref="elmFinder"/> for finding UI elements in this UI element.
	/// Example: <c>var e2 = e1.Elm["ROLE", "Name"].Find();</c>.
	/// Example: <c>var e2 = e1.Elm["ROLE1", "Name1"]["ROLE2", "Name2"]["ROLE3", "Name3"].Find();</c>.
	/// Example: <c>print.it(e.Elm.FindAll());</c>.
	/// </summary>
	public elmFinder Elm => new(default, this);
}

partial struct wnd {
	/// <summary>
	/// Gets an <see cref="elmFinder"/> for finding UI elements in this window or control.
	/// Example: <c>var e = w.Elm["ROLE", "Name"].Find();</c>.
	/// Example: <c>var e = w.Elm["ROLE1", "Name1"]["ROLE2", "Name2"]["ROLE3", "Name3"].Find();</c>.
	/// Example: <c>print.it(w.Elm.FindAll());</c>.
	/// </summary>
	public elmFinder Elm => new(this, null);

	//public static wndFinder f_(
	//	[ParamString(PSFormat.wildex)] string name = null,
	//	[ParamString(PSFormat.wildex)] string cn = null,
	//	[ParamString(PSFormat.wildex)] WOwner of = default,
	//	WFlags flags = 0, Func<wnd, bool> also = null, WContains contains = default
	//	) => new(name, cn, of, flags, also, contains);

	//public wnd this[
	//	[ParamString(PSFormat.wildex)] string name = null,
	//	[ParamString(PSFormat.wildex)] string cn = null,
	//	int? id = null, WCFlags flags = 0, Func<wnd, bool> also = null, int skip = 0,
	//	] {
	//	get => Child(name, cn, k, also, skip);
	//}
}
