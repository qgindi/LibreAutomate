
/// <summary>
/// Opens databases ref.db, doc.db or winapi.db.
/// To create databases, run scripts "Create .NET ref and doc databases.cs" and "SDK create database".
/// </summary>
static class EdDatabases
{
	public static sqlite OpenRef() => _Open("ref.db");

	public static sqlite OpenDoc() => _Open("doc.db");

	public static sqlite OpenWinapi() => _Open("winapi.db");

	static sqlite _Open(string name) {
		var path = folders.ThisAppBS + name;
		if (App.IsAuHomePC) {
			var pathNew = path + ".new";
			if(filesystem.exists(pathNew)) filesystem.move(pathNew, path, FIfExists.Delete);
		}
		return new sqlite(path, SLFlags.SQLITE_OPEN_READONLY);
	}
}
