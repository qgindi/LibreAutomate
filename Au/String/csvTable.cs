namespace Au;

/// <summary>
/// Parses and composes CSV text. CSV table data in memory as a <b>List</b> of string arrays.
/// </summary>
/// <remarks>
/// CSV is a text format used to store a single table of data in human-readable/editable way.
/// It is a list of lines (called rows or records) containing one or more values (called fields or cells) separated by a separator character.
/// 
/// There is no strictly defined CSV standard. <b>csvTable</b> uses these rules:
/// <br/>• Fields containing separator characters (default <c>','</c>), quote characters (default <c>'"'</c>) and multiple lines are enclosed in quote characters. Example: <c>"ab, cd"</c>.
/// <br/>• Each quote character in such fields is escaped (replaced) with two quote characters. Example: <c>"ab ""cd"" ef"</c>.
/// <br/>• If a field value starts or ends with ASCII space or tab characters, it is enclosed in quote characters. Example: <c>" ab "</c>. Or use parameter <i>trimSpaces</i> <c>false</c> when parsing.
/// <br/>• Rows in CSV text can have different field count. All rows in in-memory CSV table have equal field count.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// var c = new csvTable();
/// c.AddRow("A", "B");
/// c.AddRow("C", "D");
/// var csv = c.ToString();
/// print.it(csv);
/// 
/// var k = csvTable.parse(csv);
/// for (int row = 0; row < k.RowCount; row++) {
/// 	print.it(k[row, 0], k[row, 1]);
/// }
/// ]]></code>
/// </example>
public class csvTable {
	readonly List<string[]> _a;
	
	/// <summary>
	/// Initializes new <see cref="csvTable"/> variable that can be used to add rows.
	/// To create new variables from CSV text, file or dictionary, instead use static functions, for example <see cref="parse"/>.
	/// </summary>
	public csvTable() { _a = new(); }
	
	csvTable(List<string[]> a, int columnCount) { _a = a; _columnCount = columnCount; }
	
	/// <summary>
	/// Gets the internal <b>List</b> containing rows as string arrays.
	/// </summary>
	/// <remarks>
	/// It's not a copy; changing its content will change content of this <see cref="csvTable"/> variable.
	/// You can do anything with the <b>List</b>. For example, sort it, find rows containing certain field values, get/set field values directly, add/remove rows directly.
	/// All row arrays have <b>Length</b> equal to <see cref="ColumnCount"/>, and it must remain so; you can change <b>Length</b>, but then need to call <c>ColumnCount=newLength</c>.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// x.Rows.Sort((a,b) => string.CompareOrdinal(a[0], b[0]));
	/// ]]></code>
	/// </example>
	public List<string[]> Rows => _a;
	
	/// <summary>
	/// Sets or gets the field separator character used when composing CSV text.
	/// Initially it is <c>','</c>.
	/// </summary>
	public char Separator { get; set; } = ',';
	
	/// <summary>
	/// Sets or gets the quote character used when composing CSV text.
	/// Initially it is <c>'"'</c>.
	/// </summary>
	public char Quote { get; set; } = '"';
	
	/// <summary>
	/// Parses CSV string and creates new <see cref="csvTable"/> variable that contains data in internal <b>List</b> of string arrays.
	/// </summary>
	/// <param name="csv">
	/// CSV text.
	/// If rows in CSV text have different field count, the longest row sets the <see cref="ColumnCount"/> property and lengths of all row arrays; array elements of missing CSV fields will be <c>null</c>.
	/// </param>
	/// <param name="separator">Field separator character used in CSV text. Default <c>','</c>.</param>
	/// <param name="quote">Character used in CSV text to enclose some fields. Default <c>'"'</c>.</param>
	/// <param name="trimSpaces">Ignore ASCII space and tab characters surrounding fields in CSV text. Default <c>true</c>.</param>
	/// <exception cref="FormatException">Invalid CSV, eg contains incorrectly enclosed fields.</exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static unsafe csvTable parse(string csv, char separator = ',', char quote = '"', bool trimSpaces = true) {
		if (csv.NE()) return new csvTable();
		
