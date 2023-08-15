/// To get/set cell value, need to get <b>Range</b> object for that cell. The <+recipe>Excel types<> recipe shows various ways of getting <b>Range</b>.

/*/ c ExcelExt.cs; /*/
using Excel = Microsoft.Office.Interop.Excel;

var wExcel = wnd.find(0, "*- Excel");
var book = wExcel.GetExcelWorkbook();
Excel.Worksheet sheet = book.ActiveSheet;

//set cell value
Excel.Range r1 = sheet.Cells[7, "A"]; //cell A7
r1.Value = "text";

//this works too
sheet.Cells[7, "B"] = 100;

//get cell value
string s1 = (string)r1.Value; //exception if the cell's data type is not text
double d1 = sheet.Cells[7, "B"].Value; //exception if the cell's data type is not number
int i1 = (int)sheet.Cells[7, "B"].Value;
print.it(s1, d1, i1);

//get string regardless of the value type
string s2 = r1.Text, s3 = sheet.Cells[7, "B"].Text;
print.it(s2, s3);

/// Get values of multiple cells.

Excel.Range r2 = sheet.UsedRange;
//Excel.Range r2 = book.Windows[1].Selection;
var a = r2.GetMany();
int nRows = a.GetLength(0), nCols = a.GetLength(1);
for (int row = 0; row < nRows; row++) {
	print.it($"-- row {row + 1} --");
	for (int col = 0; col < nCols; col++) print.it(a[row, col]);
}

/// Add multiple values from 2D array.

object[,] a2 = {
	{ 1, 2, 3 },
	{ 4, 5, 6 },
};
sheet.AddMany(a2); //append
//sheet.AddMany(a2, 10, "C"); //add at C10

/// Append multiple values from <b>List</b> of rows.

var a3 = new List<object[]>();
for (int i = 1; i <= 3; i++) a3.Add(new object[] { i, i * 2, i * 3 });

sheet.AddMany(a3);
