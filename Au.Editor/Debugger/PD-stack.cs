using Au.Controls;

partial class PanelDebug {
	KTreeView _tvStack;
	_StackViewItem[] _aStack;
	
	void _StackViewInit() {
		_tvStack = new() { Name = "Callstack_list", SingleClickActivate = true };
		_tvStack.ItemActivated += _tvStack_ItemActivated;
	}
	
	void _StackViewSetItems(_FRAME[] a) {
		if (a == null) {
			_aStack = null;
		} else {
			_aStack = new _StackViewItem[a.Length];
			for (int i = 0; i < a.Length; i++) {
				_aStack[i] = new(this, a[i]);
			}
		}
		
		_tvStack.SetItems(_aStack);
		if (a != null) _tvStack.SelectSingle(0, andFocus: true);
	}
	
	void _tvStack_ItemActivated(TVItemEventArgs e) {
		if (!IsStopped) return;
		var v = e.Item as _StackViewItem;
		_s.frame = v.f;
		_GoToLine(v.f, keepMarkers: true);
		_VariablesViewChangedFrameOrThread();
		_ListVariables();
	}
	
	class _StackViewItem : ITreeViewItem {
		readonly PanelDebug _panel;
		readonly public _FRAME f;
		readonly string _text;
		
		public _StackViewItem(PanelDebug panel, _FRAME f) {
			_panel = panel;
			this.f = f;
			_text = _panel._FormatFrameString(f);
		}
		
		//ITreeViewItem
		
		string ITreeViewItem.DisplayText => _text;
		
		TVParts ITreeViewItem.NoParts => TVParts.Image;
		
		//public bool IsDisabled => f.file.NE();
		
		//bool ITreeViewItem.IsSelectable => !IsDisabled;
		
		int ITreeViewItem.TextColor(TVColorInfo ci) => f.file.NE() ? 0x808080 : -1;
	}
}
