
using IAccessible = Au.Types.Api.IAccessible;
using VarInt = Au.Types.Api.VarInt;
using NAVDIR = Au.Types.Api.NAVDIR;

namespace Au.Types {
	[ComVisible(true)]
	partial class MTBase {
		private protected bool _WmGetobject(nint wParam, nint lParam, out nint result) {
			result = default;
			var oid = (EObjid)lParam;
			if (oid != EObjid.CLIENT) return false;
			result = Cpp.Cpp_AccWorkaround((IAccessible)this, wParam, ref _accWorkaround);
			return true;
		}
		nint _accWorkaround;

		///
		~MTBase() { if (_accWorkaround != 0) Cpp.Cpp_AccWorkaround(null, 0, ref _accWorkaround); }

		private protected IAccessible _StdAO {
			get {
				if (_stdAO == null) Api.CreateStdAccessibleObject(_w, EObjid.CLIENT, typeof(IAccessible).GUID, out _stdAO);
				return _stdAO;
			}
		}
		IAccessible _stdAO;
	}
}

namespace Au {
	[ComVisible(true)]
	partial class popupMenu : IAccessible {
		IAccessible IAccessible.get_accParent() => _StdAO.get_accParent();

		int IAccessible.get_accChildCount() => _a.Count;

		int IAccessible.get_accChild(VarInt varChild, out object ppdispChild) { ppdispChild = null; return 1; }

		string IAccessible.get_accName(VarInt varChild)
			=> !_B(varChild, out var b) ? _name : (b.rawText ? b.Text : StringUtil.RemoveUnderlineChar(b.Text));

		string IAccessible.get_accValue(VarInt varChild) => null;

		string IAccessible.get_accDescription(VarInt varChild) => _B(varChild, out _) ? null : "Popup menu";

		VarInt IAccessible.get_accRole(VarInt varChild) {
			var r = !_B(varChild, out var b) ? ERole.MENUPOPUP : (b.IsSeparator ? ERole.SEPARATOR : ERole.MENUITEM);
			return (int)r - 1;
		}

		VarInt IAccessible.get_accState(VarInt varChild) {
			EState r = 0;
			if (!_w.IsEnabled()) r |= EState.DISABLED;
			if (!_B(varChild, out var b)) {
				if (!_w.IsVisible) r |= EState.INVISIBLE;
			} else {
				if (b.IsDisabled) r |= EState.DISABLED;
				if (FocusedItem == b) r |= EState.FOCUSED | EState.HOTTRACKED;
				if (b.IsChecked) r |= EState.CHECKED;
				if (b.IsSubmenu) r |= EState.HASPOPUP;
				//TODO3: if offscreen, r |= EState.INVISIBLE | EState.OFFSCREEN;
			}
			return (int)r - 1;
		}

		string IAccessible.get_accHelp(VarInt varChild) => _B(varChild, out var b) ? _GetFullTooltip(b) : null;

		int IAccessible.get_accHelpTopic(out string pszHelpFile, VarInt varChild) => throw new NotImplementedException();

		string IAccessible.get_accKeyboardShortcut(VarInt varChild) {
			if (_B(varChild, out var b) && !b.rawText) {
				var s = b.Text;
				int i = StringUtil.FindUnderlineChar(s);
				if (i >= 0) return s[i].ToString();
			}
			return null;
		}

		object IAccessible.get_accFocus() => null;

		object IAccessible.get_accSelection() => null;

		string IAccessible.get_accDefaultAction(VarInt varChild)
			=> !_B(varChild, out var b) || b.IsDisabled ? null : b.IsSubmenu ? "Open" : "Execute";

		void IAccessible.accSelect(ESelect flagsSelect, VarInt varChild) => throw new NotImplementedException();

		void IAccessible.accLocation(out int pxLeft, out int pyTop, out int pcxWidth, out int pcyHeight, VarInt varChild) {
			if (!_B(varChild, out var b)) {
				_StdAO.accLocation(out pxLeft, out pyTop, out pcxWidth, out pcyHeight, varChild);
			} else {
				var r = _ItemRect(b, inScreen: true);
				pxLeft = r.left; pyTop = r.top; pcxWidth = r.Width; pcyHeight = r.Height;
			}
		}

		object IAccessible.accNavigate(NAVDIR navDir, VarInt varStart) {
			int i = varStart;
			if (navDir == NAVDIR.FIRSTCHILD || navDir == NAVDIR.LASTCHILD) {
				if (i == -1) return navDir == NAVDIR.FIRSTCHILD ? 1 : _a.Count;
			} else {
				if (i == -1) return _StdAO.accNavigate(navDir, varStart);
				switch (navDir) {
				case NAVDIR.PREVIOUS:
					if (i > 0) return i;
					break;
				case NAVDIR.NEXT:
					if (++i < _a.Count) return i + 1;
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
			if (!_B(varChild, out var b) || b.IsDisabled) return;
			_w.Post(Api.WM_USER + 50, (int)varChild);
		}

		void IAccessible.put_accName(VarInt varChild, string szName) => throw new NotImplementedException();

		void IAccessible.put_accValue(VarInt varChild, string szValue) => throw new NotImplementedException();

		bool _B(VarInt varChild, out PMItem b) {
			int i = varChild;
			if (i == -1) { b = null; return false; }
			b = _a[i]; return true;
		}
	}
}