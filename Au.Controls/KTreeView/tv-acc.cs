//Implements MSAA IAccessible.

//Problem: UIA "object from point" returns CLIENT, not item, although has items in tree. MSAA works well.
//	For UIA to work correctly, need to implement UIA (AutomationPeer for control and items).
//		But then problem: if not implemented MSAA, MSAA "object from point" does not return item, although has items in tree.
//			If implemented both, tree contains duplicate subtrees; example - SysListView32 control.
//		Also, cannot implement it directly for HwndHost, because WPF then throws exception. But can implement for its parent.
//	I did not find a way to make both UIA and MSAA work correctly with WPF HwndHost.
//	Good: NVDA now works anyway.

//Strange: QM2 does not find items and client unless checked +invisible, although no invisible state. Never mind.

using NAVDIR = Au.Types.Api.NAVDIR;

namespace Au.Controls;

public partial class KTreeView {
	class _Accessible : HwndHostAccessibleBase_ {
		KTreeView _tv;

		internal _Accessible(KTreeView tv) : base(tv, tv.Hwnd) {
			_tv = tv;
		}

		ITreeViewItem _Item(int child) {
			if (child == -1) return null;
			if ((uint)child < _tv.CountVisible) return _tv._avi[child].item;
			throw new ArgumentException();
		}

		public override int ChildCount => _tv.CountVisible;

		public override string Name(int child) {
			var k = _Item(child);
			if (k == null) return _tv.Name;
			return k.DisplayText;
		}

		public override ERole Role(int child) {
			return child == -1 ? ERole.TREE : ERole.TREEITEM;
		}

		public override EState State(int child) {
			if (child == -1) return base.State(child);
			var k = _Item(child);
			EState r = 0;
			if (_tv.Focusable) {
				r |= EState.FOCUSABLE;
				if (child == _tv._focusedIndex && _tv.IsKeyboardFocused) r |= EState.FOCUSED;
			}
			if (k.IsSelectable) {
				r |= EState.SELECTABLE;
				if (_tv.MultiSelect) r |= EState.MULTISELECTABLE;
				if (_tv.IsSelected(child)) r |= EState.SELECTED;
			}
			if (k.IsFolder) r |= k.IsExpanded ? EState.EXPANDED : EState.COLLAPSED;
			var (from, to) = _tv._GetViewRange(); if (child < from || child >= to) r |= EState.INVISIBLE | EState.OFFSCREEN;
			if (k.IsDisabled) r |= EState.DISABLED;
			switch (k.CheckState) { case TVCheck.Checked: case TVCheck.RadioChecked: r |= EState.CHECKED; break; case TVCheck.Mixed: r |= EState.MIXED; break; }
			//if(child==_tv._hotIndex) r|=EState.HOTTRACKED;
			//if(!k.IsEditable) r|=EState.READONLY;
			return r;
		}

		public override int FocusedChild => _tv._focusedIndex;

		public override List<int> SelectedChildren => _tv.SelectedIndices;

		public override string DefaultAction(int child) {
			var k = _Item(child);
			if (k == null) return null;
			if (k.IsFolder) return k.IsExpanded ? "Collapse" : "Expand";
			return "Activate";
		}

		public override void SelectChild(ESelect flagsSelect, int child) {
			//if(flagsSelect.HasAny(ESelect.ADDSELECTION|ESelect.EXTENDSELECTION) && !_tv.MultiSelect) throw new InvalidOperationException();
			//int anchor=Math.Max(_tv.FocusedIndex, 0);
			switch (flagsSelect & (ESelect.TAKESELECTION | ESelect.ADDSELECTION | ESelect.EXTENDSELECTION | ESelect.REMOVESELECTION)) {
			case 0: break;
			case ESelect.TAKESELECTION:
				_tv.SelectSingle(child, andFocus: false);
				break;
			case ESelect.REMOVESELECTION:
				_tv.Select(child, false);
				break;
			case ESelect.ADDSELECTION when _tv.MultiSelect:
				_tv.Select(child, true);
				break;
			//case ESelect.EXTENDSELECTION: //rarely used
			//case ESelect.ADDSELECTION|ESelect.EXTENDSELECTION:
			//	break;
			//case ESelect.REMOVESELECTION|ESelect.EXTENDSELECTION:
			//	break;
			default: throw new ArgumentException();
			}
			if (flagsSelect.Has(ESelect.TAKEFOCUS)) {
				//_tv.Focus();
				_tv.SetFocusedItem(child);
			}
		}

		public override RECT ChildRect(int child) => _tv.GetRectPhysical(child);

		public override int? Navigate(NAVDIR navDir, int childStart) {
			if (childStart == -1) { //navDir can be only first or last
				int n = _tv.CountVisible; if (n == 0) return null;
				return navDir == NAVDIR.FIRSTCHILD ? 0 : n - 1;
			} else { //navDir cannot be first or last
				switch (navDir) {
				case NAVDIR.PREVIOUS:
				case NAVDIR.UP:
					if (childStart > 0) return childStart - 1;
					break;
				case NAVDIR.NEXT:
				case NAVDIR.DOWN:
					if (++childStart < _tv.CountVisible) return childStart;
					break;
				}
				return null;
			}
		}

		public override int HitTest(int x, int y) => _tv._ItemFromY(y);

		public override void DoDefaultAction(int child) {
			var k = _Item(child);
			if (k == null) return;
			if (k.IsFolder) _tv.Expand(child, null);
			else {
				_tv.SelectSingle(child, andFocus: true);
				_tv.ItemActivated?.Invoke(new(k, child));
			}
		}
	}
}