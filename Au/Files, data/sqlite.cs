namespace Au {
	/// <summary>
	/// A SQLite database connection.
	/// Creates/opens/closes database file or in-memory database. Executes SQL, etc.
	/// </summary>
	/// <remarks>
	/// This class wraps a SQLite API object <b>sqlite3*</b> and related <b>sqlite3_x</b> functions. They are documented in the SQLite website.
	/// Uses this unmanaged dll: <c>folders.ThisApp + @"64\sqlite3.dll"</c>. In 32-bit process - <c>32</c> instead of <c>64</c>.
	/// 
	/// To correctly close the database file, at first need to dispose all child objects, such as <see cref="sqliteStatement"/>, then dispose the <b>sqlite</b> object. To dispose a static <b>sqlite</b> variable, you may want to use <see cref="process.thisProcessExit"/> event. Although this class has a finalizer that disposes the object (closes database), you should always dispose explicitly. Finalizers don't run on process exit.
	/// </remarks>
	/// <seealso cref="sqliteStatement"/>
	/// <example>
	/// <code><![CDATA[
	/// //open database file
	/// using var db = new sqlite(@"C:\test\sqlite.db");
	/// //create table
	/// db.Execute("CREATE TABLE IF NOT EXISTS test(id INTEGER PRIMARY KEY, name TEXT, x INT, guid BLOB, array BLOB)");
	/// 
	/// //add 2 rows of data
	/// using(var trans = db.Transaction()) { //optional, but makes much faster when making multiple changes, and ensures that all or none of these changes are written to the database
	/// 	using(var p = db.Statement("INSERT OR REPLACE INTO test VALUES(?, ?, :x, ?, ?)")) {
	/// 		//assume we want to add values of these variables to the database table
	/// 		int id = 1; string name = "TEXT"; long x = -10; Guid guid = Guid.NewGuid(); int[] arr = new int[] { 1, 2, 3 };
	/// 		//add first row
	/// 		p.Bind(1, id);
	/// 		p.Bind(2, name).BindStruct(4, guid).Bind(5, arr);
	/// 		p.Bind(":x", x);
	/// 		p.Step();
	/// 		//add second row
	/// 		p.Reset().Bind(1, 2).Bind(":x", 123456789012345).Step(); //unbound columns are null
	/// 	}
	/// 	//update single row
	/// 	db.Execute("UPDATE test SET name=?2 WHERE id=?1", 2, "two");
	/// 	//write all this to database
	/// 	trans.Commit();
	/// }
	/// 
	/// //get data
	/// using(var p = db.Statement("SELECT * FROM test")) {
	/// 	while(p.Step()) { //for each row of results
	/// 		print.it(p.GetInt(0), p.GetText(1), p.GetLong(2));
	/// 		print.it(p.GetStruct<Guid>("guid"));
	/// 		print.it(p.GetArray<int>("array"));
	/// 		print.it("----");
	/// 	}
	/// }
	/// //get single value
	/// if(db.Get(out string s1, "SELECT name FROM test WHERE id=?", 1)) print.it(s1); else print.it("not found");
	/// if(db.Get(out int i1, "PRAGMA page_size")) print.it(i1);
	/// ]]></code>
	/// </example>
	public unsafe class sqlite : IDisposable {
		IntPtr _db;

		/// <summary>
		/// Opens or creates a database file.
		/// </summary>
		/// <param name="file">
		/// Database file. Can be:
		/// <br/>• Full path. Supports environment variables etc, see <see cref="pathname.expand"/>
		/// <br/>• <c>":memory:"</c> - create a private, temporary in-memory database.
		/// <br/>• <c>""</c> - create a private, temporary on-disk database.
		/// <br/>• Starts with <c>"file:"</c> - see <google>sqlite3_open_v2</google>.
		/// </param>
		/// <param name="flags"><google>sqlite3_open_v2</google> flags. Default: read-write, create file if does not exist (and parent directory).</param>
		/// <param name="sql">
		/// SQL to execute. For example, one or more ;-separated <c>PRAGMA</c> statements to configure the database connection. Or even <c>"CREATE TABLE IF NOT EXISTS ..."</c>.
		/// This function also always executes <c>"PRAGMA foreign_keys=ON;PRAGMA secure_delete=ON;"</c>.
		/// </param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="SLException">Failed to open database or execute <i>sql</i>.</exception>
		/// <remarks>
		/// Calls <google>sqlite3_open_v2</google>.
		/// <note>If a variable of this class is used by multiple threads, use <c>lock(variable) {  }</c> where need.</note>
		/// </remarks>
		public sqlite(string file, SLFlags flags = SLFlags.ReadWriteCreate, string sql = null) {
			Debug.Assert(Assembly.GetCallingAssembly() != typeof(sqlite).Assembly); //don't use sqlite in Au. Then all programs that use Au also would need sqlite3.dll.
			
			bool isSpec = file != null && (file.Length == 0 || file == ":memory:" || file.Starts("file:"));
			if (!isSpec) {
				file = pathname.normalize(file);
				if (flags.Has(SLFlags.SQLITE_OPEN_CREATE) && !filesystem.exists(file, true).File) filesystem.createDirectoryFor(file);
			}
			var r = SLApi.sqlite3_open_v2(Convert2.Utf8Encode(file), ref _db, flags, null);
			if (r != 0) {
				Dispose();
				throw new SLException(r, "sqlite3_open " + file);
			}
			Execute("PRAGMA foreign_keys=ON;PRAGMA secure_delete=ON;" + sql);
		}

		///
		protected virtual void Dispose(bool disposing) {
			if (_db != default) {
				var r = SLApi.sqlite3_close_v2(_db);
				Debug.Assert(r == 0); SLUtil_.Warn(r, "sqlite3_close");
				_db = default;
			}
		}

		/// <summary>
		/// Calls <sqlite>sqlite3_close_v2</sqlite>.
		/// If fails, prints a warning.
		/// </summary>
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		///
		~sqlite() => Dispose(false);

		/// <summary><b>sqlite3*</b></summary>
		public static implicit operator IntPtr(sqlite c) => c._db;

		/// <summary><b>sqlite3*</b></summary>
		public IntPtr Handle => _db;

		/// <summary>
		/// Calls <sqlite>sqlite3_exec</sqlite> to execute one or more SQL statements that don't return data.
		/// </summary>
		/// <param name="sql">SQL statement, or several <c>;</c>-separated statements.</param>
		/// <exception cref="SLException">Failed to execute <i>sql</i>.</exception>
		public void Execute(string sql) {
			var b = Convert2.Utf8Encode(sql);
			byte* es = null; //gets better error text than sqlite3_errstr; sqlite3_errmsg gets nothing after sqlite3_exec.
			var r = SLApi.sqlite3_exec(_db, b, default, default, &es);
			if (r != 0) throw new SLException(r, "sqlite3_exec", SLUtil_.ToStringAndFree(es));
		}

		/// <summary>
		/// Executes single SQL statement that does not return data. Binds values.
		/// </summary>
		/// <param name="sql">Single SQL statement.</param>
		/// <param name="bind">
		/// Values that will replace <c>?</c> characters in <i>sql</i>.
		/// Read about SQL parameters in SQLite website. Supported types: <see cref="sqliteStatement.BindAll"/>. Example: <see cref="sqlite"/>.
		/// </param>
		/// <exception cref="SLException">Failed.</exception>
		/// <exception cref="NotSupportedException"><i>sql</i> contains more than single SQL statement.</exception>
		public void Execute(string sql, params object[] bind) {
			using var p = Statement(sql, bind);
			p.Step();
		}

		/// <summary>
		/// Executes single SQL statement that does not return data. To bind values calls callback function.
		/// </summary>
		/// <param name="sql">Single SQL statement.</param>
		/// <param name="bind">
		/// Callback function that should bind (<see cref="sqliteStatement.Bind"/>) values to <c>?</c> characters in <i>sql</i>.
		/// Read about SQL parameters in SQLite website.
		/// </param>
		/// <exception cref="SLException">Failed.</exception>
		/// <exception cref="NotSupportedException"><i>sql</i> contains more than single SQL statement.</exception>
		public void Execute(string sql, Action<sqliteStatement> bind) {
			using var p = Statement(sql);
			bind(p);
			p.Step();
		}

		/// <summary>
		/// Returns <c>new sqliteStatement(this, sql)</c>.
		/// </summary>
		/// <param name="sql">Single SQL statement. This function does not execute it.</param>
		/// <seealso cref="sqliteStatement"/>
		/// <inheritdoc cref="sqliteStatement(sqlite, string, bool)"/>
		public sqliteStatement Statement(string sql) => new sqliteStatement(this, sql);

		/// <summary>
		/// Returns <c>new sqliteStatement(this, sql).BindAll(bind)</c>.
		/// </summary>
		/// <param name="sql">Single SQL statement. This function does not execute it.</param>
		/// <param name="bind">
		/// Values that will replace <c>?</c> characters in <i>sql</i>. Optional.
		/// Read about SQL parameters in SQLite website. Supported types: <see cref="sqliteStatement.BindAll"/>. Example: <see cref="sqlite"/>.
		/// </param>
		/// <exception cref="NotSupportedException">
		/// - <i>sql</i> contains more than single SQL statement.
		/// - A <i>bind</i> value is of an unsupported type.
		/// </exception>
		/// <seealso cref="sqliteStatement"/>
		/// <seealso cref="sqliteStatement.BindAll"/>
		/// <inheritdoc cref="sqliteStatement(sqlite, string, bool)"/>
		/// <inheritdoc cref="sqliteStatement.BindAll" path="/param"/>
		public sqliteStatement Statement(string sql, params object[] bind) => new sqliteStatement(this, sql).BindAll(bind);

		#region get

		/// <summary>
		/// Executes single SQL statement and gets single value.
		/// </summary>
		/// <returns><c>false</c> if the statement returned no data.</returns>
		/// <param name="value">Receives data.</param>
		/// <exception cref="SLException">Failed.</exception>
		/// <exception cref="NotSupportedException"><i>sql</i> contains more than single SQL statement.</exception>
		/// <remarks>
		/// Also can be used to get <b>uint</b>, <b>short</b>, <b>ushort</b>, <b>byte</b>, <b>sbyte</b>, enum. Will need to cast from <b>int</b>.
		/// </remarks>
		/// <inheritdoc cref="Execute(string, object[])" path="/param"/>
		public bool Get(out int value, string sql, params object[] bind) {
			using var p = Statement(sql, bind);
			bool R = p.Step();
			value = R ? p.GetInt(0) : default;
			return R;
		}

		/// <remarks>
		/// Also can be used to get <b>ulong</b>, 64-bit enum, maybe <b>DateTime</b>.
		/// </remarks>
		/// <inheritdoc cref="Get(out int, string, object[])"/>
		public bool Get(out long value, string sql, params object[] bind) {
			using var p = Statement(sql, bind);
			bool R = p.Step();
			value = R ? p.GetLong(0) : default;
			return R;
		}

		///// <remarks></remarks>
		///// <inheritdoc cref="Get(out int, string, object[])"/>
		//public bool Get(out DateTime value, bool convertToLocal, string sql, params object[] bind)
		//{
		//	using var p = Prepare(sql, bind);
		//	bool R = p.Step();
		//	value = R ? p.GetDateTime(0, convertToLocal) : default;
		//	return R;
		//}

		/// <remarks></remarks>
		/// <inheritdoc cref="Get(out int, string, object[])"/>
		public bool Get(out bool value, string sql, params object[] bind) {
			using var p = Statement(sql, bind);
			bool R = p.Step();
			value = R ? p.GetBool(0) : default;
			return R;
		}

		/// <remarks>
		/// Also can be used to get <b>float</b>.
		/// </remarks>
		/// <inheritdoc cref="Get(out int, string, object[])"/>
		public bool Get(out double value, string sql, params object[] bind) {
			using var p = Statement(sql, bind);
			bool R = p.Step();
			value = R ? p.GetDouble(0) : default;
			return R;
		}

		/// <remarks></remarks>
		/// <inheritdoc cref="Get(out int, string, object[])"/>
		public bool Get(out string value, string sql, params object[] bind) {
			using var p = Statement(sql, bind);
			bool R = p.Step();
			value = R ? p.GetText(0) : default;
			return R;
		}

		/// <remarks></remarks>
		/// <inheritdoc cref="Get(out int, string, object[])"/>
		public bool Get<T>(out T[] value, string sql, params object[] bind) where T : unmanaged {
			using var p = Statement(sql, bind);
			bool R = p.Step();
			value = R ? p.GetArray<T>(0) : default;
			return R;
		}

		/// <remarks></remarks>
		/// <inheritdoc cref="Get(out int, string, object[])"/>
		public bool Get<T>(out List<T> value, string sql, params object[] bind) where T : unmanaged {
			using var p = Statement(sql, bind);
			bool R = p.Step();
			value = R ? p.GetList<T>(0) : default;
			return R;
		}

		/// <remarks>
		/// Can be used to get various value types, for example <b>decimal</b>, <b>Guid</b>, <b>RECT</b>.
		/// </remarks>
		/// <inheritdoc cref="Get(out int, string, object[])"/>
		public bool GetStruct<T>(out T value, string sql, params object[] bind) where T : unmanaged {
			using var p = Statement(sql, bind);
			bool R = p.Step();
			value = R ? p.GetStruct<T>(0) : default;
			return R;
		}

		/// <summary>
		/// Executes single SQL statement and returns <c>true</c> if it returns at least one row of data.
		/// </summary>
		/// <remarks>This function is similar to the <b>GetX</b> functions, but it does not retrieve the data.</remarks>
		/// <inheritdoc cref="Get(out int, string, object[])" path="//param|//exception"/>
		public bool Any(string sql, params object[] bind) {
			using var p = Statement(sql, bind);
			return p.Step();
		}

		#endregion

		/// <summary>
		/// Calls <sqlite>sqlite3_last_insert_rowid</sqlite>.
		/// </summary>
		public long LastInsertRowid => SLApi.sqlite3_last_insert_rowid(_db);

		/// <summary>
		/// Calls <sqlite>sqlite3_changes</sqlite>.
		/// </summary>
		public int Changes => SLApi.sqlite3_changes(_db);

		/// <summary>
		/// Calls <sqlite>sqlite3_get_autocommit</sqlite>.
		/// </summary>
		public bool IsInTransaction => 0 == SLApi.sqlite3_get_autocommit(_db);

		/// <summary>
		/// Returns <c>new SLTransaction(this, sql, sqlOfDispose)</c>.
		/// See <see cref="SLTransaction(sqlite, string, string)"/>.
		/// </summary>
		/// <inheritdoc cref="SLTransaction(sqlite, string, string)"/>
		public SLTransaction Transaction(string sql = "BEGIN", string sqlOfDispose = "ROLLBACK")
			=> new SLTransaction(this, sql, sqlOfDispose);

		/// <summary>
		/// Returns <c>true</c> if the table exists.
		/// </summary>
		/// <param name="table">Table name.</param>
		/// <remarks>
		/// This function is slower than <c>"CREATE TABLE IF NOT EXISTS..."</c>.
		/// </remarks>
		public bool TableExists(string table) {
			return Any("SELECT 1 FROM sqlite_master WHERE type='table' AND name=?", table);
		}

		#region util

		/// <summary>
		/// Returns <c>true</c> if default database text encoding is not UTF-8.
		/// </summary>
		internal bool IsUtf16 {
			get {
				if (__isUtf16 == 0) {
					var t = _RawGetText("PRAGMA encoding") ?? "";
					__isUtf16 = (byte)(t.Starts("UTF-16") ? 2 : 1);
				}
				return __isUtf16 == 2;
			}
		}
		byte __isUtf16; //0 not queried, 1 utf8 or failed, 2 utf16

		string _RawGetText(string sql) {
			string r = null;
			fixed (char* p = sql) {
				IntPtr x = default;
				if (0 == SLApi.sqlite3_prepare16_v3(_db, p, -1, 0, ref x, null)) {
					var e = SLApi.sqlite3_step(x);
					Debug.Assert(e == SLError.Row || e == SLError.Done);
					if (e == SLError.Row) r = Convert2.Utf8Decode(SLApi.sqlite3_column_text(x, 0));
					SLApi.sqlite3_finalize(x);
				}
			}
			return r;
		}

		#endregion
	}

	/// <summary>
	/// Creates and executes a SQLite prepared statement.
	/// </summary>
	/// <remarks>
	/// This class wraps a SQLite API object <sqlite>sqlite3_stmt*</sqlite> and related <b>sqlite3_x</b> functions. They are documented perfectly in the SQLite website.
	/// More info and example: <see cref="sqlite"/>.
	/// <note type="important">A variable of this class can be used by multiple threads, but not simultaneously. Use <c>lock(database) {  }</c> where need.</note>
	/// </remarks>
	public unsafe class sqliteStatement : IDisposable {
		sqlite _db;
		IntPtr _st;

		/// <summary>
		/// Calls <sqlite>sqlite3_prepare16_v3</sqlite>.
		/// </summary>
		/// <param name="db"></param>
		/// <param name="sql">Single SQL statement.</param>
		/// <param name="persistent">Use flag <b>SQLITE_PREPARE_PERSISTENT</b>.</param>
		/// <exception cref="SLException">Failed.</exception>
		/// <exception cref="NotSupportedException"><i>sql</i> contains more than single SQL statement.</exception>
		public sqliteStatement(sqlite db, string sql, bool persistent = false) {
			Not_.Null(db);
			_db = db;
			int flags = persistent ? 1 : 0; //SQLITE_PREPARE_PERSISTENT
			fixed (char* p = sql) {
				char* tail = null;
				_Err(SLApi.sqlite3_prepare16_v3(db, p, sql.Length * 2, flags, ref _st, &tail), "sqlite3_prepare");
				if (tail != null && tail - p != sql.Length) throw new NotSupportedException("sql contains more than single SQL statement");
			}
		}

		///
		protected virtual void Dispose(bool disposing) {
			if (_st != default) {
				//note: don't throw, because sqlite3_finalize can return error of previous sqlite3_step etc
				SLApi.sqlite3_finalize(_st);
				_st = default;
			}
		}

		/// <summary>
		/// Calls <sqlite>sqlite3_finalize</sqlite>.
		/// </summary>
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		///
		~sqliteStatement() => Dispose(false);

		/// <summary><b>sqlite3_stmt*</b></summary>
		public static implicit operator IntPtr(sqliteStatement s) => s._st;

		/// <summary><b>sqlite3_stmt*</b></summary>
		public IntPtr Handle => _st;

		/// <summary>
		/// The database connection.
		/// </summary>
		public sqlite DB => _db;

		/// <summary>
		/// Calls <sqlite>sqlite3_step</sqlite>, and returns <c>true</c> if results data available (<sqlite>sqlite3_step</sqlite> returned <b>SQLITE_ROW</b>).
		/// </summary>
		/// <exception cref="SLException">Failed.</exception>
		public bool Step() {
			var r = SLApi.sqlite3_step(_st);
			if (r == SLError.Row) return true;
			if (r != SLError.Done) _Err(r, "sqlite3_step");
			return false;
		}

		/// <summary>
		/// Calls <sqlite>sqlite3_reset</sqlite> and/or <sqlite>sqlite3_clear_bindings</sqlite>.
		/// </summary>
		/// <returns>this.</returns>
		/// <param name="resetStatement">Call <sqlite>sqlite3_reset</sqlite>. Default <c>true</c>.</param>
		/// <param name="clearBindings">Call <sqlite>sqlite3_clear_bindings</sqlite>. Default <c>true</c>.</param>
		public sqliteStatement Reset(bool resetStatement = true, bool clearBindings = true) {
			//note: don't throw, because sqlite3_reset can return error of previous sqlite3_step
			if (resetStatement) SLApi.sqlite3_reset(_st);
			if (clearBindings) SLApi.sqlite3_clear_bindings(_st);
			return this;
		}

		#region bind

		int _B(SLIndexOrName p) {
			if (p.name == null) return p.index;
			int r = SLApi.sqlite3_bind_parameter_index(_st, Convert2.Utf8Encode(p.name));
			if (r == 0) throw new SLException($"Parameter '{p.name}' does not exist in the SQL statement.");
			return r;
		}

		/// <summary>Calls <sqlite>sqlite3_bind_int</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind(SLIndexOrName sqlParam, int value)
			=> _Err(SLApi.sqlite3_bind_int(_st, _B(sqlParam), value), "sqlite3_bind_int");

		/// <summary>Calls <sqlite>sqlite3_bind_int</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind(SLIndexOrName sqlParam, uint value)
			=> Bind(sqlParam, (int)value);

		/// <summary>Calls <sqlite>sqlite3_bind_int64</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind(SLIndexOrName sqlParam, long value)
			=> _Err(SLApi.sqlite3_bind_int64(_st, _B(sqlParam), value), "sqlite3_bind_int64");

		/// <summary>Calls <sqlite>sqlite3_bind_int64</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind(SLIndexOrName sqlParam, ulong value)
			=> Bind(sqlParam, (long)value);

		/// <summary>Calls <google>sqlite3_bind_int</google> (0 or 1).</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind(SLIndexOrName sqlParam, bool value)
			=> Bind(sqlParam, value ? 1 : 0);

		/// <summary>Binds an enum value as <b>int</b> or <b>long</b>. Calls <sqlite>sqlite3_bind_int</sqlite> or <sqlite>sqlite3_bind_int64</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		[MethodImpl(MethodImplOptions.NoInlining)] //ensure that value is copied to the parameter, because must not be smaller than int
		public sqliteStatement Bind<T>(SLIndexOrName sqlParam, T value) where T : unmanaged, Enum
			=> Bind(sqlParam, sizeof(T) == 8 ? *(long*)&value : *(int*)&value);

		/// <summary>Calls <sqlite>sqlite3_bind_double</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind(SLIndexOrName sqlParam, double value)
			=> _Err(SLApi.sqlite3_bind_double(_st, _B(sqlParam), value), "sqlite3_bind_double");

		/// <summary>Calls <sqlite>sqlite3_bind_text16</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind(SLIndexOrName sqlParam, string value)
			=> _Err(SLApi.sqlite3_bind_text16(_st, _B(sqlParam), value, (value?.Length ?? 0) * 2), "sqlite3_bind_text16");

		/// <summary>Calls <sqlite>sqlite3_bind_blob64</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind(SLIndexOrName sqlParam, void* blob, long nBytes)
			=> _Err(SLApi.sqlite3_bind_blob64(_st, _B(sqlParam), blob, nBytes), "sqlite3_bind_blob64");

		sqliteStatement _Bind(SLIndexOrName sqlParam, RByte blob) {
			fixed (byte* p = blob) return Bind(sqlParam, p, blob.Length);
		}

		/// <summary>Calls <sqlite>sqlite3_bind_blob64</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind<T>(SLIndexOrName sqlParam, ReadOnlySpan<T> blob) where T : unmanaged
			=> _Bind(sqlParam, MemoryMarshal.AsBytes(blob));

		/// <summary>Calls <sqlite>sqlite3_bind_blob64</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind<T>(SLIndexOrName sqlParam, Span<T> blob) where T : unmanaged
			=> _Bind(sqlParam, MemoryMarshal.AsBytes(blob));

		/// <summary>Calls <sqlite>sqlite3_bind_blob64</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind<T>(SLIndexOrName sqlParam, T[] array) where T : unmanaged
			=> Bind(sqlParam, array.AsSpan());

		/// <summary>Calls <sqlite>sqlite3_bind_blob64</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		public sqliteStatement Bind<T>(SLIndexOrName sqlParam, List<T> list) where T : unmanaged
			=> Bind(sqlParam, CollectionsMarshal.AsSpan(list));

		/// <summary>Binds a value as blob. Calls <sqlite>sqlite3_bind_blob64</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		/// <remarks>Can be any value type that does not contain fields of reference types. Examples: <b>Guid</b>, <b>POINT</b>, <b>int</b>, <b>decimal</b>.</remarks>
		public sqliteStatement BindStruct<T>(SLIndexOrName sqlParam, T value) where T : unmanaged
			=> Bind(sqlParam, &value, sizeof(T));

		/// <summary>Calls <sqlite>sqlite3_bind_null</sqlite>.</summary>
		/// <returns>this.</returns>
		/// <param name="sqlParam">Parameter name or 1-based index.</param>
		/// <exception cref="SLException">Failed.</exception>
		/// <remarks>Usually don't need to call this function. Unset parameter values are null. The <b>Bind</b> functions set null too if the value is null.</remarks>
		public sqliteStatement BindNull(SLIndexOrName sqlParam)
			=> _Err(SLApi.sqlite3_bind_null(_st, _B(sqlParam)), "sqlite3_bind_null");

		//rejected. 1. Currently we don't have a Blob class. 2. Can do in SQL: zeroblob(nBytes).
		///// <summary>Calls <sqlite>sqlite3_bind_zeroblob</sqlite>.</summary>
		///// <returns>this.</returns>
		///// <exception cref="SLException">Failed.</exception>
		//public Statement BindZeroBlob(SLIndexOrName sqlParam, int nBytes)
		//	=> _Err(SLApi.sqlite3_bind_zeroblob(_st, _B(sqlParam), nBytes), "sqlite3_bind_zeroblob");

		//rejected. DateTime can be stored in many ways. Let users decide how they want to store it, and explicitly convert to long, string, etc.
		///// <summary>Calls <c>sqlite3_bind_int64(value.ToBinary())</c>.</summary>
		///// <returns>this.</returns>
		///// <exception cref="SLException">Failed.</exception>
		//public Statement Bind(SLIndexOrName sqlParam, DateTime value, bool convertToUtc = false)
		//	=> Bind(sqlParam, (convertToUtc ? value.ToUniversalTime() : value).ToBinary());

		/// <summary>
		/// Used by <see cref="BindAll"/>.
		/// </summary>
		internal void BindObject(int i, object v) {
			int k;
			switch (v) {
			case null: BindNull(i); break;
			case int x: k = x; goto gi;
			case uint x: k = (int)x; goto gi;
			case bool x: k = x ? 1 : 0; goto gi;
			case long x: Bind(i, x); break;
			case ulong x: Bind(i, x); break;
			case double x: Bind(i, x); break;
			case float x: Bind(i, x); break;
			case string x: Bind(i, x); break;
			case byte x: k = x; goto gi;
			case sbyte x: k = x; goto gi;
			case short x: k = x; goto gi;
			case ushort x: k = x; goto gi;
			case decimal x: Bind(i, &x, sizeof(decimal)); break;
			case Guid x: Bind(i, &x, sizeof(Guid)); break;
			case Enum x:
				switch (x.GetTypeCode()) {
				case TypeCode.Int64: case TypeCode.UInt64: Bind(i, Convert.ToInt64(v)); break;
				default: k = Convert.ToInt32(v); goto gi;
				}
				break;
			case Array a:
				//never mind: should throw if managed type. Same for List.
				//	It seems .NET does not have a function to check it.
				//	Slow workarounds: https://stackoverflow.com/questions/53968920/how-do-i-check-if-a-type-fits-the-unmanaged-constraint-in-c
				fixed (byte* p = Unsafe.As<byte[]>(a)) Bind(i, p, Buffer.ByteLength(a));
				break;
			//case System.Collections.IList a:
			//	//Can get data pointer and number of elements:
			//	//	var span = CollectionsMarshal.AsSpan(Unsafe.As<List<byte>>(a)).
			//	//But how to get element type size in a safe/fast/clean way?
			//	//	This works, but unsafe etc: Marshal.SizeOf(a.GetType().GetGenericArguments()[0])
			//	//	This does not work: MemoryMarshal.AsBytes(span).
			//	//Or can get array through reflection, but slow and unsafe: var v=a.GetType().GetField("_items", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(i) as Array;
			//	//Or can convert to List<T> with 'dynamic', but first time it adds 72 ms delay (hot CPU).
			//	break;
			default:
				//never mind: this func does not support other types supported by other BindX functions. Quite difficult.
				var t = v.GetType();
				throw new NotSupportedException(t.Name);
				//case DateTime x: Bind(i, x); break;
			}
			return;
			gi: Bind(i, k);
		}

		/// <summary>
		/// Binds multiple values of any supported types.
		/// </summary>
		/// <returns>this.</returns>
		/// <param name="values">
		/// Values that will replace <c>?</c> characters in SQL.
		/// Read about SQL parameters in SQLite website. Example: <see cref="sqlite"/>.
		/// Supported types:
		/// <br/>• <b>int</b>, <b>uint</b>, <b>byte</b>, <b>sbyte</b>, <b>short</b>, <b>ushort</b> - calls <sqlite>sqlite3_bind_int</sqlite>.
		/// <br/>• <b>bool</b> - calls <sqlite>sqlite3_bind_int</sqlite> (0 or 1).
		/// <br/>• <b>long</b>, <b>ulong</b> - calls <sqlite>sqlite3_bind_int64</sqlite>.
		/// <br/>• <b>double</b>, <b>float</b> - calls <sqlite>sqlite3_bind_double</sqlite>.
		/// <br/>• <b>string</b> - calls <sqlite>sqlite3_bind_text16</sqlite>.
		/// <br/>• <b>decimal</b> - calls <sqlite>sqlite3_bind_blob64</sqlite>.
		/// <br/>• <b>Guid</b> - calls <sqlite>sqlite3_bind_blob64</sqlite>.
		/// <br/>• <b>Array</b> - calls <sqlite>sqlite3_bind_blob64</sqlite>.
		/// <br/>• An <b>enum</b> type - calls <sqlite>sqlite3_bind_int</sqlite> or <sqlite>sqlite3_bind_int64</sqlite>.
		/// </param>
		/// <exception cref="NotSupportedException">A value is of an unsupported type.</exception>
		/// <exception cref="SLException">Failed.</exception>
		/// <remarks>
		/// For each parameter calls a <b>sqlite3_bind_x</b> function depending on type. Uses index 1, 2 and so on.
		/// This function is an alternative to calling <b>BindX</b> functions for each parameter. However it supports less types and adds boxing overhead.
		/// Does not call <sqlite>sqlite3_reset</sqlite> and <sqlite>sqlite3_clear_bindings</sqlite>. If need, call <see cref="Reset"/> before.
		/// </remarks>
		public sqliteStatement BindAll(params object[] values) {
			if (values != null)
				for (int i = 0; i < values.Length;) {
					object v = values[i];
					BindObject(++i, v);
				}
			return this;
		}

		#endregion

		#region get

		int _C(SLIndexOrName p) {
			if (p.name == null) return p.index;
			int r = ColumnIndex(p.name);
			if (r < 0) throw new SLException($"Column '{p.name}' does not exist in query results.");
			return r;
		}

		/// <summary>
		/// Calls <google>sqlite3_column_int</google>.
		/// </summary>
		/// <param name="column">Column name of 0-based index in results.</param>
		/// <exception cref="SLException">The column does not exist in query results.</exception>
		/// <remarks>
		/// Use this function to get integer values of size 4, 2 or 1 bytes: <b>int</b>, <b>uint</b>, <b>short</b>, <b>ushort</b>, <b>byte</b>, <b>sbyte</b>, enum.
		/// </remarks>
		public int GetInt(SLIndexOrName column) {
			int r = SLApi.sqlite3_column_int(_st, _C(column));
			if (r == 0) _WarnGet();
			return r;
		}

		/// <summary>
		/// Calls <google>sqlite3_column_int64</google>.
		/// </summary>
		/// <param name="column">Column name of 0-based index in results.</param>
		/// <exception cref="SLException">The column does not exist in query results.</exception>
		/// <remarks>
		/// Use this function to get integer values of size 8 bytes: <b>long</b>, <b>ulong</b>, 64-bit enum, maybe <b>DateTime</b>.
		/// </remarks>
		public long GetLong(SLIndexOrName column) {
			long r = SLApi.sqlite3_column_int64(_st, _C(column));
			if (r == 0) _WarnGet();
			return r;
		}

		/// <summary>
		/// Calls <google>sqlite3_column_int64</google> and returns <c>true</c> if the value is not 0.
		/// </summary>
		/// <param name="column">Column name of 0-based index in results.</param>
		/// <exception cref="SLException">The column does not exist in query results.</exception>
		public bool GetBool(SLIndexOrName column) => GetLong(column) != 0;

		///// <summary>
		///// Calls <c>DateTime.FromBinary(sqlite3_column_int64(column))</c>.
		///// </summary>
		///// <param name="column">Column name of 0-based index in results.</param>
		///// <param name="convertToLocal">If the value in database is stored as UTC, convert to local.</param>
		///// <exception cref="ArgumentException">The value in database is not in the valid DateTime range.</exception>
		///// <seealso cref="Bind(int, DateTime, bool)"/>
		//public DateTime GetDateTime(SLIndexOrName column, bool convertToLocal = false)
		//{
		//	var r = DateTime.FromBinary(GetLong(column)); //info: it's OK if 0/null
		//	if(convertToLocal && r.Kind == DateTimeKind.Utc) r = r.ToLocalTime();
		//	return r;
		//}

		/// <summary>
		/// Calls <google>qlite3_column_double</google>.
		/// </summary>
		/// <param name="column">Column name of 0-based index in results.</param>
		/// <exception cref="SLException">The column does not exist in query results.</exception>
		public double GetDouble(SLIndexOrName column) {
			double r = SLApi.sqlite3_column_double(_st, _C(column));
			if (r == 0) _WarnGet();
			return r;
		}

		/// <summary>
		/// Calls <google>sqlite3_column_text</google>.
		/// </summary>
		/// <param name="column">Column name of 0-based index in results.</param>
		/// <exception cref="SLException">The column does not exist in query results.</exception>
		public string GetText(SLIndexOrName column) {
			int icol = _C(column);
			if (_db.IsUtf16) { //both these codes would work, but with long strings can be significantly slower if SQLite has to convert text encoding
				char* t = SLApi.sqlite3_column_text16(_st, icol);
				if (t != null) return new string(t, 0, SLApi.sqlite3_column_bytes16(_st, icol));
			} else {
				byte* t = SLApi.sqlite3_column_text(_st, icol);
				if (t != null) return Encoding.UTF8.GetString(t, SLApi.sqlite3_column_bytes(_st, icol));
			}
			_WarnGet();
			return null;
		}

		/// <summary>
		/// Calls <google>sqlite3_column_blob</google>.
		/// </summary>
		/// <returns>The returned memory is managed by SQLite and will become invalid when calling other SQLite functions afterwards. For zero-length BLOB returns <c>null</c>.</returns>
		/// <param name="column">Column name of 0-based index in results.</param>
		/// <param name="nBytes">Blob size.</param>
		/// <exception cref="SLException">The column does not exist in query results.</exception>
		public byte* GetBlob(SLIndexOrName column, out int nBytes) {
			int icol = _C(column);
			var r = (byte*)SLApi.sqlite3_column_blob(_st, icol);
			if (r == null) { nBytes = 0; _WarnGet(); } else nBytes = SLApi.sqlite3_column_bytes(_st, icol);
			return r;
		}

		/// <summary>
		/// Calls <google>sqlite3_column_blob</google>.
		/// </summary>
		/// <returns>The returned memory is managed by SQLite and will become invalid when calling other SQLite functions afterwards.</returns>
		/// <param name="column">Column name of 0-based index in results.</param>
		/// <exception cref="SLException">The column does not exist in query results.</exception>
		public RByte GetBlob(SLIndexOrName column) {
			var p = GetBlob(column, out int n);
			return new(p, n);
		}

		/// <summary>
		/// Calls <google>sqlite3_column_blob</google>.
		/// </summary>
		/// <returns>The returned memory is managed by SQLite and will become invalid when calling other SQLite functions afterwards.</returns>
		/// <param name="column">Column name of 0-based index in results.</param>
		/// <exception cref="SLException">The column does not exist in query results.</exception>
		public ReadOnlySpan<T> GetBlob<T>(SLIndexOrName column) where T: unmanaged {
			var p = GetBlob(column, out int n);
			return new(p, n / sizeof(T));
		}

		/// <summary>
		/// Calls <google>sqlite3_column_blob</google> and creates array.
		/// </summary>
		/// <param name="column">Column name of 0-based index in results.</param>
		/// <exception cref="SLException">The column does not exist in query results.</exception>
		public T[] GetArray<T>(SLIndexOrName column) where T : unmanaged {
			var t = GetBlob(column, out int nb);
			if (t == null) return null;
			if (nb == 0) return Array.Empty<T>();
			int size = sizeof(T);
			int ne = nb / size; nb = ne * size;
			var r = new T[ne];
			fixed (T* p = r) MemoryUtil.Copy(t, p, nb);
			return r;
		}

		/// <summary>
		/// Calls <google>sqlite3_column_blob</google> and creates <b>List&lt;T&gt;</b>.
		/// </summary>
		/// <param name="column">Column name of 0-based index in results.</param>
		/// <exception cref="SLException">The column does not exist in query results.</exception>
		public List<T> GetList<T>(SLIndexOrName column) where T : unmanaged {
			var t = (T*)GetBlob(column, out int nb);
			if (t == null) return null;
			int ne = nb / sizeof(T);
			var r = new List<T>(ne);
			for (int i = 0; i < ne; i++) r.Add(t[i]);
			return r;
		}

		/// <summary>
		/// Calls <google>sqlite3_column_blob</google> and creates a variable of any value type that does not have fields of reference types.
		/// </summary>
		/// <param name="column">Column name of 0-based index in results.</param>
		/// <exception cref="SLException">The column does not exist in query results.</exception>
		public T GetStruct<T>(SLIndexOrName column) where T : unmanaged {
			var t = (T*)GetBlob(column, out int nb);
			T r = default;
			if (t != null && nb == sizeof(T)) MemoryUtil.Copy(t, &r, nb);
			return r;
		}

		/// <summary>
		/// Calls <sqlite>sqlite3_column_count</sqlite>.
		/// </summary>
		public int ColumnCount => SLApi.sqlite3_column_count(_st);

		/// <summary>
		/// Calls <sqlite>sqlite3_column_name</sqlite>.
		/// </summary>
		public string ColumnName(int index) => Convert2.Utf8Decode(SLApi.sqlite3_column_name(_st, index));

		/// <summary>
		/// Finds column by name in results.
		/// </summary>
		/// <returns>0-based index, or -1 if not found.</returns>
		/// <param name="name">Column name in results, as returned by <sqlite>sqlite3_column_name</sqlite>. Case-sensitive.</param>
		public int ColumnIndex(string name) {
			int n = ColumnCount;
			if (n > 0 && !name.NE()) {
				if (name.IsAscii()) {
					for (int i = 0; i < n; i++) {
						byte* b = SLApi.sqlite3_column_name(_st, i);
						if (BytePtr_.AsciiEq(b, name)) return i;
					}
				} else {
					var bname = Convert2.Utf8Encode(name);
					for (int i = 0; i < n; i++) {
						byte* b = SLApi.sqlite3_column_name(_st, i);
						if (BytePtr_.Eq(b, bname)) return i;
					}
				}
			}
			return -1;
		}

		#endregion

		#region util

		[DebuggerStepThrough]
		sqliteStatement _Err(SLError r, string func) {
			if (r != 0 && r != SLError.Row) throw new SLException(r, _db, func);
			return this;
		}

		//rejected: not thread-safe. Other .NET wrappers ignore it too.
		//void _Err(string func)
		//{
		//	var r = SLApi.sqlite3_errcode(_db);
		//	if(r != 0 && r != SLError.Row) throw new SLException(r, _db, func);
		//}

		/// <summary>
		/// Called by <b>GetX</b> functions when <b>sqlite3_column_x</b> returns <c>null</c>/0.
		/// Shows warning if <sqlite>sqlite3_errcode</sqlite> is not 0 or Row.
		/// Does not throw exception because it is not thread-safe.
		/// </summary>
		void _WarnGet([CallerMemberName] string m_ = null) {
			var r = SLApi.sqlite3_errcode(_db);
			if (r != 0 && r != SLError.Row) SLUtil_.Warn(r, m_, "Note: it may be a false positive if this database connection is used by multiple threads simultaneously without locking.");
		}

		#endregion
	}
}

