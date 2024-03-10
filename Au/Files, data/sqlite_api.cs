//Most of these declarations are from System.Data.SQLite library. Modified.

namespace Au.Types;

#region enum

/// <summary>
/// Flags for <see cref="sqlite"/> constructor.
/// </summary>
[Flags]
public enum SLFlags {
	/// <summary>Default flags. <sqlite>SQLITE_OPEN_READWRITE</sqlite> and <sqlite>SQLITE_OPEN_CREATE</sqlite>.</summary>
	ReadWriteCreate = SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE,
	
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	SQLITE_OPEN_READONLY = 0x1,
	SQLITE_OPEN_READWRITE = 0x2,
	SQLITE_OPEN_CREATE = 0x4,
	SQLITE_OPEN_URI = 0x40,
	SQLITE_OPEN_NOMUTEX = 0x8000,
	SQLITE_OPEN_FULLMUTEX = 0x10000,
	SQLITE_OPEN_SHAREDCACHE = 0x20000,
	SQLITE_OPEN_PRIVATECACHE = 0x40000,
#pragma warning restore CS1591
}

/// <summary>
/// SQLite API error codes. Also two success codes - Row and Done.
/// </summary>
public enum SLError {
	/// <summary>
	/// Successful result
	/// </summary>
	Ok /* 0 */,
	/// <summary>
	/// SQL error or missing database
	/// </summary>
	Error /* 1 */,
	/// <summary>
	/// Internal logic error in SQLite
	/// </summary>
	Internal /* 2 */,
	/// <summary>
	/// Access permission denied
	/// </summary>
	Perm /* 3 */,
	/// <summary>
	/// Callback routine requested an abort
	/// </summary>
	Abort /* 4 */,
	/// <summary>
	/// The database file is locked
	/// </summary>
	Busy /* 5 */,
	/// <summary>
	/// A table in the database is locked
	/// </summary>
	Locked /* 6 */,
	/// <summary>
	/// A malloc() failed
	/// </summary>
	NoMem /* 7 */,
	/// <summary>
	/// Attempt to write a readonly database
	/// </summary>
	ReadOnly /* 8 */,
	/// <summary>
	/// Operation terminated by <b>sqlite3_interrupt</b>
	/// </summary>
	Interrupt /* 9 */,
	/// <summary>
	/// Some kind of disk I/O error occurred
	/// </summary>
	IoErr /* 10 */,
	/// <summary>
	/// The database disk image is malformed
	/// </summary>
	Corrupt /* 11 */,
	/// <summary>
	/// Unknown opcode in <b>sqlite3_file_control</b>
	/// </summary>
	NotFound /* 12 */,
	/// <summary>
	/// Insertion failed because database is full
	/// </summary>
	Full /* 13 */,
	/// <summary>
	/// Unable to open the database file
	/// </summary>
	CantOpen /* 14 */,
	/// <summary>
	/// Database lock protocol error
	/// </summary>
	Protocol /* 15 */,
	/// <summary>
	/// Database is empty
	/// </summary>
	Empty /* 16 */,
	/// <summary>
	/// The database schema changed
	/// </summary>
	Schema /* 17 */,
	/// <summary>
	/// String or BLOB exceeds size limit
	/// </summary>
	TooBig /* 18 */,
	/// <summary>
	/// Abort due to constraint violation
	/// </summary>
	Constraint /* 19 */,
	/// <summary>
	/// Data type mismatch
	/// </summary>
	Mismatch /* 20 */,
	/// <summary>
	/// Library used incorrectly
	/// </summary>
	Misuse /* 21 */,
	/// <summary>
	/// Uses OS features not supported on host
	/// </summary>
	NoLfs /* 22 */,
	/// <summary>
	/// Authorization denied
	/// </summary>
	Auth /* 23 */,
	/// <summary>
	/// Auxiliary database format error
	/// </summary>
	Format /* 24 */,
	/// <summary>
	/// 2nd parameter to <b>sqlite3_bind</b> out of range
	/// </summary>
	Range /* 25 */,
	/// <summary>
	/// File opened that is not a database file
	/// </summary>
	NotADb /* 26 */,
	/// <summary>
	/// Notifications from <b>sqlite3_log</b>
	/// </summary>
	Notice /* 27 */,
	/// <summary>
	/// Warnings from <b>sqlite3_log</b>
	/// </summary>
	Warning /* 28 */,
	/// <summary>
	/// <b>sqlite3_step</b> has another row ready
	/// </summary>
	Row = 100,
	/// <summary>
	/// <b>sqlite3_step</b> has finished executing
	/// </summary>
	Done /* 101 */,
	///// <summary>
	///// Used to mask off extended result codes
	///// </summary>
	//NonExtendedMask = 0xFF,
	
#if false
	/////////////////////////////////////////////////////////////////////////
	// BEGIN EXTENDED RESULT CODES
	/////////////////////////////////////////////////////////////////////////

