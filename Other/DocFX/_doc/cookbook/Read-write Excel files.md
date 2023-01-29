# Read-write Excel files
<a href='https://epplussoftware.com/'>EPPlus</a> is a library for reading/writing/etc Excel files (.xlsx, .xlsm). Free for non-commercial. NuGet: <u title='Paste the underlined text in menu -> Tools -> NuGet'>EPPlus</u>.

```csharp
/*/ nuget -\EPPlus; /*/
using OfficeOpenXml;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

string file = folders.Downloads + "Financial Sample.xlsx";
using(var package = new ExcelPackage(new FileInfo(file))) {
	var sheet = package.Workbook.Worksheets[0];
	var s = sheet.Cells["A1"].Value;
	print.it(s);
	sheet.Cells["A1"].Value = "new value";
	package.Save();
}
```

