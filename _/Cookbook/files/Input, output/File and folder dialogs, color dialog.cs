/// Simplest "Open" dialog. <help Au.More.FileOpenSaveDialog>More info<>.

var dOpen = new FileOpenSaveDialog();
if (!dOpen.ShowOpen(out string path1)) return;
print.it(path1);
//var text = File.ReadAllText(path1);

/// Set some properties.

var dOpenP = new FileOpenSaveDialog {
	Title = "Select a file",
	InitFolderNow = @"C:\",
	FileTypes = "Text files|*.txt|Office files|*.doc;*.xls|All files|*.*",
};
if (!dOpenP.ShowOpen(out string path2)) return;
print.it(path2);

/// Use GUID to remember the last used folder.

var dOpenG = new FileOpenSaveDialog("12f59d11-db07-4a16-89b6-a5a120c4cd48") {
	InitFolderFirstTime = folders.Documents
};
if (!dOpenG.ShowOpen(out string path3)) return;
print.it(path3);

/// Create GUID.

print.it(Guid.NewGuid());

/// Select multiple files.

var dOpenM = new FileOpenSaveDialog();
if (!dOpenM.ShowOpen(out string[] paths)) return;
foreach (var path in paths) {
	print.it(path);
}

/// Select folder.

var dFolder = new FileOpenSaveDialog();
if (!dFolder.ShowOpen(out string folderPath, selectFolder: true)) return;
print.it(folderPath);

/// "Save As" dialog.

var initFolder = folders.ThisAppDocuments;
var dSave = new FileOpenSaveDialog { FileTypes = "Text files|*.txt", InitFolderNow = initFolder };
if (!dSave.ShowSave(out string path4)) return;
print.it(path4);
//File.WriteAllText(path4, "TEXT");

/// The <b>System.Windows.Forms</b> namespace has common dialog classes <b>ColorDialog</b>, <b>FontDialog</b>, etc.

var d = new System.Windows.Forms.ColorDialog { FullOpen = true };
if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
//if (d.ShowDialog(b.Window.FormOwner()) != System.Windows.Forms.DialogResult.OK) return; //set owner = WPF Window created with wpfBuilder b
print.it(d.Color, (ColorInt)d.Color);
