using IAccessible = Au.Types.Api.IAccessible;
using VarInt = Au.Types.Api.VarInt;
using NAVDIR = Au.Types.Api.NAVDIR;

namespace Au;

[ComVisible(true)]
partial class toolbar : IAccessible {
	IAccessible IAccessible.get_accParent() => _StdAO.get_accParent();

	int IAccessible.get_accChildCount() => _a.Count;

	int IAccessible.get_accChild(VarInt varChild, out object ppdispChild) { ppdispChild = null; return 1; }

	string IAccessible.get_accName(VarInt varChild) => _B(varChild, out var b) ? (b.Text ?? b.Tooltip) : _name;

	string IAccessible.get_accValue(VarInt varChild) => null;

	string IAccessible.get_accDescription(VarInt varChild) => _B(varChild, out _) ? null : "Floating toolbar";

	VarInt IAccessible.get_accRole(VarInt varChild) {
		Debug_.PrintIf(Api.GetCurrentThreadId() != _w.ThreadId, "thread");
		var r = !_B(varChild, out var b)
			? ERole.TOOLBAR
			: b.ItemType switch {
				TBItemType.Separator => ERole.SEPARATOR,
				TBItemType.Group => ERole.GROUPING,
				TBItemType.Menu => ERole.BUTTONMENU,
				_ => ERole.BUTTON
			};
		return (int)r - 1;
	}

	VarInt IAccessible.get_accState(VarInt varChild) {
		EState r = 0;
		if (!_w.IsEnabled()) r |= EState.DISABLED;
		if (!_B(varChild, out var b)) {
			if (!_w.IsVisible) r |= EState.INVISIBLE;
		} else {
			if (b.IsSeparatorOrGroup_) r |= EState.DISABLED;
			if (b.IsMenu_) r |= EState.HASPOPUP;
			//TODO3: if offscreen, r |= EState.INVISIBLE | EState.OFFSCREEN;
			//no: EState.HOTTRACKED;
		}
		return (int)r - 1;
	}

	string IAccessible.get_accHelp(VarInt varChild) => _B(varChild, out var b) ? _GetFullTooltip(b) : null;

	int IAccessible.get_accHelpTopic(out string pszHelpFile, VarInt varChild) => throw new NotImplementedException();

	string IAccessible.get_accKeyboardShortcut(VarInt varChild) => null;

	object IAccessible.get_accFocus() => null;

	object IAccessible.get_accSelection() => null;

	string IAccessible.get_accDefaultAction(VarInt varChild)
		=> !_B(varChild, out var b) || b.IsSeparatorOrGroup_ ? null : b.IsMenu_ ? "Open" : "Execute";

	void IAccessible.accSelect(ESelect flagsSelect, VarInt varChild) => throw new NotImplementedException();

	void IAccessible.accLocation(out int pxLeft, out int pyTop, out int pcxWidth, out int pcyHeight, VarInt varChild) {
		if (!_B(varChild, out var b)) {
			_StdAO.accLocation(out pxLeft, out pyTop, out pcxWidth, out pcyHeight, varChild);
		} else {
			var r = b.rect; _w.MapClientToScreen(ref r);
			pxLeft = r.left; pyTop = r.top; pcxWidth = r.Width; pcyHeight = r.Height;
		}
	}

	object IAccessible.accNavigate(NAVDIR navDir, VarInt varStart) {
		int i = varStart;
		var a = _a;
		if (navDir == NAVDIR.FIRSTCHILD || navDir == NAVDIR.LASTCHILD) {
			if (i == -1) return navDir == NAVDIR.FIRSTCHILD ? 1 : a.Count;
		} else {
			if (i == -1) return _StdAO.accNavigate(navDir, varStart);
			switch (navDir) {
			case NAVDIR.PREVIOUS:
				if (i > 0) return i;
				break;
			case NAVDIR.NEXT:
				if (++i < a.Count) return i + 1;
				break;
			}
		}
		return null;
	}

	VarInt IAccessible.accHitTest(int xLeft, int yTop) {
		POINT p = new(xLeft, yTop); _w.MapScreenToClient(ref p);
		if (!_w.ClientRect.Contains(p)) return _StdAO.accHitTest(xLeft, yTop);
		return _HitTest(p);
	}

	void IAccessible.accDoDefaultAction(VarInt varChild) {
		if (!_B(varChild, out var b) || b.IsSeparatorOrGroup_) return;
		_w.Post(Api.WM_USER + 50, (int)varChild);
	}

	void IAccessible.put_accName(VarInt varChild, string szName) { }

	void IAccessible.put_accValue(VarInt varChild, string szValue) { }

	bool _B(VarInt varChild, out TBItem b) {
		int i = varChild;
		if (i == -1) { b = null; return false; }
		b = _a[i]; return true;
	}
}