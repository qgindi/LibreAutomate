/// Open the <b>Properties<> dialog of class file <.c>ExcelExt<>, click <b>COM<>, select <b>Microsoft Excel<>, click <b>OK<>. It creates Excel COM interop assemblies for current Excel version and replaces the /*/ ... /*/ line.

/*/ com Excel 1.9 #9fdf46bf.dll; /*/
using Excel = Microsoft.Office.Interop.Excel;

/// <summary>
/// Extension methods for classes of namespace Microsoft.Office.Interop.Excel.
/// </summary>
public static class ExcelExt {
	/// <summary>
	/// Gets <b>Workbook</b> object from this Excel window.
	/// </summary>
	/// <exception cref="AuWndException">The window handle is 0 or invalid.</exception>
	/// <exception cref="Exception">Failed to get COM object.</exception>
	public static Excel.Workbook GetExcelWorkbook(this wnd t) => ((Excel.Window)ComUtil.GetWindowObject(t, "EXCEL7")).Parent;
	
	/// <summary>
	/// Appends or replaces values of multiple cells in the fast way.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="a">2D array containing cell data. Dimensions: [rows, columns].</param>
	/// <param name="row">1-based index of the first row for the new data. If null, uses the first unused row (appends).</param>
	/// <param name="column">Name or 1-based index of the first column for the new data. If null, uses "A".</param>
	public static void AddMany(this Excel.Worksheet t, object[,] a, object row = null, object column = null) {
		if (a.Length == 0) return;
		Excel.Range r = t.Cells[row ?? GetRowIndexForAppend(t), column ?? "A"];
		r = r.Resize[a.GetLength(0), a.GetLength(1)];
		r.Value = a;
	}
	
	/// <summary>
	/// Appends or replaces values of multiple cells in the fast way.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="list">List of rows.</param>
	/// <param name="row">1-based index of the first row for the new data. If null, uses the first unused row (appends).</param>
	/// <param name="column">Name or 1-based index of the first column for the new data. If null, uses "A".</param>
	public static void AddMany(this Excel.Worksheet t, List<object[]> list, object row = null, object column = null) {
		if (list.Count > 0) AddMany(t, list.ToArray2D(), row, column);
	}
	
	/// <summary>
	/// Gets 1-based index of the first row below the used range. If the worksheet is empty, returns 1.
	/// Can be used for appending data to the worksheet.
	/// </summary>
	public static int GetRowIndexForAppend(this Excel.Worksheet t) {
		var r = t.UsedRange;
		if (r.Count == 1 && r.Value is null) return r.Row;
		return r.Row + r.Rows.Count;
	}
	
	/// <summary>
	/// Converts List of object arrays to 2D array.
	/// </summary>
	public static object[,] ToArray2D(this List<object[]> t) {
		var r = new object[t.Count, t.Max(o => o.Length)];
		for (int i = 0; i < t.Count; i++) for (int j = 0; j < t[i].Length; j++) r[i, j] = t[i][j];
		return r;
	}
	
	/// <summary>
	/// Converts this 2-dim object array with lower bounds != 0 to array with lower bounds == 0.
	/// </summary>
	/// <returns>If this array has non-0 lower bounds, returns new array (copied), else returns this array.</returns>
	public static object[,] NormalizeBounds(this object[,] t) {
		int b0 = t.GetLowerBound(0), b1 = t.GetLowerBound(1);
		if (b0 == 0 && b1 == 0) return t;
		var r = new object[t.GetLength(0), t.GetLength(1)];
		Array.Copy(t, r, r.Length);
		return r;
	}
	
	/// <summary>
	/// Gets all values in this range.
	/// </summary>
	/// <returns>2D array.</returns>
	public static object[,] GetMany(this Excel.Range t) {
		var v = t.Value;
		if (v is object[,] a) //multiple cells
			return a.NormalizeBounds(); //1-based to 0-based
		return new object[,] { { v } }; //single cell
	}
}

//TODO: get used range from entire rows or columns.
