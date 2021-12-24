using Au.Controls;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;

class PanelOpen : DockPanel
{
	KTreeView _tv;
	bool _updatedOnce;

	public PanelOpen() {
		//this.UiaSetName("Open panel"); //no UIA element for Panel. Use this in the future if this panel will be : UserControl.

		_tv = new KTreeView { Name = "Open_list" };
		this.Children.Add(_tv);
	}

	public void ZUpdateList() {
		//_tv.SetItems(App.Model.OpenFiles, _updatedOnce); //this would be ok, but displays yellow etc
		var a = App.Model.OpenFiles;
		_tv.SetItems(a.Select(o => new _Item { f = o }), _updatedOnce);
		if (a.Count > 0) _tv.SelectSingle(0, andFocus: true);
		if (!_updatedOnce) {
			_updatedOnce = true;
			FilesModel.NeedRedraw += v => { _tv.Redraw(v.remeasure); };
			_tv.ItemClick += _tv_ItemClick;
			//_tv.ContextMenuOpening += (_,_) => //never mind
		}
	}

	private void _tv_ItemClick(object sender, TVItemEventArgs e) {
		if (e.ModifierKeys != 0 || e.ClickCount != 1) return;
		var f = (e.Item as _Item).f;
		switch (e.MouseButton) {
		case MouseButton.Left:
			App.Model.SetCurrentFile(f);
			break;
		case MouseButton.Right:
			_tv.Select(e.Item);
			switch (popupMenu.showSimple("Close\tM-click|Close all other|Close all")) {
			case 1:
				_CloseFile();
				break;
			case 2:
				App.Model.CloseEtc(FilesModel.ECloseCmd.CloseAll, dontClose: f);
				break;
			case 3:
				App.Model.CloseEtc(FilesModel.ECloseCmd.CloseAll);
				break;
			}
			break;
		case MouseButton.Middle:
			_CloseFile();
			break;
		}

		void _CloseFile() {
			App.Model.CloseFile(f, selectOther: true);
		}
	}

	class _Item : ITreeViewItem
	{
		public FileNode f;

		#region ITreeViewItem

		string ITreeViewItem.DisplayText => f.DisplayName;

		string ITreeViewItem.ImageSource => f.ImageSource;

		#endregion
	}
}