namespace Au.Types {
	/// <summary>
	/// A SQLite transaction or savepoint. The main purpose is to automatically rollback if not explicitly committed.
	/// Usage: <c>using(var trans = new SLTransaction(db)) { ... trans.Commit(); }</c>
	/// </summary>
	public sealed class SLTransaction : IDisposable {
		sqlite _db;

		/// <summary>
		/// Begins a SQLite transaction and prepares for automatic rollback if not explicitly committed.
		/// Usage: <c>using(var trans = new SLTransaction(db)) { ... trans.Commit(); }</c>
		/// </summary>
		/// <param name="db"></param>
		/// <param name="sql">SQL to execute now. Default <c>"BEGIN"</c>. For nested transaction use <c>"SAVEPOINT name"</c>.</param>
		/// <param name="sqlOfDispose">
		/// SQL to execute when disposing the <b>SLTransaction</b> variable if not called <see cref="Commit"/> or <see cref="Rollback"/>.
		/// Default <c>"ROLLBACK"</c>. For nested transaction use <c>"ROLLBACK TO name"</c>.
		/// See also: <see cref="SqlOfDispose"/>.
		/// </param>
		/// <exception cref="SLException">Failed to execute <i>sql</i>.</exception>
		public SLTransaction(sqlite db, string sql = "BEGIN", string sqlOfDispose = "ROLLBACK") {
			Not_.Null(db);
			db.Execute(sql);
			_db = db;
			SqlOfDispose = sqlOfDispose;
		}

