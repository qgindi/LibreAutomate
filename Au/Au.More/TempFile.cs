namespace Au.More;

/// <summary>
/// Creates unique name for a temporary file and later auto-deletes the file.
/// </summary>
/// <remarks>
/// Use code like in the example to auto-delete the temporary file if exists. Or call <b>Dispose</b>. Else this class does not delete the file.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// using (var f = new TempFile()) {
/// 	print.it(f);
/// 	filesystem.saveText(f, "DATA");
/// 	print.it(filesystem.loadText(f));
/// } //now auto-deletes the file
/// ]]></code>
/// </example>
public sealed class TempFile : IDisposable {
	readonly string _file;

	/// <summary>
	/// Creates full path string with a unique filename (GUID) for a temporary file.
	/// </summary>
	/// <param name="ext">Filename extension with dot. Default: <c>".tmp"</c>. Can be <c>null</c>.</param>
	/// <param name="directory">Parent directory. If <c>null</c> (default), uses <see cref="folders.ThisAppTemp"/>. The function creates the directory if does not exist.</param>
	/// <exception cref="ArgumentException"><i>directory</i> not full path.</exception>
	/// <exception cref="AuException">Failed to create directory.</exception>
	public TempFile(string ext = ".tmp", string directory = null) {
		filesystem.createDirectory(directory ??= folders.ThisAppTemp);
		_file = pathname.combine(directory, Guid.NewGuid().ToString()) + ext;
	}

	/// <summary>
	/// Deletes the file if exists.
	/// </summary>
	/// <remarks>
	/// Does not throw exception if fails to delete.
	/// </remarks>
	public void Dispose() { filesystem.delete(_file, FDFlags.CanFail); }

	/// <summary>
	/// Gets the file path.
	/// </summary>
	public string File => _file;

	/// <summary>
	/// Returns the file path.
	/// </summary>
	public static implicit operator string(TempFile f) => f._file;

	/// <summary>
	/// Returns the file path.
	/// </summary>
	public override string ToString() => _file;
}
