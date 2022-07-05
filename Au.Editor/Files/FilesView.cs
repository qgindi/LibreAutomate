using Au.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;

partial class FilesModel
{
	public class FilesView : KTreeView
	{
		public FilesView() {
			SetMultiSelect(toggle: false);
			AllowDrop = true;

			ItemActivated += _ItemActivated;
			ItemClick += _ItemClick;

			ItemDragStart += _ItemDragStart;

			FilesModel.NeedRedraw += v => { if (v.f != null) Redraw(v.f, v.remeasure); else Redraw(v.remeasure); };

			App.Commands.BindKeysTarget(this, "Files");
		}

		public void SetItems() {
			base.SetItems(App.Model.Root.Children());
		}

		public void SetMultiSelect(bool toggle) {
			bool multi = App.Settings.files_multiSelect;
			if (toggle) App.Settings.files_multiSelect = (multi ^= true);
			MultiSelect = multi;
			SingleClickActivate = !multi;
			App.Commands[nameof(Menus.File.CopyPaste.MultiSelect_files)].Checked = multi;
		}

		private void _ItemActivated(TVItemEventArgs e) {
			var f = e.Item as FileNode;
			if (f.IsFolder) return;
			var m = App.Model;
			if (e.ClickCount == 0 && f == m.CurrentFile) Panels.Editor.ZActiveDoc?.Focus(); //let Enter set focus = active doc
			else m._SetCurrentFile(f, focusEditor: e.ClickCount switch { 1 => null, 2 => true, _ => false });
		}

		private void _ItemClick(TVItemEventArgs e) {
			if (e.Mod != 0) return;
			var f = e.Item as FileNode;
			switch (e.Button) {
			case MouseButton.Right:
				Dispatcher.InvokeAsync(() => App.Model._ItemRightClicked(f));
				break;
			case MouseButton.Middle:
				if (!f.IsFolder) App.Model.CloseFile(f);
				break;
			}
		}

		protected override void OnKeyDown(KeyEventArgs e) {
			base.OnKeyDown(e);
			var m = App.Model;
			switch ((e.KeyboardDevice.Modifiers, e.Key)) {
			case (0, Key.Enter): m.OpenSelected(1); break;
			case (0, Key.Delete): m.DeleteSelected(); break;
			case (ModifierKeys.Control, Key.X): m.CutCopySelected(true); break;
			case (ModifierKeys.Control, Key.C): m.CutCopySelected(false); break;
			case (ModifierKeys.Control, Key.V): m.Paste(); break;
			case (0, Key.Escape): m.Uncut(); break;
			default: return;
			}
			e.Handled = true;
		}

		protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
			App.Model._ContextMenu();
		}

		public new FileNode[] SelectedItems => base.SelectedItems.Cast<FileNode>().ToArray();

		#region drag-drop

		private void _ItemDragStart(TVItemEventArgs e) {
			if (e.Button != MouseButton.Left) return;
			//if(e.Item.IsFolder && e.Item.IsExpanded) Expand(e.Index, false);
			var a = IsSelected(e.Index) ? SelectedItems : new FileNode[] { e.Item as FileNode };
			DragDropFiles = a;
			DragDrop.DoDragDrop(this, new DataObject(typeof(FileNode[]), a), DragDropEffects.Move | DragDropEffects.Copy);
			DragDropFiles = null;
		}

		public FileNode[] DragDropFiles { get; private set; }

		protected override void OnDragOver(DragEventArgs e) {
			e.Handled = true;
			bool can = _DragDrop(e, false);
			OnDragOver2(can);
			base.OnDragOver(e);
		}

		protected override void OnDrop(DragEventArgs e) {
			e.Handled = true;
			_DragDrop(e, true);
			base.OnDrop(e);
		}

		bool _DragDrop(DragEventArgs e, bool drop) {
			bool can;
			FileNode[] nodes = null;
			if (can = e.Data.GetDataPresent(typeof(FileNode[]))) {
				nodes = e.Data.GetData(typeof(FileNode[])) as FileNode[];
				GetDropInfo(out var d);
				if (d.targetItem is FileNode target) {
					bool no = false;
					foreach (FileNode v in nodes) {
						if (d.intoFolder) {
							no = v == target;
						} else {
							no = target.IsDescendantOf(v);
						}
						if (no) break;
					}
					can = !no;
				}
			} else {
				can = e.Data.GetDataPresent(DataFormats.FileDrop);
			}

			if (can) {
				//convert multiple effects to single
				switch (e.KeyStates & (DragDropKeyStates.ControlKey | DragDropKeyStates.ShiftKey)) {
				case DragDropKeyStates.ControlKey: e.Effects &= DragDropEffects.Copy; break;
				case DragDropKeyStates.ShiftKey: e.Effects &= DragDropEffects.Move; break;
				case DragDropKeyStates.ControlKey | DragDropKeyStates.ShiftKey: e.Effects &= DragDropEffects.Link; break;
				default:
					if (e.Effects.Has(DragDropEffects.Move)) e.Effects = DragDropEffects.Move;
					else if (e.Effects.Has(DragDropEffects.Link)) e.Effects = DragDropEffects.Link;
					else if (e.Effects.Has(DragDropEffects.Copy)) e.Effects = DragDropEffects.Copy;
					else e.Effects = 0;
					break;
				}
			} else {
				e.Effects = 0;
			}
			if (e.Effects == 0) return false;

			if (drop) {
				var files = nodes == null ? e.Data.GetData(DataFormats.FileDrop) as string[] : null;
				GetDropInfo(out var d);
				var pos = d.intoFolder ? FNPosition.Inside : (d.insertAfter ? FNPosition.After : FNPosition.Before);
				App.Model._DroppedOrPasted(nodes, files, e.Effects == DragDropEffects.Copy, d.targetItem as FileNode, pos);
			}
			return true;
		}

		#endregion
	}
}

//Tested Jdenticon library that generates random icons. Works well, but the icons are too abstract and monotonic.
//partial class FileNode
//{
//	System.Drawing.Bitmap ITreeViewItem.Image {
//		get {
//			if (_bmp == null && 0 != Name.Like(true, "Script14?.cs", "Script13?.cs")) {
//				var v = Identicon.FromValue(Name, size: 16);
//				v.Style = new IdenticonStyle { BackColor = Jdenticon.Rendering.Color.Transparent/*, ColorSaturation=0.7f, ColorLightness = new(0.1f, 0.5f)*/ };
//				using var stream = v.SaveAsPng();
//				_bmp = System.Drawing.Bitmap.FromStream(stream) as System.Drawing.Bitmap;
//			}
//			return _bmp;
//		}
//	}
//	System.Drawing.Bitmap _bmp;
//}