		/// <summary>
		/// Calls <see cref="Rollback"/> if not called <see cref="Commit"/> or <see cref="Rollback"/>.
		/// </summary>
		/// <exception cref="SLException">Failed to execute <see cref="SqlOfDispose"/>.</exception>
		public void Dispose() {
			if (_db != null) Rollback(SqlOfDispose);
		}
		
		///
		~SLTransaction() {
			if (_db != null) print.warning($"Non-disposed SLTransaction variable.");
		}

		/// <summary>
		/// Executes a rollback SQL (if in transaction) and disables <see cref="Dispose"/>.
		/// Usually don't need to call this function explicitly. It is implicitly called when disposing this variable if the transaction was not committed.
		/// </summary>
		/// <param name="sql">SQL to execute. Default: <see cref="SqlOfDispose"/>.</param>
		/// <exception cref="SLException">Failed to execute <i>sql</i>.</exception>
		public void Rollback(string sql = null) {
			if (_db == null) throw new InvalidOperationException();
			if (sql == null) sql = SqlOfDispose;
			//if(ErrorMessage!=null) print.it(ErrorMessage);
			if (_db.IsInTransaction) //in some cases sqlite rolls back on error
				_db.Execute(sql);
			_db = null;
		}

		/// <summary>
		/// Executes a commit SQL and disables <see cref="Dispose"/>.
		/// </summary>
		/// <param name="sql">SQL to execute. Default <c>"COMMIT"</c>. For nested transaction use <c>"RELEASE name"</c>.</param>
		/// <exception cref="SLException">Failed to execute <i>sql</i>.</exception>
		public void Commit(string sql = "COMMIT") {
			if (_db == null) throw new InvalidOperationException();
			_db.Execute(sql);
			_db = null;
		}

