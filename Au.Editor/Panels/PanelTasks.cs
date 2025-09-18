using Au.Controls;
using System.Windows.Controls;
using System.Windows.Input;

namespace LA;

class PanelTasks {
	KTreeView _tv;
	bool _updatedOnce;

	public PanelTasks() {
		//P.UiaSetName("Tasks panel"); //no UIA element for Panel

		_tv = new KTreeView { Name = "Tasks_list" };
		P.Children.Add(_tv);
		
		Panels.PanelManager["Tasks"].DontActivateFloating = e => true;
	}

	public DockPanel P { get; } = new();

	public void UpdateList() {
		_tv.SetItems(App.Tasks.Items, _updatedOnce);
		if (!_updatedOnce) {
			_updatedOnce = true;
			FilesModel.NeedRedraw += v => { _tv.Redraw(v.remeasure); };
			_tv.ItemClick += _tv_ItemClick;
		}
	}

	private void _tv_ItemClick(TVItemEventArgs e) {
		if (e.Mod != 0 || e.ClickCount != 1) return;
		var t = e.Item as RunningTask;
		var f = t.f;
		switch (e.Button) {
		case MouseButton.Left:
			App.Model.SetCurrentFile(f);
			break;
		case MouseButton.Right:
			_tv.Select(t);
			var name = f.DisplayName;
			var m = new popupMenu { RawText = true };
			m["End task  '" + name + "'"] = _ => App.Tasks.EndTask(t);
			m["End all  '" + name + "'"] = _ => App.Tasks.EndTasksOf(f);
			m.Separator();
			m["Attach debugger"] = _ => Panels.Debug.Attach(t.processId);
			m.Separator();
			m["Close\tM-click", disable: f.OpenDoc == null] = _ => App.Model.CloseFile(f, selectOther: true);
			//m.Separator();
			//m["Recent tasks and triggers..."] = _ => RecentTT.Show(); //rejected. It is in menu Run. Or would also need to show context menu when rclicked in empty space.
			m.Show();
			break;
		case MouseButton.Middle:
			App.Model.CloseFile(f, selectOther: true);
			break;
		}
	}
}
