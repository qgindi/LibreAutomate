/// The mostly used Excel classes are in this simplified hierarchy:
/// <b>Application</b> - represents an Excel process. Contains workbooks.
/// . <b>Workbook</b> - represents an Excel file (.xlsx, .xls). Contains sheets (worksheets, charts, macros). Can have more than 1 window (the <b>Window</b> class). 
/// .. <b>Worksheet</b> - contains cells.
/// ... <b>Range</b> - represents 1 or more cells. You need it to get or set cell values etc.

/// This code gets objects of all these types in various ways.

/*/ c ExcelExt.cs; /*/
using Excel = Microsoft.Office.Interop.Excel;

//get Workbook from wnd
var wExcel = wnd.find(0, "*- Excel");
var book = wExcel.GetExcelWorkbook();

//get Worksheet from Workbook
Excel.Worksheet sheet = book.ActiveSheet;
Excel.Worksheet sheet3 = book.Worksheets["Sheet3"];
Excel.Worksheet sheet2 = book.Worksheets[2]; //1-based worksheet index (not including charts etc)
Excel.Worksheet sheet4 = book.Sheets[2]; //1-based sheet index (including charts etc)
Excel.Worksheet sheet5 = book.Sheets.Add(After: book.Sheets[book.Sheets.Count]); //add new worksheet

//get Range from Worksheet
Excel.Range r1 = sheet.Range["B2"]; //1 cell
Excel.Range r2 = sheet.Range["A1:C2"]; //6 cells
Excel.Range r3 = sheet.Range["A1", "C2"]; //6 cells
Excel.Range r4 = sheet.Range["Named range"];
Excel.Range r5 = sheet.Cells[1, 1]; //cell A1 (use 1-based row and column indices)
Excel.Range r6 = sheet.Cells[1, "A"]; //cell A1
Excel.Range r7 = sheet.UsedRange; //all cells in the smallest rectangle with non-empty cells

//get Range from Workbook through Window
Excel.Range r8 = book.Windows[1].ActiveCell; //the active cell in the active window of the workbook
Excel.Range r9 = book.Windows[1].Selection; //selected cells in the active window of the workbook

//get Application from anywhere
Excel.Application app = book.Application;
Excel.Application app2 = sheet.Application;
Excel.Application app3 = r1.Application;

//get Workbook from Application
Excel.Workbook book2 = app.ActiveWorkbook;
Excel.Workbook book3 = app.Workbooks["Name"];
Excel.Workbook book4 = app.Workbooks[1]; //1-based index

//get Worksheet from Application (from active workbook)
Excel.Worksheet sheet6 = app.ActiveSheet;
Excel.Worksheet sheet7 = app.Worksheets["Name"];

//get Range from Application (from active workbook)
Excel.Range r10 = app.ActiveCell;
Excel.Range r11 = app.Selection;

//get Workbook from Worksheet
Excel.Workbook book5 = sheet.Parent;

/// Excel functions often have parameters and return values of type <.k>object<> or <.k>dynamic<>. These types are used to carry a value of any type. The actual value type is retrieved at run time. Many such functions are in the above examples.

/// You can simply assign values of any type to <.k>object<> and <.k>dynamic<> variables or parameters.

object o = "STRING";
o = 10;
dynamic d = "STRING";
d = 10;
x.FunctionWithParametersOfObjectType("STRING", 10); //exception at run time if the function does not support types of passed values

/// Get a typed value from <.k>object<> (several ways). Here o is an <.k>object<> variable or a call to a function that returns <.k>object<>.

var s1 = (string)o; //exception at run time if o is not string
var s2 = o as string; //null if o is not string
if (o is string s3) print.it("o is string");
//or use switch statement or expression

/// Get a typed value from <.k>dynamic<>. Here d is a <.k>dynamic<> variable or a call to a function that returns <.k>dynamic<>.

string s4 = d; //exception at run time if o is not string
//other ways are the same as with object

/// <.k>dynamic<> allows to call functions of the actual type. They are resolved at run time. The code editor and compiler don't know the actual type and therefore cannot show a list of available functions etc and detect errors.

print.it(d.ToLower()); //exception at run time if the type does not have this function or if it called incorrectly