	/// <summary>
	/// A collation sequence was referenced by a schema and it cannot be
	/// found.
	/// </summary>
	Error_Missing_CollSeq = (Error | (1 << 8)),
	/// <summary>
	/// An internal operation failed and it may succeed if retried.
	/// </summary>
	Error_Retry = (Error | (2 << 8)),
	/// <summary>
	/// A file read operation failed.
	/// </summary>
	IoErr_Read = (IoErr | (1 << 8)),
	/// <summary>
	/// A file read operation returned less data than requested.
	/// </summary>
	IoErr_Short_Read = (IoErr | (2 << 8)),
	/// <summary>
	/// A file write operation failed.
	/// </summary>
	IoErr_Write = (IoErr | (3 << 8)),
	/// <summary>
	/// A file synchronization operation failed.
	/// </summary>
	IoErr_Fsync = (IoErr | (4 << 8)),
	/// <summary>
	/// A directory synchronization operation failed.
	/// </summary>
	IoErr_Dir_Fsync = (IoErr | (5 << 8)),
	/// <summary>
	/// A file truncate operation failed.
	/// </summary>
	IoErr_Truncate = (IoErr | (6 << 8)),
	/// <summary>
	/// A file metadata operation failed.
	/// </summary>
	IoErr_Fstat = (IoErr | (7 << 8)),
	/// <summary>
	/// A file unlock operation failed.
	/// </summary>
	IoErr_Unlock = (IoErr | (8 << 8)),
	/// <summary>
	/// A file lock operation failed.
	/// </summary>
	IoErr_RdLock = (IoErr | (9 << 8)),
	/// <summary>
	/// A file delete operation failed.
	/// </summary>
	IoErr_Delete = (IoErr | (10 << 8)),
	/// <summary>
	/// Not currently used.
	/// </summary>
	IoErr_Blocked = (IoErr | (11 << 8)),
	/// <summary>
	/// Out-of-memory during a file operation.
	/// </summary>
	IoErr_NoMem = (IoErr | (12 << 8)),
	/// <summary>
	/// A file existence/status operation failed.
	/// </summary>
	IoErr_Access = (IoErr | (13 << 8)),
	/// <summary>
	/// A check for a reserved lock failed.
	/// </summary>
	IoErr_CheckReservedLock = (IoErr | (14 << 8)),
	/// <summary>
	/// A file lock operation failed.
	/// </summary>
	IoErr_Lock = (IoErr | (15 << 8)),
	/// <summary>
	/// A file close operation failed.
	/// </summary>
	IoErr_Close = (IoErr | (16 << 8)),
	/// <summary>
	/// A directory close operation failed.
	/// </summary>
	IoErr_Dir_Close = (IoErr | (17 << 8)),
	/// <summary>
	/// A shared memory open operation failed.
	/// </summary>
	IoErr_ShmOpen = (IoErr | (18 << 8)),
	/// <summary>
	/// A shared memory size operation failed.
	/// </summary>
	IoErr_ShmSize = (IoErr | (19 << 8)),
	/// <summary>
	/// A shared memory lock operation failed.
	/// </summary>
	IoErr_ShmLock = (IoErr | (20 << 8)),
	/// <summary>
	/// A shared memory map operation failed.
	/// </summary>
	IoErr_ShmMap = (IoErr | (21 << 8)),
	/// <summary>
	/// A file seek operation failed.
	/// </summary>
	IoErr_Seek = (IoErr | (22 << 8)),
	/// <summary>
	/// A file delete operation failed because it does not exist.
	/// </summary>
	IoErr_Delete_NoEnt = (IoErr | (23 << 8)),
	/// <summary>
	/// A file memory mapping operation failed.
	/// </summary>
	IoErr_Mmap = (IoErr | (24 << 8)),
	/// <summary>
	/// The temporary directory path could not be obtained.
	/// </summary>
	IoErr_GetTempPath = (IoErr | (25 << 8)),
	/// <summary>
	/// A path string conversion operation failed.
	/// </summary>
	IoErr_ConvPath = (IoErr | (26 << 8)),
	/// <summary>
	/// Reserved.
	/// </summary>
	IoErr_VNode = (IoErr | (27 << 8)),
	/// <summary>
	/// An attempt to authenticate failed.
	/// </summary>
	IoErr_Auth = (IoErr | (28 << 8)),
	/// <summary>
	/// An attempt to begin a file system transaction failed.
	/// </summary>
	IoErr_Begin_Atomic = (IoErr | (29 << 8)),
	/// <summary>
	/// An attempt to commit a file system transaction failed.
	/// </summary>
	IoErr_Commit_Atomic = (IoErr | (30 << 8)),
	/// <summary>
	/// An attempt to rollback a file system transaction failed.
	/// </summary>
	IoErr_Rollback_Atomic = (IoErr | (31 << 8)),
	/// <summary>
	/// A database table is locked in shared-cache mode.
	/// </summary>
	Locked_SharedCache = (Locked | (1 << 8)),
	/// <summary>
	/// A virtual table in the database is locked.
	/// </summary>
	Locked_Vtab = (Locked | (2 << 8)),
	/// <summary>
	/// A database file is locked due to a recovery operation.
	/// </summary>
	Busy_Recovery = (Busy | (1 << 8)),
	/// <summary>
	/// A database file is locked due to snapshot semantics.
	/// </summary>
	Busy_Snapshot = (Busy | (2 << 8)),
	/// <summary>
	/// A database file cannot be opened because no temporary directory is available.
	/// </summary>
	CantOpen_NoTempDir = (CantOpen | (1 << 8)),
	/// <summary>
	/// A database file cannot be opened because its path represents a directory.
	/// </summary>
	CantOpen_IsDir = (CantOpen | (2 << 8)),
	/// <summary>
	/// A database file cannot be opened because its full path could not be obtained.
	/// </summary>
	CantOpen_FullPath = (CantOpen | (3 << 8)),
	/// <summary>
	/// A database file cannot be opened because a path string conversion operation failed.
	/// </summary>
	CantOpen_ConvPath = (CantOpen | (4 << 8)),
	/// <summary>
	/// A virtual table is malformed.
	/// </summary>
	Corrupt_Vtab = (Corrupt | (1 << 8)),
	/// <summary>
	/// A required sequence table is missing or corrupt.
	/// </summary>
	Corrupt_Sequence = (Corrupt | (2 << 8)),
	/// <summary>
	/// A database file is read-only due to a recovery operation.
	/// </summary>
	ReadOnly_Recovery = (ReadOnly | (1 << 8)),
	/// <summary>
	/// A database file is read-only because a lock could not be obtained.
	/// </summary>
	ReadOnly_CantLock = (ReadOnly | (2 << 8)),
	/// <summary>
	/// A database file is read-only because it needs rollback processing.
	/// </summary>
	ReadOnly_Rollback = (ReadOnly | (3 << 8)),
	/// <summary>
	/// A database file is read-only because it was moved while open.
	/// </summary>
	ReadOnly_DbMoved = (ReadOnly | (4 << 8)),
	/// <summary>
	/// The shared-memory file is read-only and it should be read-write.
	/// </summary>
	ReadOnly_CantInit = (ReadOnly | (5 << 8)),
	/// <summary>
	/// Unable to create journal file because the directory is read-only.
	/// </summary>
	ReadOnly_Directory = (ReadOnly | (6 << 8)),
	/// <summary>
	/// An operation is being aborted due to rollback processing.
	/// </summary>
	Abort_Rollback = (Abort | (2 << 8)),
	/// <summary>
	/// A <c>CHECK</c> constraint failed.
	/// </summary>
	Constraint_Check = (Constraint | (1 << 8)),
	/// <summary>
	/// A commit hook produced a unsuccessful return code.
	/// </summary>
	Constraint_CommitHook = (Constraint | (2 << 8)),
	/// <summary>
	/// A <c>FOREIGN KEY</c> constraint failed.
	/// </summary>
	Constraint_ForeignKey = (Constraint | (3 << 8)),
	/// <summary>
	/// Not currently used.
	/// </summary>
	Constraint_Function = (Constraint | (4 << 8)),
	/// <summary>
	/// A <c>NOT NULL</c> constraint failed.
	/// </summary>
	Constraint_NotNull = (Constraint | (5 << 8)),
	/// <summary>
	/// A <c>PRIMARY KEY</c> constraint failed.
	/// </summary>
	Constraint_PrimaryKey = (Constraint | (6 << 8)),
	/// <summary>
	/// The <c>RAISE</c> function was used by a trigger-program.
	/// </summary>
	Constraint_Trigger = (Constraint | (7 << 8)),
	/// <summary>
	/// A <c>UNIQUE</c> constraint failed.
	/// </summary>
	Constraint_Unique = (Constraint | (8 << 8)),
	/// <summary>
	/// Not currently used.
	/// </summary>
	Constraint_Vtab = (Constraint | (9 << 8)),
	/// <summary>
	/// A <c>ROWID</c> constraint failed.
	/// </summary>
	Constraint_RowId = (Constraint | (10 << 8)),
	/// <summary>
	/// Frames were recovered from the WAL log file.
	/// </summary>
	Notice_Recover_Wal = (Notice | (1 << 8)),
	/// <summary>
	/// Pages were recovered from the journal file.
	/// </summary>
	Notice_Recover_Rollback = (Notice | (2 << 8)),
	/// <summary>
	/// An automatic index was created to process a query.
	/// </summary>
	Warning_AutoIndex = (Warning | (1 << 8)),
	/// <summary>
	/// User authentication failed.
	/// </summary>
	Auth_User = (Auth | (1 << 8)),
	/// <summary>
	/// Success.  Prevents the extension from unloading until the process
	/// terminates.
	/// </summary>
	Ok_Load_Permanently = (Ok | (1 << 8))
#endif
}

