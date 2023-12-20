/// Microsoft Excel has powerful automation API. It can be used in Excel macros and other apps/scripts if Excel is installed (Microsoft 365 or Office).
/// Setup: recipe <+recipe>ExcelExt<>.

/*/ c ExcelExt.cs; /*/
using Excel = Microsoft.Office.Interop.Excel;

try {
#if true //automate an existing Excel window
	var wExcel = wnd.find(0, "*- Excel");
	var book = wExcel.GetExcelWorkbook();
#else //start new Excel process
	var app = new Excel.Application();
	var book = app.Workbooks.Add(); //create new workbook
	//var book = app.Workbooks.Open(folders.Documents + @"Book1.xlsx"); //or open
	app.Visible = true;
#endif
	
	//then use Excel API
	Excel.Worksheet sheet = book.ActiveSheet;
	//...
	
	//app.Quit();
}
catch (Exception e1) { print.it(e1); }

/// The try-catch is important. After an unhandled exception Excel process would not exit when its window closed (you can see it in Task Manager).

/// On the internet you can find <google Microsoft.Office.Interop.Excel namespace>Excel API documentation</google>, many code examples and other info.

/// The API is old, not convenient to use, not type-safe. You can instead use libraries that read-write Excel files directly without opening in Excel. See cookbook recipes in "Excel files" folder.
