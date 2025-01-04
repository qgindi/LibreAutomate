/// <link https://epplussoftware.com/>EPPlus<> is a library for reading/writing/etc Excel files (<_>.xlsx, .xlsm</_>). Free for non-commercial. NuGet: <+nuget>EPPlus<>.

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