#if false
	// These are the options to the internal sqlite3_config call.
internal enum Config
{
	SQLITE_CONFIG_NONE = 0, // nil
	SQLITE_CONFIG_SINGLETHREAD = 1, // nil
	SQLITE_CONFIG_MULTITHREAD = 2, // nil
	SQLITE_CONFIG_SERIALIZED = 3, // nil
	SQLITE_CONFIG_MALLOC = 4, // sqlite3_mem_methods*
	SQLITE_CONFIG_GETMALLOC = 5, // sqlite3_mem_methods*
	SQLITE_CONFIG_SCRATCH = 6, // void*, int sz, int N
	SQLITE_CONFIG_PAGECACHE = 7, // void*, int sz, int N
	SQLITE_CONFIG_HEAP = 8, // void*, int nByte, int min
	SQLITE_CONFIG_MEMSTATUS = 9, // boolean
	SQLITE_CONFIG_MUTEX = 10, // sqlite3_mutex_methods*
	SQLITE_CONFIG_GETMUTEX = 11, // sqlite3_mutex_methods*
								 // previously SQLITE_CONFIG_CHUNKALLOC 12 which is now unused
	SQLITE_CONFIG_LOOKASIDE = 13, // int int
	SQLITE_CONFIG_PCACHE = 14, // sqlite3_pcache_methods*
	SQLITE_CONFIG_GETPCACHE = 15, // sqlite3_pcache_methods*
	SQLITE_CONFIG_LOG = 16, // xFunc, void*
	SQLITE_CONFIG_URI = 17, // int
	SQLITE_CONFIG_PCACHE2 = 18, // sqlite3_pcache_methods2*
	SQLITE_CONFIG_GETPCACHE2 = 19, // sqlite3_pcache_methods2*
	SQLITE_CONFIG_COVERING_INDEX_SCAN = 20, // int
	SQLITE_CONFIG_SQLLOG = 21, // xSqllog, void*
	SQLITE_CONFIG_MMAP_SIZE = 22, // sqlite3_int64, sqlite3_int64
	SQLITE_CONFIG_WIN32_HEAPSIZE = 23, // int nByte
	SQLITE_CONFIG_PCACHE_HDRSZ = 24, // int *psz
	SQLITE_CONFIG_PMASZ = 25 // unsigned int szPma
}