		/// <summary>
		/// Gets or sets SQL to execute when disposing this variable if not called <see cref="Commit"/> or <see cref="Rollback"/>.
		/// Initially = parameter <c>sqlOfDispose</c> of constructor.
		/// </summary>
		public string SqlOfDispose { get; set; }

		//public string ErrorMessage { get; set; }
	}

	/// <summary>
	/// Used for parameter types of some <see cref="sqliteStatement"/> functions.
	/// Has implicit conversions from <b>int</b> and <b>string</b>. If <b>int</b>, the value is interpreted as index. If <b>string</b> - as name.
	/// </summary>
	public struct SLIndexOrName {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public int index;
		public string name;
		SLIndexOrName(int index) { this.index = index; this.name = null; }
		SLIndexOrName(string name) { this.index = 0; this.name = name; }
		public static implicit operator SLIndexOrName(int index) => new(index);
		public static implicit operator SLIndexOrName(string name) => new(name);
#pragma warning restore CS1591
	}

	/// <summary>
	/// Exception thrown by <see cref="sqlite"/>, <see cref="sqliteStatement"/> and related types.
	/// </summary>
	public class SLException : Exception {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public SLException(string message) : base(message) { }

		internal SLException(SLError r, string prefix = null, string suffix = null)
			: base(SLUtil_.Concat(prefix, SLApi.Errstr(r), suffix)) {
			Debug.Assert(r != 0 && r != SLError.Done && r != SLError.Row);
			ErrorCode = r;
		}

