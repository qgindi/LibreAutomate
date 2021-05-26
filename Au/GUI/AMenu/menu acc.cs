﻿using Au.Types;
using Au.Util;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using IAccessible = Au.Types.Api.IAccessible;
using VarInt = Au.Types.Api.VarInt;
using AccNAVDIR = Au.Types.Api.AccNAVDIR;

namespace Au
{
	[ComVisible(true)]
	partial class MTBase
	{
		private protected bool _WmGetobject(nint wParam, nint lParam, out nint result) {
			result = default;
			var oid = (AccOBJID)lParam;
			if (oid != AccOBJID.CLIENT) return false;
			result = Cpp.Cpp_AccWorkaround((IAccessible)this, wParam, ref _accWorkaround);
			return true;
		}
		nint _accWorkaround;

		///
		~MTBase() { if (_accWorkaround != 0) Cpp.Cpp_AccWorkaround(null, 0, ref _accWorkaround); }

		private protected IAccessible _StdAO {
			get {
				if (_stdAO == null) Api.CreateStdAccessibleObject(_w, AccOBJID.CLIENT, typeof(IAccessible).GUID, out _stdAO);
				return _stdAO;
			}
		}
		IAccessible _stdAO;
	}

	[ComVisible(true)]
	partial class AMenu : IAccessible
	{
		IAccessible IAccessible.get_accParent() => _StdAO.get_accParent();

		int IAccessible.get_accChildCount() => _a.Count;

		int IAccessible.get_accChild(VarInt varChild, out object ppdispChild) { ppdispChild = null; return 1; }

		string IAccessible.get_accName(VarInt varChild)
			=> !_B(varChild, out var b) ? _name : (b.rawText ? b.Text : AStringUtil.RemoveUnderlineChar(b.Text));

		string IAccessible.get_accValue(VarInt varChild) => null;

		string IAccessible.get_accDescription(VarInt varChild) => _B(varChild, out _) ? null : "Popup menu";

		VarInt IAccessible.get_accRole(VarInt varChild) {
			var r = !_B(varChild, out var b) ? AccROLE.MENUPOPUP : (b.IsSeparator ? AccROLE.SEPARATOR : AccROLE.MENUITEM);
			return (int)r - 1;
		}

		VarInt IAccessible.get_accState(VarInt varChild) {
			AccSTATE r = 0;
			if (!_w.IsEnabled()) r |= AccSTATE.DISABLED;
			if (!_B(varChild, out var b)) {
				if (!_w.IsVisible) r |= AccSTATE.INVISIBLE;
			} else {
				if (b.IsDisabled) r |= AccSTATE.DISABLED;
				if (FocusedItem == b) r |= AccSTATE.FOCUSED | AccSTATE.HOTTRACKED;
				if (b.IsChecked) r |= AccSTATE.CHECKED;
				if (b.IsSubmenu) r |= AccSTATE.HASPOPUP;
				//SHOULDDO: if offscreen, r |= AccSTATE.INVISIBLE | AccSTATE.OFFSCREEN;
			}
			return (int)r - 1;
		}

		string IAccessible.get_accHelp(VarInt varChild) => _B(varChild, out var b) ? _GetFullTooltip(b) : null;

		int IAccessible.get_accHelpTopic(out string pszHelpFile, VarInt varChild) => throw new NotImplementedException();

		string IAccessible.get_accKeyboardShortcut(VarInt varChild) {
			if (_B(varChild, out var b) && !b.rawText) {
				var s = b.Text;
				int i = AStringUtil.FindUnderlineChar(s);
				if (i >= 0) return s[i].ToString();
			}
			return null;
		}

		object IAccessible.get_accFocus() => null;

		object IAccessible.get_accSelection() => null;

		string IAccessible.get_accDefaultAction(VarInt varChild) => _B(varChild, out var b) && b.clicked != null ? (b.IsSubmenu ? "Open" : "Execute") : null;

		void IAccessible.accSelect(AccSELFLAG flagsSelect, VarInt varChild) => throw new NotImplementedException();

		void IAccessible.accLocation(out int pxLeft, out int pyTop, out int pcxWidth, out int pcyHeight, VarInt varChild) {
			if (!_B(varChild, out var b)) {
				_StdAO.accLocation(out pxLeft, out pyTop, out pcxWidth, out pcyHeight, varChild);
			} else {
				var r = _ItemRect(b, inScreen: true);
				pxLeft = r.left; pyTop = r.top; pcxWidth = r.Width; pcyHeight = r.Height;
			}
		}

		object IAccessible.accNavigate(AccNAVDIR navDir, VarInt varStart) {
			int i = varStart;
			if (navDir == AccNAVDIR.FIRSTCHILD || navDir == AccNAVDIR.LASTCHILD) {
				if (i == -1) return navDir == AccNAVDIR.FIRSTCHILD ? 1 : _a.Count;
			} else {
				if (i == -1) return _StdAO.accNavigate(navDir, varStart);
				switch (navDir) {
				case AccNAVDIR.PREVIOUS:
					if (i > 0) return i;
					break;
				case AccNAVDIR.NEXT:
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
			if (!_B(varChild, out var b) || b.clicked == null) return;
			_w.Post(Api.WM_USER + 50, (int)varChild);
		}

		void IAccessible.put_accName(VarInt varChild, string szName) => throw new NotImplementedException();

		void IAccessible.put_accValue(VarInt varChild, string szValue) => throw new NotImplementedException();

		bool _B(VarInt varChild, out AMenu.MenuItem b) {
			int i = varChild;
			if (i == -1) { b = null; return false; }
			b = _a[i]; return true;
		}
	}
}