internal enum ConfigDb
{
	/// <summary>
	/// This value represents an unknown (or invalid) option, do not use it.
	/// </summary>
	SQLITE_DBCONFIG_NONE = 0, // nil

	/// <summary>
	/// This option is used to change the name of the "main" database
	/// schema.  The sole argument is a pointer to a constant UTF-8 string
	/// which will become the new schema name in place of "main".
	/// </summary>
	SQLITE_DBCONFIG_MAINDBNAME = 1000, // char*

	/// <summary>
	/// This option is used to configure the lookaside memory allocator.
	/// The value must be an array with three elements.  The second element
	/// must be an <see cref="Int32" /> containing the size of each buffer
	/// slot.  The third element must be an <see cref="Int32" /> containing
	/// the number of slots.  The first element must be an <see cref="IntPtr" />
	/// that points to a native memory buffer of bytes equal to or greater
	/// than the product of the second and third element values.
	/// </summary>
	SQLITE_DBCONFIG_LOOKASIDE = 1001, // void* int int

	/// <summary>
	/// This option is used to enable or disable the enforcement of
	/// foreign key constraints.
	/// </summary>
	SQLITE_DBCONFIG_ENABLE_FKEY = 1002, // int int*

	/// <summary>
	/// This option is used to enable or disable triggers.
	/// </summary>
	SQLITE_DBCONFIG_ENABLE_TRIGGER = 1003, // int int*