		internal SLException(SLError r, IntPtr db, string prefix = null)
			: base(SLUtil_.Concat(prefix, r, db)) {
			Debug.Assert(r != 0 && r != SLError.Done && r != SLError.Row);
			ErrorCode = r;
		}
#pragma warning restore CS1591

		/// <summary>
		/// The called SQLite API function returned this error code.
		/// </summary>
		public SLError ErrorCode { get; private set; }
	}
}

namespace Au.More {
	internal static unsafe class SLUtil_ {
		internal static string ToStringAndFree(byte* utf8) {
			var r = Convert2.Utf8Decode(utf8);
			SLApi.sqlite3_free(utf8);
			return r;
		}

		//currently not used
		//internal static void Err(SLError r, sqlite db, string func)
		//{
		//	if(r != 0) throw new SLException(r, db, func);
		//}

		internal static void Warn(SLError r, string func, string suffix = null) {
			if (r != 0) print.warning(SLUtil_.Concat(func, SLApi.Errstr(r), suffix));
		}

		internal static string Concat(string s1, string s2, string s3) {
			using (new StringBuilder_(out var b)) {
				_Append(s1); _Append(s2); if (s3 != s2) _Append(s3);
				return b.ToString();

				void _Append(string s) => b.AppendSentence(s, s?.Starts("sqlite3_") ?? false); //avoid uppercase for function names
			}
		}

		internal static string Concat(string s1, SLError r, IntPtr db)
			=> Concat(s1, SLApi.Errstr(r), SLApi.sqlite3_errcode(db) == 0 ? null : SLApi.Errmsg(db));
	}

}