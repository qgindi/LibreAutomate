using System.Windows.Controls;

partial class PanelFiles : DockPanel
{
	FilesModel.FilesView _tv;

	public PanelFiles() {
		//this.UiaSetName("Files panel"); //no UIA element for Panel. Use this in the future if this panel will be : UserControl.

		_tv = new() { Name = "Files_list" };
		this.Children.Add(_tv);
	}

	public FilesModel.FilesView TreeControl => _tv;
}