	/// <summary>
	/// This option is used to enable or disable the two-argument version
	/// of the <b>fts3_tokenizer</b> function which is part of the FTS3 full-text
	/// search engine extension.
	/// </summary>
	SQLITE_DBCONFIG_ENABLE_FTS3_TOKENIZER = 1004, // int int*

	/// <summary>
	/// This option is used to enable or disable the loading of extensions.
	/// </summary>
	SQLITE_DBCONFIG_ENABLE_LOAD_EXTENSION = 1005, // int int*

	/// <summary>
	/// This option is used to enable or disable the automatic checkpointing
	/// when a WAL database is closed.
	/// </summary>
	SQLITE_DBCONFIG_NO_CKPT_ON_CLOSE = 1006, // int int*

	/// <summary>
	/// This option is used to enable or disable the query planner stability
	/// guarantee (QPSG).
	/// </summary>
	SQLITE_DBCONFIG_ENABLE_QPSG = 1007, // int int*

	/// <summary>
	/// This option is used to enable or disable the extra <c>EXPLAIN QUERY PLAN</c>
	/// output for trigger programs.
	/// </summary>
	SQLITE_DBCONFIG_TRIGGER_EQP = 1008, // int int*

	/// <summary>
	/// This option is used as part of the process to reset a database back
	/// to an empty state.  Because resetting a database is destructive and
	/// irreversible, the process requires the use of this obscure flag and
	/// multiple steps to help ensure that it does not happen by accident.
	/// </summary>
	SQLITE_DBCONFIG_RESET_DATABASE = 1009 // int int*
}

