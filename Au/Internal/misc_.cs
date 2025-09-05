namespace Au.More;

/// <summary>
/// Dictionary with case-insensitive string keys (uses <see cref="StringComparer.OrdinalIgnoreCase"/>).
/// </summary>
/// <remarks>
/// This class can be used instead of <see cref="Dictionary{TKey, TValue}"/>.
/// Unlike <c>Dictionary</c>, keys remain case-insensitive after deserialization (<see cref="System.Text.Json.JsonSerializer"/>, <see cref="JSettings"/> etc).
/// Another way to deserialize case-insensitive dictionaries - <c>JsonObjectCreationHandling.Populate</c> in serialization options or attribute.
/// </remarks>
class DictionaryI_<TValue> : Dictionary<string, TValue> {
	/// <summary>
	/// Calls base constructor with <see cref="StringComparer.OrdinalIgnoreCase"/>.
	/// </summary>
	public DictionaryI_() : base(StringComparer.OrdinalIgnoreCase) { }
	
	/// <summary>
	/// Calls base constructor with <see cref="StringComparer.OrdinalIgnoreCase"/>.
	/// </summary>
	public DictionaryI_(int capacity) : base(capacity, StringComparer.OrdinalIgnoreCase) { }
}
