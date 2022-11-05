
/// <summary>
/// Misc util functions.
/// </summary>
static class EdUtil
{

}

/// <summary>
/// Can be used to return bool and error text. Has implicit conversions from/to bool.
/// </summary>
record struct BoolError(bool ok, string error) {
	public static implicit operator bool(BoolError x) => x.ok;
	public static implicit operator BoolError(bool ok) => new(ok, ok ? null : "Failed.");
}

#if DEBUG

static class EdDebug
{
}

#endif