internal enum TypeAffinity
{
	/// <summary>
	/// All integers in SQLite default to Int64
	/// </summary>
	Int64 = 1,
	/// <summary>
	/// All floating point numbers in SQLite default to double
	/// </summary>
	Double = 2,
	/// <summary>
	/// The default data type of SQLite is text
	/// </summary>
	Text = 3,
	/// <summary>
	/// Typically blob types are only seen when returned from a function
	/// </summary>
	Blob = 4,
	/// <summary>
	/// Null types can be returned from functions
	/// </summary>
	Null = 5,
}
#endif

#endregion

#region functions

internal static unsafe class SLApi {
	const string SQLITE_DLL = "sqlite3.dll";
	
	static SLApi() {
		filesystem.more.LoadDll64or32Bit_(SQLITE_DLL);
	}
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_close_v2(IntPtr db);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_create_function(IntPtr db, byte[] strName, int nArgs, int nType, IntPtr pvUser, SQLiteCallback func, SQLiteCallback fstep, SQLiteFinalCallback ffinal);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_finalize(IntPtr stmt);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_backup_finish(IntPtr backup);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_reset(IntPtr stmt);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern byte* sqlite3_bind_parameter_name(IntPtr stmt, int index);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern byte* sqlite3_column_database_name(IntPtr stmt, int index);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern byte* sqlite3_column_decltype(IntPtr stmt, int index);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern byte* sqlite3_column_name(IntPtr stmt, int index);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern byte* sqlite3_column_origin_name(IntPtr stmt, int index);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern byte* sqlite3_column_table_name(IntPtr stmt, int index);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern byte* sqlite3_column_text(IntPtr stmt, int index);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern char* sqlite3_column_text16(IntPtr stmt, int index);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern byte* sqlite3_errmsg(IntPtr db);
	
	internal static string Errmsg(IntPtr db) => Convert2.Utf8Decode(sqlite3_errmsg(db));
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_prepare16_v3(IntPtr db, char* sql, int nBytes, int prepFlags, ref IntPtr stmt, char** ptrRemain);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_table_column_metadata(IntPtr db, byte[] dbName, byte[] tblName, byte[] colName, byte** ptrDataType, byte** ptrCollSeq, ref int notNull, ref int primaryKey, ref int autoInc);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern byte* sqlite3_value_text(IntPtr p);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern byte* sqlite3_libversion();
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_libversion_number();
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern byte* sqlite3_sourceid();
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_compileoption_used(byte[] zOptName);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern byte* sqlite3_compileoption_get(int N);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_enable_shared_cache(int enable);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_enable_load_extension(IntPtr db, int enable);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_load_extension(IntPtr db, byte[] fileName, byte[] procName, byte** pError);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_overload_function(IntPtr db, byte[] zName, int nArgs);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_win32_set_directory16(uint type, string value);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_win32_reset_heap();
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_win32_compact_heap(ref uint largest);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void* sqlite3_malloc(int n);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void* sqlite3_malloc64(ulong n);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void* sqlite3_realloc(void* p, int n);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void* sqlite3_realloc64(void* p, ulong n);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern ulong sqlite3_msize(void* p);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern void sqlite3_free(void* p);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_open_v2(byte[] filename, ref IntPtr db, SLFlags flags, byte[] vfsName);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_interrupt(IntPtr db);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern long sqlite3_last_insert_rowid(IntPtr db);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern int sqlite3_changes(IntPtr db);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern long sqlite3_memory_used();
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern long sqlite3_memory_highwater(int resetFlag);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_shutdown();
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_busy_timeout(IntPtr db, int ms);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_clear_bindings(IntPtr stmt);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	static extern SLError sqlite3_bind_blob64(IntPtr stmt, int index, void* value, long nSize, nint nTransient);
	
