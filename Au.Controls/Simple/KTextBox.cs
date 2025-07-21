using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Au.Controls;

/// <summary>
/// Adds some features:
/// <br/>• Changes caret position when clicked left margin.
/// <br/>• Clears on middle-click.
/// <br/>• Can disable horizontal scrollbar when not focused.
/// </summary>
public class KTextBox : TextBox {
	/// <summary>
	/// Disables horizontal scrollbar when not focused.
	/// </summary>
	public bool Small {
		get => _small;
		set {
			if (_small) throw new InvalidOperationException();
			_small = true;
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		}
	}
	bool _small;

	protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) {
		if (_small) HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
		base.OnGotKeyboardFocus(e);
	}

	protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) {
		if (_small) HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		base.OnLostKeyboardFocus(e);
	}
	
	protected override void OnMouseDown(MouseButtonEventArgs e) {
		//Workaround for this nasty default behavior of TextBox: does not set the caret position when clicked the left padding area.
		//	Then difficult to set the caret position eg at the start, because the width of the sensitive area is 0.5 of the width of the first character (eg 2 pixels if `i`).
		//	Classic Edit controls etc don't have this problem. Users often click the padding area to move the caret at the start, but in WPF TextBox it does not work.
		if (e.ChangedButton == MouseButton.Left && e.OriginalSource is Grid g && e.GetPosition(g).X < Padding.Left + 3) {
			var p = e.GetPosition(this);
			int i = base.GetCharacterIndexFromPoint(p, true);
			if (i >= 0) this.CaretIndex = i;
			//never mind: cursor not I-beam.
			//never mind: can't drag-select starting from the padding area.
		}
		if (e.ChangedButton == MouseButton.Middle) {
			Clear();
		}
		base.OnMouseDown(e);
	}
}

/// <summary>
/// TextBox for a file or folder path.
/// Supports drag-drop and Browse dialog (right-click).
/// </summary>
public class KTextBoxFile : TextBox {
	bool _canDrop;
	
	///
	protected override void OnPreviewDragEnter(DragEventArgs e) {
		if (e.Handled = _canDrop = e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effects = DragDropEffects.Link;
		base.OnPreviewDragEnter(e);
	}
	
	///
	protected override void OnPreviewDragOver(DragEventArgs e) {
		if (e.Handled = _canDrop) e.Effects = DragDropEffects.Link;
		base.OnPreviewDragEnter(e);
	}
	
	///
	protected override void OnDrop(DragEventArgs e) {
		if (e.Data.GetData(DataFormats.FileDrop) is string[] a && a.Length > 0) {
			var s = a[0];
			if (s.Ends(".lnk", true) && filesystem.exists(s).File)
				try { s = shortcutFile.getTarget(s); }
				catch (Exception) { }
			Text = s;
		}
		base.OnDrop(e);
	}
	
	///
	protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
		var m = new popupMenu { CheckDontClose = true };
		
		m["Browse..."] = o => {
			var d = new FileOpenSaveDialog(ClientGuid ?? (IsFolder ? "3d4a9167-929a-4346-adfb-e2f03427412c" : "6a7d02c0-7f98-4808-b764-84985ca6e767"));
			if (d.ShowOpen(out string s, owner: this.Hwnd(), selectFolder: IsFolder)) _SetText(s);
		};
		m["folders.EditMe + @\"EditMe\""] = o => _SetText(o.Text);
		m.Submenu("Known folders", m => {
			foreach (var v in typeof(folders).GetProperties()) {
				bool nac = folders.noAutoCreate;
				folders.noAutoCreate = true;
				string path = v.GetValue(null) switch { string k => k, FolderPath k => k.Path, _ => null };
				folders.noAutoCreate = nac;
				if (path == null) continue;
				var name = v.Name;
				m[$"{name}\t{path.Limit(80, middle: true)}"] = o => _SetText(Unexpand ? $"folders.{name} + @\"EditMe\"" : path + (path.Ends('\\') ? "" : "\\"));
			}
		});
		m.Submenu("Environment variables", m => {
			foreach (var (name, path) in Environment.GetEnvironmentVariables().OfType<System.Collections.DictionaryEntry>().Select(o => (o.Key as string, o.Value as string)).OrderBy(o => o.Item1)) {
				if (!pathname.isFullPath(path, orEnvVar: true)) continue;
				int i = path.IndexOf(';'); if (i > 0 && pathname.isFullPath(path.AsSpan(i + 1), orEnvVar: true)) continue; //list of paths
				if (IsFolder && filesystem.exists(path).File) continue;
				m[$"{name}\t{path.Limit(80, middle: true)}"] = o => _SetText(Unexpand ? $"%{name}%\\" : pathname.expand(path));
			}
		});
		m.Submenu("Folder windows", m => {
			foreach (var v in ExplorerFolder.All(onlyFilesystem: true).Select(o => o.GetFolderPath())) {
				m[v.Limit(100, middle: true)] = o => _SetText(v);
			}
		});
		m.Separator();
		m.AddCheck("Unexpand", Unexpand, o => { Unexpand ^= true; UnexpandChanged?.Invoke(); });
		
		void _SetText(string s) {
			Text = s;
			CaretIndex = s.Length;
		}
		
		m.Show(owner: this.Hwnd());
		e.Handled = true;
	}
	
	/// <summary>
	/// true if this control is for a folder path.
	/// </summary>
	public bool IsFolder { get; set; }
	
	/// <summary>
	/// For <see cref="FileOpenSaveDialog"/>.
	/// If not set, uses 2 different GUIDs: one for folders (see <see cref="IsFolder"/>), other for files.
	/// </summary>
	public string ClientGuid { get; set; }
	
	/// <summary>
	/// Let <b>GetCode</b> unexpand path.
	/// Also used/changed by the context menu.
	/// Default true.
	/// </summary>
	public bool Unexpand { get; set; } = true;
	
	public event Action UnexpandChanged;
	
	/// <summary>
	/// Formats code like '@"path"' or 'folders.X + @"relative"' or 'folders.X'.
	/// </summary>
	public string GetCode() {
		var s = Text;
		if (s.Contains('"') || s.Starts("folders.")) return s;
		if (Unexpand && folders.unexpandPath(s, out var s1, out var s2)) return s2.NE() ? s1 : $"{s1} + @\"{s2}\"";
		return $"@\"{s}\"";
	}
}