		var a = new List<string[]>();
		var tempRow = new List<string>(8);
		string sQuote1 = null, sQuote2 = null;
		
		fixed (char* s0 = csv) {
			char* s = s0, se = s0 + csv.Length;
			int nCol = 0;
			
			for (; s < se; s++) {
				//Read a field.
				string field = "";
				if (trimSpaces) { //ltrim
					while (*s == ' ' || *s == '\t') if (++s == se) goto g1;
				}
				char* f, e; //field beginning and end, not including spaces
				bool hasEscapedQuote = false;
				if (*s == quote) {
					f = ++s;
					bool hasClosingQuote = false;
					while (s < se) {
						if (*s++ == quote) {
							if (s < se && *s == quote) { s++; hasEscapedQuote = true; } //escaped quote
							else { hasClosingQuote = true; break; }
						}
					}
					if (!hasClosingQuote) throw new FormatException($"Invalid CSV format. Cells that start with {quote} must end with {quote}.");
					e = s - 1; //before quote
					if (trimSpaces) { //rtrim
						while (s < se && (*s == ' ' || *s == '\t')) s++;
					}
					if (s < se && !(*s == separator || *s == '\n')) {
						if (*s == '\r' && s < se + 1 && s[1] == '\n') s++;
						else throw new FormatException($"Invalid CSV format. For {quote} in enclosed cells use {quote}{quote}.");
					}
				} else {
					f = s; //field start
					while (s < se && *s != separator && *s != '\n') s++; //skip field, space and \r
					e = s; if (e > f && *e == '\n' && e[-1] == '\r') e--;
					if (trimSpaces) { //rtrim
						while (e > f && (e[-1] == ' ' || e[-1] == '\t')) e--;
					}
				}
				field = new string(f, 0, (int)(e - f)); //field to string
				if (hasEscapedQuote) {
					if (sQuote1 == null) { sQuote1 = new string(quote, 1); sQuote2 = new string(quote, 2); }
					field = field.Replace(sQuote2, sQuote1);
				}
				g1:
				
				//print.it(field);
				
				tempRow.Add(field);
				if (s >= se || *s == '\n') {
					//print.it(a.Count);
					//print.it(tempRow);
					
					a.Add(tempRow.ToArray());
					if (tempRow.Count > nCol) nCol = tempRow.Count;
					tempRow.Clear();
				}
			}
			
			var R = new csvTable(a, 0);
			R.ColumnCount = nCol; //make all rows of equal length and set _columnCount
			return R;
		} //fixed
	}
	
	/// <summary>
	/// Composes CSV text from the internal <b>List</b> of string arrays.
	/// </summary>
	/// <remarks>
	/// Depends on these properties: <see cref="Separator"/> (initially <c>','</c>), <see cref="Quote"/> (initially <c>'"'</c>).
	/// </remarks>
	public override string ToString() {
		if (RowCount == 0 || ColumnCount == 0) return "";
		
		using (new StringBuilder_(out var b)) {
			char quote = Quote;
			string sQuote1 = null, sQuote2 = null;
			
			for (int r = 0; r < _a.Count; r++) {
				for (int c = 0; c < _columnCount; c++) {
					var field = _a[r][c];
					if (!field.NE()) {
						bool hasQuote = field.Contains(quote);
						if (hasQuote || field.Contains(Separator) || field[0] == ' ' || field[^1] == ' ') {
							if (hasQuote) {
								if (sQuote1 == null) { sQuote1 = new string(quote, 1); sQuote2 = new string(quote, 2); }
								field = field.Replace(sQuote1, sQuote2);
							}
							b.Append(quote).Append(field).Append(quote);
						} else b.Append(field);
					}
					if (c < _columnCount - 1) b.Append(Separator);
				}
				b.AppendLine();
			}
			return b.ToString();
		}
	}
	
	/// <summary>
	/// Gets or sets row count.
	/// The <c>get</c> function returns the <b>Count</b> property of the internal <b>List</b> of string arrays.
	/// The <c>set</c> function can add new rows or remove rows at the end.
	/// </summary>
	public int RowCount {
		get => _a.Count;
		set {
			if (value > _a.Count) {
				_a.Capacity = value;
				while (_a.Count < value) _a.Add(_columnCount > 0 ? new string[_columnCount] : null);
			} else if (value < _a.Count) {
				_a.RemoveRange(value, _a.Count - value);
			}
		}
	}
	
	/// <summary>
	/// Gets or sets column count.
	/// The <c>get</c> function returns the length of all string arrays in the internal <b>List</b>.
	/// The <c>set</c> function can add new columns or remove columns at the right.
	/// </summary>
	public int ColumnCount {
		get => _columnCount;
		set {
			if (value <= 0) throw new ArgumentOutOfRangeException();
			//if(value == _columnCount) return;
			for (int r = 0; r < _a.Count; r++) {
				var o = _a[r];
				if (o == null) _a[r] = new string[value];
				else if (o.Length != value) {
					var t = new string[value];
					if (value > _columnCount) o.CopyTo(t, 0);
					else {
						for (int c = 0; c < value; c++) t[c] = o[c];
					}
					_a[r] = t;
				}
			}
			
			_columnCount = value;
		}
	}
	int _columnCount;
	
	/// <summary>
	/// Gets or sets a field.
	/// </summary>
	/// <param name="row">0-based row index. The <c>set</c> function adds new row if negative or equal to <see cref="RowCount"/>.</param>
	/// <param name="column">0-based column index. With the <c>set</c> function it can be &gt;= <see cref="ColumnCount"/> and &lt; 1000; then sets <c>ColumnCount = column + 1</c>.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <example>
	/// <code><![CDATA[
	/// var c = new csvTable { ColumnCount = 2 };
	/// c[0, 0] = "A1";
	/// c[0, 1] = "A2";
	/// c[1, 0] = "B1";
	/// c[1, 1] = "B2";
	/// print.it(c);
	/// string s = c[0, 0];
	/// print.it(s);
	/// ]]></code>
	/// </example>
	public string this[int row, int column] {
		get {
			if ((uint)column >= _columnCount) throw new ArgumentOutOfRangeException("column");
			if ((uint)row >= RowCount) throw new ArgumentOutOfRangeException("row");
			
			return _a[row][column];
		}
		set {
			//Auto-add columns.
			if (column < 0) column = int.MaxValue;
			if (column >= _columnCount) {
				if (column >= 1000) throw new ArgumentOutOfRangeException("column");
				ColumnCount = column + 1;
			}
			//Auto-add row.
			if (row < 0) row = RowCount;
			if (row >= RowCount) {
				if (row > RowCount) throw new ArgumentOutOfRangeException("row");
				_a.Add(new string[_columnCount]);
			}
			
			_a[row][column] = value;
		}
	}
	
	/// <summary>
	/// Gets or sets a field.
	/// </summary>
	/// <param name="row">0-based row index. Can be from the end; for example <c>^1</c> is the last row. The <c>set</c> function adds new row if <c>^0</c>.</param>
	/// <param name="column">0-based column index. With the <c>set</c> function it can be &gt;= <see cref="ColumnCount"/> and &lt; 1000; then sets <c>ColumnCount = column + 1</c>.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public string this[Index row, int column] {
		get => this[row.GetOffset(_a.Count), column];
		set { this[row.GetOffset(_a.Count), column] = value; }
	}
	
	/// <summary>
	/// Gets or sets fields in a row.
	/// </summary>
	/// <param name="row">0-based row index. The <c>set</c> function adds new row if negative or equal to <see cref="RowCount"/>.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <remarks>
	/// The <c>get</c> function gets the row array. It's not a copy; changing its elements will change content of this <see cref="csvTable"/> variable.
	/// The <c>set</c> function sets the row array. Does not copy the array, unless its <b>Length</b> is less than <see cref="ColumnCount"/>.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var c = new csvTable();
	/// c[-1] = ["A", "B"];
	/// c[-1] = ["C", "D"];
	/// print.it(c);
	/// for (int row = 0; row < c.RowCount; row++) {
	/// 	print.it(c[row, 0], c[row, 1]);
	/// }
	/// ]]></code>
	/// </example>
	public string[] this[int row] {
		get {
			if ((uint)row >= RowCount) throw new ArgumentOutOfRangeException();
			
			return _a[row];
		}
		set {
			//Auto-add row.
			if (row < 0) row = RowCount;
			if (row >= RowCount) {
				if (row > RowCount) throw new ArgumentOutOfRangeException();
				_a.Add(null);
			}
			
			var t = value;
			if (value == null || value.Length < _columnCount) {
				//make row length = _columnCount
				t = new string[_columnCount];
				value?.CopyTo(t, 0);
			} else if (value.Length > _columnCount) {
				//auto-add columns
				ColumnCount = value.Length;
			}
			
			_a[row] = t;
		}
	}
	
	/// <summary>
	/// Gets or sets fields in a row.
	/// </summary>
	/// <param name="row">0-based row index. Can be from the end; for example <c>^1</c> is the last row. The <c>set</c> function adds new row if <c>^0</c>.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <remarks>
	/// The <c>get</c> function gets the row array. It's not a copy; changing its elements will change content of this <see cref="csvTable"/> variable.
	/// The <c>set</c> function sets the row array. Does not copy the array, unless its <b>Length</b> is less than <see cref="ColumnCount"/>.
	/// </remarks>
	public string[] this[Index row] {
		get => this[row.GetOffset(_a.Count)];
		set { this[row.GetOffset(_a.Count)] = value; }
	}
	
	/// <summary>
	/// Adds new row and sets its fields.
	/// </summary>
	/// <param name="fields">Row fields. Can be a string array or multiple string arguments. Does not copy the array, unless its <b>Length</b> is less than <see cref="ColumnCount"/>. Adds new columns if array <b>Length</b> (or the number of string arguments) is greater than <b>ColumnCount</b>.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public void AddRow(params string[] fields) => InsertRow(-1, fields);
	
	/// <summary>
	/// Inserts new row and sets its fields.
	/// </summary>
	/// <param name="index">0-based row index. If negative or equal to <see cref="RowCount"/>, adds to the end.</param>
	/// <param name="fields">Row fields. Can be a string array or multiple string arguments. Does not copy the array, unless its <b>Length</b> is less than <see cref="ColumnCount"/>. Adds new columns if array <b>Length</b> (or the number of string arguments) is greater than <b>ColumnCount</b>.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public void InsertRow(int index, params string[] fields) {
		if (index < 0) index = RowCount;
		_a.Insert(index, null);
		this[index] = fields;
	}
	
	/// <summary>
	/// Inserts new empty row.
	/// </summary>
	/// <param name="index">0-based row index. If negative or equal to <see cref="RowCount"/>, adds to the end.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public void InsertRow(int index) {
		InsertRow(index, null);
	}
	
	/// <summary>
	/// Removes one or more rows.
	/// </summary>
	/// <param name="index">0-based row index.</param>
	/// <param name="count">How many rows to remove, default 1.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <exception cref="ArgumentException"></exception>
	public void RemoveRow(int index, int count = 1) {
		_a.RemoveRange(index, count);
	}
	
	//FUTURE: implement these. Rarely used. Quite much code.
	//Don't need to implement many others because users can call our _a (the Data property) methods directly.
	//public void MoveRow(int from, int to)
	//{
	
	//}
	
	//public void InsertColumn(int index)
	//{
	
	//}
	
	//public void RemoveColumn(int index)
	//{
	
	//}
	
	/// <summary>
	/// Loads and parses a CSV file.
	/// </summary>
	/// <param name="file">File. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>.</param>
	/// <param name="separator">Field separator character used in CSV text. Default <c>','</c>.</param>
	/// <param name="quote">Character used in CSV text to enclose some fields. Default <c>'"'</c>.</param>
	/// <param name="trimSpaces">Ignore ASCII space and tab characters surrounding fields in CSV text. Default <c>true</c>.</param>
	/// <exception cref="ArgumentException">Not full path.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="File.ReadAllText(string)"/>.</exception>
	/// <exception cref="FormatException">Invalid CSV, eg contains incorrectly enclosed fields.</exception>
	/// <remarks>
	/// Calls <see cref="File.ReadAllText(string)"/> and <see cref="parse"/>. Also uses <see cref="filesystem.waitIfLocked"/>.
	/// </remarks>
	public static csvTable load(string file, char separator = ',', char quote = '"', bool trimSpaces = true) {
		var csv = filesystem.loadText(file);
		return parse(csv, separator, quote, trimSpaces);
	}
	
	/// <summary>
	/// Composes CSV and saves to a file.
	/// </summary>
	/// <param name="file">File. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>. The file can exist or not; this function overwrites it.</param>
	/// <param name="backup">Create backup file named <c>file + "~backup"</c>.</param>
	/// <exception cref="ArgumentException">Not full path.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="File.WriteAllText(string, string)"/>.</exception>
	/// <remarks>
	/// Calls <see cref="ToString"/> and <see cref="File.WriteAllText(string, string)"/>. Also uses <see cref="filesystem.save"/>.
	/// </remarks>
	public void Save(string file, bool backup = false) {
		var csv = ToString();
		filesystem.saveText(file, csv, backup);
	}
	
	/// <summary>
	/// Creates 2-column CSV table from dictionary keys and values of type string.
	/// </summary>
	/// <param name="d"></param>
	/// <exception cref="ArgumentNullException"></exception>
	public static csvTable fromDictionary(Dictionary<string, string> d) {
		Not_.Null(d);
		var a = new List<string[]>(d.Count);
		foreach (var v in d) a.Add(new string[] { v.Key, v.Value });
		return new csvTable(a, 2);
	}
	
	/// <summary>
	/// Creates 2-column CSV table from dictionary keys and values of any type, using a callback function to convert values to string.
	/// </summary>
	/// <param name="d"></param>
	/// <param name="valueToString">Callback function that converts value of type T to string.</param>
	/// <exception cref="ArgumentNullException"></exception>
	public static csvTable fromDictionary<T>(Dictionary<string, T> d, Func<T, string> valueToString) {
		Not_.Null(d, valueToString);
		var a = new List<string[]>(d.Count);
		foreach (var v in d) {
			var t = valueToString(v.Value);
			a.Add(new string[] { v.Key, t });
		}
		return new csvTable(a, 2);
	}
	
	/// <summary>
	/// Creates CSV table of any column count from dictionary keys and values of any type, using a callback function to convert values to cell strings.
	/// </summary>
	/// <param name="d"></param>
	/// <param name="columnCount">CSV column count. Must be 2 or more.</param>
	/// <param name="valueToCells">Callback function that converts value of type T to one or more strings and puts them in row array elements starting from index 1. At index 0 is key.</param>
	/// <exception cref="ArgumentNullException"></exception>
	/// <exception cref="ArgumentOutOfRangeException"><i>columnCount</i> less than 2.</exception>
	public static csvTable fromDictionary<T>(Dictionary<string, T> d, int columnCount, Action<T, string[]> valueToCells) {
		Not_.Null(d, valueToCells);
		if (columnCount < 2) throw new ArgumentOutOfRangeException();
		var a = new List<string[]>(d.Count);
		foreach (var v in d) {
			var t = new string[columnCount];
			t[0] = v.Key;
			valueToCells(v.Value, t);
			a.Add(t);
		}
		return new csvTable(a, columnCount);
	}
	
	/// <summary>
	/// Creates dictionary from this 2-column CSV table.
	/// </summary>
	/// <param name="ignoreCase">Case-insensitive dictionary keys.</param>
	/// <param name="ignoreDuplicates">Don't throw exception if column 0 contains duplicate strings. Replace old value with new value.</param>
	/// <exception cref="InvalidOperationException"><b>ColumnCount</b> not 2.</exception>
	/// <exception cref="ArgumentException">Column 0 contains duplicate strings.</exception>
	public Dictionary<string, string> ToDictionary(bool ignoreCase, bool ignoreDuplicates) {
		if (_columnCount != 2) throw new InvalidOperationException("ColumnCount must be 2");
		var d = new Dictionary<string, string>(ignoreCase ? StringComparer.OrdinalIgnoreCase : null);
		foreach (var v in _a) {
			if (ignoreDuplicates) d[v[0]] = v[1];
			else d.Add(v[0], v[1]);
		}
		return d;
	}
	
	/// <summary>
	/// Creates dictionary from this CSV table of any column count, using a callback function to convert cell strings to dictionary values of any type.
	/// </summary>
	/// <param name="ignoreCase">Case-insensitive dictionary keys.</param>
	/// <param name="ignoreDuplicates">Don't throw exception if column 0 contains duplicate strings. Replace old value with new value.</param>
	/// <param name="rowToValue">Callback function that converts one or more cell strings to single value of type T. The array is whole row; element 0 is key, and usually is not used.</param>
	/// <exception cref="ArgumentNullException"></exception>
	/// <exception cref="InvalidOperationException"><b>ColumnCount</b> less than 2.</exception>
	/// <exception cref="ArgumentException">Column 0 contains duplicate strings.</exception>
	public Dictionary<string, T> ToDictionary<T>(bool ignoreCase, bool ignoreDuplicates, Func<string[], T> rowToValue) {
		Not_.Null(rowToValue);
		if (_columnCount < 2) throw new InvalidOperationException("ColumnCount must be >= 2");
		var d = new Dictionary<string, T>(ignoreCase ? StringComparer.OrdinalIgnoreCase : null);
		foreach (var v in _a) {
			var t = rowToValue(v);
			if (ignoreDuplicates) d[v[0]] = t;
			else d.Add(v[0], t);
		}
		return d;
	}
	
	//rejected, because: 1. In some cases can fail to resolve overloads. 2. Almost duplicate of the string[] overload.
	///// <summary>
	///// Creates dictionary from this 2-column CSV table, using a callback function to convert cell strings to dictionary values of any type.
	///// </summary>
	///// <param name="ignoreCase">Case-insensitive dictionary keys.</param>
	///// <param name="stringToValue">Callback function that converts cell string to value of type T.</param>
	///// <exception cref="ArgumentNullException"></exception>
	///// <exception cref="InvalidOperationException"><b>ColumnCount</b> not 2.</exception>
	///// <exception cref="ArgumentException">Column 0 contains duplicate values.</exception>
	//public Dictionary<string, T> ToDictionary<T>(bool ignoreCase, Func<string, T> stringToValue)
	//{
	//	if(stringToValue == null) throw new ArgumentNullException();
	//	if(_columnCount != 2) throw new InvalidOperationException("ColumnCount must be 2");
	//	var d = new Dictionary<string, T>(ignoreCase ? StringComparer.OrdinalIgnoreCase : null);
	//	foreach(var v in _a) d.Add(v[0], stringToValue(v[1]));
	//	return d;
	//}
	
	/// <summary>
	/// Obsolete, use <see cref="Set"/>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetInt(int row, int column, int value, bool hex = false) {
		this[row, column] = hex ? "0x" + value.ToString("X") : value.ToString();
	}
	
	/// <summary>
	/// Obsolete, use <see cref="Get"/>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public int GetInt(int row, int column) {
		return this[row, column].ToInt();
	}
	
	/// <summary>
	/// Obsolete, use <see cref="Set"/>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetDouble(int row, int column, double value) {
		this[row, column] = value.ToS();
	}
	
	/// <summary>
	/// Obsolete, use <see cref="Get"/>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public double GetDouble(int row, int column) {
		this[row, column].ToNumber(out double R);
		return R;
	}
	
	/// <summary>
	/// Converts a number to string and sets a field.
	/// </summary>
	/// <param name="row">0-based row index. Can be from the end; for example <c>^1</c> is the last row. Adds new row if <c>^0</c>.</param>
	/// <param name="column">0-based column index. If &gt;= <see cref="ColumnCount"/> and &lt; 1000, sets <c>ColumnCount = column + 1</c>.</param>
	/// <param name="value"></param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>row</i> or <i>column</i>.</exception>
	public void Set(Index row, int column, int value) { this[row, column] = value.ToS(); }
	
	/// <summary>
	/// Converts a number to hex string and sets a field.
	/// </summary>
	/// <inheritdoc cref="Set(Index, int, int)"/>
	public void Set(Index row, int column, uint value) { this[row, column] = "0x" + value.ToString("X"); }
	
	/// <inheritdoc cref="Set(Index, int, int)"/>
	public void Set(Index row, int column, long value) { this[row, column] = value.ToS(); }
	
	/// <inheritdoc cref="Set(Index, int, uint)"/>
	public void Set(Index row, int column, ulong value) { this[row, column] = "0x" + value.ToString("X"); }
	
	/// <inheritdoc cref="Set(Index, int, int)"/>
	public void Set(Index row, int column, double value) { this[row, column] = value.ToS(); }
	
	/// <inheritdoc cref="Set(Index, int, int)"/>
	public void Set(Index row, int column, float value) { this[row, column] = value.ToS(); }
	
	/// <summary>
	/// Converts a bool to string <c>"true"</c> or <c>"false"</c> and sets a field.
	/// </summary>
	/// <inheritdoc cref="Set(Index, int, int)"/>
	public void Set(Index row, int column, bool value) { this[row, column] = value ? "true" : "false"; }
	
	/// <summary>
	/// Gets a field value converted to int. See <see cref="ExtString.ToInt(string, out int, int, STIFlags)"/>.
	/// </summary>
	/// <param name="row">0-based row index.</param>
	/// <param name="column">0-based column index.</param>
	/// <param name="value">Receives the result, or 0 if failed.</param>
	/// <returns>False if failed to convert from string.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>row</i> or <i>column</i>.</exception>
	public bool Get(Index row, int column, out int value) => this[row, column].ToInt(out value);
	
	/// <summary>
	/// Gets a field value converted to uint. See <see cref="ExtString.ToInt(string, out uint, int, STIFlags)"/>.
	/// </summary>
	/// <inheritdoc cref="Get(Index, int, out int)"/>
	public bool Get(Index row, int column, out uint value) => this[row, column].ToInt(out value);
	
	/// <summary>
	/// Gets a field value converted to long. See <see cref="ExtString.ToInt(string, out long, int, STIFlags)"/>.
	/// </summary>
	/// <inheritdoc cref="Get(Index, int, out int)"/>
	public bool Get(Index row, int column, out long value) => this[row, column].ToInt(out value);
	
	/// <summary>
	/// Gets a field value converted to ulong. See <see cref="ExtString.ToInt(string, out ulong, int, STIFlags)"/>.
	/// </summary>
	/// <inheritdoc cref="Get(Index, int, out int)"/>
	public bool Get(Index row, int column, out ulong value) => this[row, column].ToInt(out value);
	
	/// <summary>
	/// Gets a field value converted to double. See <see cref="ExtString.ToNumber(string, out double, Range?, NumberStyles)"/>.
	/// </summary>
	/// <inheritdoc cref="Get(Index, int, out int)"/>
	public bool Get(Index row, int column, out double value) => this[row, column].ToNumber(out value);
	
	/// <summary>
	/// Gets a field value converted to float. See <see cref="ExtString.ToNumber(string, out float, Range?, NumberStyles)"/>.
	/// </summary>
	/// <inheritdoc cref="Get(Index, int, out int)"/>
	public bool Get(Index row, int column, out float value) => this[row, column].ToNumber(out value);
	
	/// <summary>
	/// Gets a field value like <c>"true"</c> or <c>"false"</c> converted to <c>bool</c>. Case-insensitive.
	/// </summary>
	/// <inheritdoc cref="Get(Index, int, out int)"/>
	public bool Get(Index row, int column, out bool value) => Boolean.TryParse(this[row, column], out value);
	
	//rejected: ToXml, ToHtml. Could be pasted in Excel, but need special format, difficult to make fully compatible. OpenOffice supports only HTML.
}