	internal static SLError sqlite3_bind_blob64(IntPtr stmt, int index, void* value, long nSize)
		=> sqlite3_bind_blob64(stmt, index, value, nSize, -1); //SQLITE_TRANSIENT //FUTURE: somehow avoid copying if large data. Eg if array or List, can use callback and GCHandle.
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_bind_double(IntPtr stmt, int index, double value);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_bind_int(IntPtr stmt, int index, int value);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_bind_int64(IntPtr stmt, int index, long value);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_bind_null(IntPtr stmt, int index);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	static extern SLError sqlite3_bind_text16(IntPtr stmt, int index, string value, int nlen, nint nTransient);
	
	internal static SLError sqlite3_bind_text16(IntPtr stmt, int index, string value, int nlen)
		=> sqlite3_bind_text16(stmt, index, value, nlen, -1); //SQLITE_TRANSIENT
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_bind_zeroblob(IntPtr stmt, int index, int n);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_bind_parameter_count(IntPtr stmt);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern int sqlite3_bind_parameter_index(IntPtr stmt, byte[] strName);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern int sqlite3_column_count(IntPtr stmt);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_step(IntPtr stmt);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_stmt_readonly(IntPtr stmt);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern double sqlite3_column_double(IntPtr stmt, int index);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern int sqlite3_column_int(IntPtr stmt, int index);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern long sqlite3_column_int64(IntPtr stmt, int index);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern void* sqlite3_column_blob(IntPtr stmt, int index);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern int sqlite3_column_bytes(IntPtr stmt, int index);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern int sqlite3_column_bytes16(IntPtr stmt, int index);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern TypeAffinity sqlite3_column_type(IntPtr stmt, int index);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_create_collation(IntPtr db, byte[] strName, int nType, IntPtr pvUser, SQLiteCollation func);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void* sqlite3_value_blob(IntPtr p);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_value_bytes(IntPtr p);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_value_bytes16(IntPtr p);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern double sqlite3_value_double(IntPtr p);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_value_int(IntPtr p);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern long sqlite3_value_int64(IntPtr p);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern TypeAffinity sqlite3_value_type(IntPtr p);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_blob(IntPtr context, byte[] value, int nSize, IntPtr pvReserved);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_double(IntPtr context, double value);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_error(IntPtr context, byte[] strErr, int nLen);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_error_code(IntPtr context, SLError value);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_error_toobig(IntPtr context);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_error_nomem(IntPtr context);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_value(IntPtr context, IntPtr value);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_zeroblob(IntPtr context, int nLen);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_int(IntPtr context, int value);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_int64(IntPtr context, long value);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_null(IntPtr context);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr sqlite3_aggregate_context(IntPtr context, int nBytes);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_error16(IntPtr context, string strName, int nLen);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_result_text16(IntPtr context, string strName, int nLen, IntPtr pvReserved);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_progress_handler(IntPtr db, int ops, SQLiteProgressCallback func, IntPtr pvUser);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr sqlite3_set_authorizer(IntPtr db, SQLiteAuthorizerCallback func, IntPtr pvUser);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr sqlite3_update_hook(IntPtr db, SQLiteUpdateCallback func, IntPtr pvUser);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr sqlite3_commit_hook(IntPtr db, SQLiteCommitCallback func, IntPtr pvUser);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr sqlite3_trace(IntPtr db, SQLiteTraceCallback func, IntPtr pvUser);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr sqlite3_trace_v2(IntPtr db, SQLiteTraceFlags mask, SQLiteTraceCallback2 func, IntPtr pvUser);
	
