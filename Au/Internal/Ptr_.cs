namespace Au.More;

/// <summary>
/// String functions for unmanaged <c>char*</c> or <c>byte*</c> strings.
/// </summary>
internal static unsafe class Ptr_ {
	#region char*
	
	/// <summary>
	/// Gets span from start to <c>'\0'</c>.
	/// </summary>
	public static RStr ToRSpan(char* p) => MemoryMarshal.CreateReadOnlySpanFromNullTerminated(p);
	
	/// <summary>
	/// Gets the number of characters in <i>p</i> until <c>'\0'</c>.
	/// </summary>
	/// <param name="p"><c>'\0'</c>-terminated string. Can be <c>null</c>.</param>
	public static int Length(char* p) => MemoryMarshal.CreateReadOnlySpanFromNullTerminated(p).Length;
	
	/// <summary>
	/// Gets the number of characters in <i>p</i> until <c>'\0'</c> or <i>max</i>.
	/// </summary>
	/// <param name="p"><c>'\0'</c>-terminated string. Can be null if <i>max</i> is 0.</param>
	/// <param name="max">Max length to scan. Returns <i>max</i> if does not find <c>'\0'</c>.</param>
	public static int Length(char* p, int max) {
		int i = new RStr(p, max).IndexOf('\0');
		return i < 0 ? max : i;
	}
	
	#endregion
	
	#region byte*
	
	/// <summary>
	/// Gets span from start to <c>'\0'</c>.
	/// </summary>
	public static RByte ToRSpan(byte* p) => MemoryMarshal.CreateReadOnlySpanFromNullTerminated(p);
	
	/// <summary>
	/// Gets the number of bytes in <i>p</i> until <c>'\0'</c>.
	/// </summary>
	/// <param name="p"><c>'\0'</c>-terminated string. Can be <c>null</c>.</param>
	public static int Length(byte* p) => MemoryMarshal.CreateReadOnlySpanFromNullTerminated(p).Length;
	
	/// <summary>
	/// Gets the number of bytes in <i>p</i> until <c>'\0'</c> or <i>max</i>.
	/// </summary>
	/// <param name="p"><c>'\0'</c>-terminated string. Can be null if <i>max</i> is 0.</param>
	/// <param name="max">Max length to scan. Returns <i>max</i> if does not find <c>'\0'</c>.</param>
	public static int Length(byte* p, int max) {
		int i = new RByte(p, max).IndexOf((byte)0);
		return i < 0 ? max : i;
	}
	
	#endregion
}
