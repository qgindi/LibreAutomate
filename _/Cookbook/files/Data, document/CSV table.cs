/// Use class <see cref="csvTable"/> to work with CSV files or CSV-formatted strings or string tables in memory.

/// CSV string example.
/// The last row shows how to use special characters in cell values.

var s = """"
A1, B1, C1
100, -5, 2.5
"comma, ""quotes""", " start/end spaces ", "multiple
lines"
"""";

/// Load CSV string or file.

csvTable x = csvTable.parse(s); //load from string
//csvTable x = csvTable.load(@"C:\Test\test.csv"); //load from file

/// Enumerate values.
/// Note: row and column indices are 0-based.

for (int r = 0; r < x.RowCount; r++) {
	print.it($"<><c green>row {r}<>");
	print.it(x[r, 0]);
	print.it(x[r, 1]);
	print.it(x[r, 2]);
}

/// Convert values (strings) to number.

int k = x.GetInt(1, 0); //or x[1, 0].ToInt()
double d = x.GetDouble(1, 2); //or x[1, 0].ToNumber()
print.it(k, d);

/// Change values.

x[0, 1] = "changed";
x.SetInt(1, 1, 50);

/// Add and remove rows.

x.RemoveRow(x.RowCount - 1); //remove the last row
x.AddRow("A3", "B3", "B4"); //append row
x.InsertRow(0, "A0", "B0", "B0"); //insert row
x.InsertRow(-1); //append empty row

/// Set row count and column count.

x.RowCount = 10;
x.ColumnCount = 6;

/// Convert to CSV string or save in file.

var s2 = x.ToString();
print.it(s2);
//x.Save(@"C:\Test\test.csv");