	// Since sqlite3_config() takes a variable argument list, we have to overload declarations
	// for all possible calls that we want to use.
	//[DllImport(SQLITE_DLL, EntryPoint = "sqlite3_config", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_config_none(Config op);
	
	//[DllImport(SQLITE_DLL, EntryPoint = "sqlite3_config", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_config_int(Config op, int value);
	
	////[DllImport(SQLITE_DLL, EntryPoint = "sqlite3_config", CallingConvention = CallingConvention.Cdecl)]
	////internal static extern SLError sqlite3_config_log(Config op, SQLiteLogCallback func, IntPtr pvUser);
	
	//[DllImport(SQLITE_DLL, EntryPoint = "sqlite3_db_config", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_db_config_charptr(IntPtr db, ConfigDb op, IntPtr charPtr);
	
	//[DllImport(SQLITE_DLL, EntryPoint = "sqlite3_db_config", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_db_config_int_refint(IntPtr db, ConfigDb op, int value, ref int result);
	
	//[DllImport(SQLITE_DLL, EntryPoint = "sqlite3_db_config", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_db_config_intptr_two_ints(IntPtr db, ConfigDb op, IntPtr ptr, int int0, int int1);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_db_status(IntPtr db, SQLiteStatusOpsEnum op, ref int current, ref int highwater, int resetFlag);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr sqlite3_rollback_hook(IntPtr db, SQLiteRollbackCallback func, IntPtr pvUser);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr sqlite3_db_handle(IntPtr stmt);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_db_release_memory(IntPtr db);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr sqlite3_db_filename(IntPtr db, byte[] dbName);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_db_readonly(IntPtr db, IntPtr dbName);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr sqlite3_next_stmt(IntPtr db, IntPtr stmt);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_exec(IntPtr db, byte[] strSql, IntPtr pvCallback, IntPtr pvParam, byte** errMsg);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_release_memory(int nBytes);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern int sqlite3_get_autocommit(IntPtr db);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_extended_result_codes(IntPtr db, int onoff);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern SLError sqlite3_errcode(IntPtr db);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_extended_errcode(IntPtr db);
	
	[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	internal static extern byte* sqlite3_errstr(SLError rc);
	
	internal static string Errstr(SLError r) => Convert2.Utf8Decode(sqlite3_errstr(r));
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void sqlite3_log(SLError iErrCode, byte[] zFormat);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_file_control(IntPtr db, byte[] zDbName, int op, IntPtr pArg);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr sqlite3_backup_init(IntPtr destDb, byte[] zDestName, IntPtr sourceDb, byte[] zSourceName);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_backup_step(IntPtr backup, int nPage);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_backup_remaining(IntPtr backup);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_backup_pagecount(IntPtr backup);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_blob_close(IntPtr blob);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int sqlite3_blob_bytes(IntPtr blob);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_blob_open(IntPtr db, byte[] dbName, byte[] tblName, byte[] colName, long rowId, int flags, ref IntPtr ptrBlob);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_blob_read(IntPtr blob, [MarshalAs(UnmanagedType.LPArray)] byte[] buffer, int count, int offset);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_blob_reopen(IntPtr blob, long rowId);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_blob_write(IntPtr blob, [MarshalAs(UnmanagedType.LPArray)] byte[] buffer, int count, int offset);
	
	//[DllImport(SQLITE_DLL, CallingConvention = CallingConvention.Cdecl)]
	//internal static extern SLError sqlite3_declare_vtab(IntPtr db, byte[] zSQL);
	
}

#endregion
