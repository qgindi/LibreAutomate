using System.Windows;
using System.Windows.Controls;
using Au.Controls;

partial class FilesModel
{
	class DNewWorkspace : KDialogWindow
	{
		string _name, _location;
		public string ResultPath { get; private set; }

		public DNewWorkspace(string name, string location) {
			_name = name;
			_location = location;

			Title = "New Workspace";

			var b = new wpfBuilder(this).WinSize(600);
			TextBox tName, tLocation = null;
			b.R.Add("Folder name", out tName, _name).Validation(_Validate);
			b.R.Add("Parent folder", out tLocation, _location).Validation(_Validate);
			b.R.AddButton("Browse...", _Browse).Width(70).Align("L");
			b.R.AddOkCancel();
			b.End();

			void _Browse(WBButtonClickArgs e) {
				var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDA}") {
					InitFolderNow = filesystem.exists(tLocation.Text).isDir ? tLocation.Text : folders.ThisAppDocuments,
				};
				if (d.ShowOpen(out string s, this, selectFolder: true)) tLocation.Text = s;
			}

			string _Validate(FrameworkElement e) {
				var s = (e as TextBox).Text;
				if (e == tLocation) {
					if (!filesystem.exists(s).isDir) return "Folder does not exist";
				} else {
					if (pathname.isInvalidName(s)) return "Invalid filename";
					ResultPath = pathname.combine(tLocation.Text, s); //validation is when OK clicked
					if (filesystem.exists(ResultPath)) return s + " already exists";
				}
				return null;
			}

			//b.OkApply += e => {
			//	print.it(ResultPath); e.Cancel = true;
			//};
		}
	}
}
