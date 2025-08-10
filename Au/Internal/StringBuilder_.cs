namespace Au.More;

/// <summary>
/// Provides a cached reusable instance of <c>StringBuilder</c> per thread. It's an optimization that reduces the number of instances constructed and collected.
/// Used like <c>using(new StringBuilder_(out var b)) { b.Append("example"); var s = b.ToString(); }</c>.
/// </summary>
/// <remarks>
/// This is a modified copy of the .NET internal <c>StringBuilderCache</c> class.
/// </remarks>
internal struct StringBuilder_ : IDisposable {
	StringBuilder _sb;
	
	/// <summary>
	/// The cached <c>StringBuilder</c> has this <c>Capacity</c>. The cache is not used if <i>capacity</i> is bigger.
	/// </summary>
	public const int Capacity = 2000;
	
	[ThreadStatic] private static StringBuilder t_cached;
	
	/// <summary>
	/// Gets a new or cached/cleared <c>StringBuilder</c> of the specified or bigger capacity.
	/// </summary>
	/// <param name="capacity">
	/// Min needed <c>StringBuilder.Capacity</c>. If less than <c>Capacity</c> (2000), uses <c>Capacity</c>. If more than <c>Capacity</c>, does not use the cache.
	/// Use this parameter only when the needed capacity is variable; else either use default if need default or smaller, or don't use <c>StringBuilder_</c> if need bigger.
	/// </param>
	public StringBuilder_(out StringBuilder sb, int capacity = Capacity) {
		if (capacity <= Capacity) {
			capacity = Capacity;
			var b = t_cached;
			if (b != null) {
				t_cached = null;
				b.Clear();
				sb = _sb = b;
				return;
			}
		}
		sb = _sb = new StringBuilder(capacity);
	}
	
	/// <summary>
	/// Releases the <c>StringBuilder</c> to the cache.
	/// </summary>
	public void Dispose() {
		if (_sb.Capacity == Capacity) t_cached = _sb;
		_sb = null;
	}
}
