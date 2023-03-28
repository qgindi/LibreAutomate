using Au.Controls;
using System.Windows.Controls;
using System.Windows.Input;

//TODO: Ctrl+Tab stops working if current document closed from Open or Tasks panel. Starts working when something focused.

class PanelOpen {
	KTreeView _tv;
	bool _updatedOnce;
	bool _closing;

	public PanelOpen() {
		//P.UiaSetName("Open panel"); //no UIA element for Panel

		_tv = new KTreeView { Name = "Open_list" };
		P.Children.Add(_tv);
	}

	public DockPanel P { get; } = new();

	public KTreeView TreeControl => _tv;

	public void UpdateList() {
		var e = App.Model.OpenFiles.Select(o => new _Item { f = o });
		if (_Sort) e = e.OrderBy(o => o.f.Name, StringComparer.OrdinalIgnoreCase)
				.ThenBy(o => o.f.Id); //to make stable when there are multiple files with same name
		_tv.SetItems(e, _updatedOnce);
		_SelectCurrent(true);
		if (!_updatedOnce) {
			_updatedOnce = true;
			FilesModel.NeedRedraw += v => {
				if (v.f != null && !App.Model.OpenFiles.Contains(v.f)) return;
				if (v.renamed && _Sort) UpdateList();
				else _tv.Redraw(v.remeasure);
			};
			_tv.ItemClick += _tv_ItemClick;
			//_tv.ContextMenuOpening += (_,_) => //never mind
		}
	}

	private void _tv_ItemClick(TVItemEventArgs e) {
		if (e.Mod != 0 || e.ClickCount != 1) return;
		var f = (e.Item as _Item).f;
		switch (e.Button) {
		case MouseButton.Left:
			App.Model.SetCurrentFile(f);
			break;
		case MouseButton.Right:
			_tv.SelectSingle(e.Item, andFocus: false);
			_ContextMenu(f);
			break;
		case MouseButton.Middle:
			_CloseFile(f);
			break;
		}
	}

	void _CloseFile(FileNode f) {
		_closing = true; //prevent scrolling to top when closing an item near the bottom
		App.Model.CloseFile(f, selectOther: true);
		_closing = false;
	}

	void _ContextMenu(FileNode f) {
		var m = new popupMenu();
		m.Add(1, "Close\tM-click");
		m.Add(2, "Close all other");
		m.Add(3, "Close all");
		m.Separator();
		m.AddCheck("Sort alphabetically", _Sort).Id = 4;
		var r = m.Show();
		switch (r) {
		case 1:
			_CloseFile(f);
			return;
		case 2:
			App.Model.CloseEtc(FilesModel.ECloseCmd.CloseAll, dontClose: f);
			return;
		case 3:
			App.Model.CloseEtc(FilesModel.ECloseCmd.CloseAll);
			return;
		case 4:
			App.Settings.openFiles_flags ^= 1;
			UpdateList();
			return;
		}
		_SelectCurrent(false);
	}

	void _SelectCurrent(bool focus) {
		if (_tv.CountVisible == 0) return;
		int i = _Sort ? _tv.IndexOf(o => (o as _Item).f == App.Model.CurrentFile) : 0;
		if (focus) _tv.SetFocusedItem(i, _closing ? 0 : TVFocus.EnsureVisible);
		_tv.SelectSingle(i, andFocus: false);
	}

	static bool _Sort => 0 != (1 & App.Settings.openFiles_flags);

	class _Item : ITreeViewItem {
		public FileNode f;

		#region ITreeViewItem

		string ITreeViewItem.DisplayText => f.DisplayName;

		object ITreeViewItem.Image => f.Image;

		#endregion
	}
}
