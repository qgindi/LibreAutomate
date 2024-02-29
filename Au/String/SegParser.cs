//Modified version of Microsoft.Extensions.Primitives.StringSegment. It is from github; current .NET does not have it, need to get from NuGet.
//Can be used instead of String.Split, especially when you want less garbage. Faster (the github version with StringTokenizer was slower).

namespace Au.More {
	/// <summary>
	/// Splits a string into substrings as start/end offsets or strings.
	/// </summary>
	/// <remarks>
	/// Can be used with foreach. Normally you don't create <b>SegParser</b> instances explicitly; instead use <see cref="ExtString.Segments"/> with foreach.
	/// </remarks>
	public struct SegParser : IEnumerable<StartEnd>, IEnumerator<StartEnd> {
		readonly string _separators;
		readonly string _s;
		readonly int _sStart, _sEnd;
		int _start, _end;
		ushort _sepLength;
		SegFlags _flags;

		/// <summary>
		/// Initializes this instance to split a string.
		/// </summary>
		/// <param name="s">The string.</param>
		/// <param name="separators">A string containing characters that delimit substrings. Or one of <see cref="SegSep"/> constants.</param>
		/// <param name="flags"></param>
		/// <param name="range">Part of the string to split.</param>
		public SegParser(string s, string separators, SegFlags flags = 0, Range? range = null) {
			_separators = separators;
			_s = s;
			_sepLength = 1;
			_flags = flags;
			(_sStart, _sEnd) = range.GetStartEnd(s.Length);
			_start = 0;
			_end = _sStart - 1;
		}
		//FUTURE: support multiple separator chars and strings.
		//	Eg if separators starts with 2 same chars, that char is used to delimit separators, like "||,|;|substring".

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		[NoDoc]
		public SegParser GetEnumerator() => this;

		IEnumerator<StartEnd> IEnumerable<StartEnd>.GetEnumerator() => this;

		IEnumerator IEnumerable.GetEnumerator() => this;

		[NoDoc]
		public StartEnd Current => new(_start, _end);

		object IEnumerator.Current => Current;

		[NoDoc]
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public bool MoveNext() {
			gStart:
			int i = _end + _sepLength, to = _sEnd;
			if (i > to) return false;
			_start = i;
			string s = _s, sep = _separators;
			switch (sep.Length) {
			case 1: {
					var c = sep[0];
					for (; i < to; i++) if (s[i] == c) goto g1;
				}
				break;
			case 22:
				if (ReferenceEquals(sep, SegSep.Whitespace)) {
					for (; i < to; i++)
						if (char.IsWhiteSpace(s[i])) goto g1;
				} else if (ReferenceEquals(sep, SegSep.Word)) {
					for (; i < to; i++)
						if (!char.IsLetterOrDigit(s[i])) goto g1;
				} else if (ReferenceEquals(sep, SegSep.Line)) {
					_sepLength = 1;
					for (; i < to; i++) {
						var c = s[i];
						if (c > '\r') continue;
						if (c == '\r') goto g2; else if (c == '\n') goto g1;
					}
					break;
					g2:
					if (i < to - 1 && s[i + 1] == '\n') _sepLength = 2;
					break;
				} else goto default;
				break;
			default: {
					for (; i < to; i++) {
						var c = s[i];
						for (int j = 0; j < sep.Length; j++) if (c == sep[j]) goto g1; //speed: reverse slower
					}
				}
				break;
			}
			g1:
			_end = i;
			if (i == _start && 0 != (_flags & SegFlags.NoEmpty)) goto gStart;
			return true;
		}

		void IDisposable.Dispose() {
			//rejected. Normally this variable is not reused because GetEnumerator returns a copy.
			//_end = _sStart - 1;
		}

		[NoDoc]
		public void Reset() {
			_end = _sStart - 1;
		}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>
		/// Returns segment values as string[].
		/// </summary>
		/// <param name="maxCount">The maximal number of substrings to get. If negative (default), gets all. Else if there are more substrings, the last element will contain single substring, unlike with <see cref="String.Split"/>.</param>
		public unsafe string[] ToStringArray(int maxCount = -1) {
			//All this big code is just to make this function as fast as String.Split. Also less garbage.
			//	Simple code is slower when substrings are very short.
			//	With short substrings, the parsing code takes less than half of time. Creating strings and arrays is the slow part.
			//	As an intermediate buffer we use a threadlocal array. It's the fastest known way.

			string[] a1 = t_a1 ??= new string[c_a1Size]; //at first use the threadlocal array. When it is filled, use a2.
			List<string> a2 = null;

			int n = 0;
			for (; MoveNext(); n++) { //slightly faster than foreach
				if (maxCount >= 0 && n == maxCount) break;
				var s = _s[_start.._end];
				if (n < c_a1Size) {
					a1[n] = s;
				} else {
					a2 ??= new List<string>(c_a1Size * 2);
					a2.Add(s);
				}
			}
			_end = _sStart - 1; //reset, because this variable can be reused later. never mind: try/finally; it makes slightly slower.

			var r = new string[n];
			for (int i = 0, to = Math.Min(n, c_a1Size); i < to; i++) {
				r[i] = a1[i];
				a1[i] = null;
			}
			for (int i = c_a1Size; i < r.Length; i++) {
				r[i] = a2[i - c_a1Size];
			}
			return r;
		}

		const int c_a1Size = 100;
		[ThreadStatic] static string[] t_a1;
		//tested, all slower: weakreference, native memory, System.Buffers.ArrayPool<string>.Shared.

		//rejected. Not useful. Can use LINQ ToArray.
		//public StartEnd[] ToSegmentArray(int maxCount = -1)
	}
}

namespace Au.Types {
	/// <summary>
	/// Contains several string constants that can be used with some "split string" functions of this library to specify separators.
	/// </summary>
	public static class SegSep {
		/// <summary>
		/// Specifies that separators are spaces, tabs, newlines and other characters for which <see cref="char.IsWhiteSpace(char)"/> returns <c>true</c>.
		/// </summary>
		public const string Whitespace = "SSlkGrJUMUutrbSK3s6Crw";

		/// <summary>
		/// Specifies that separators are all characters for which <see cref="char.IsLetterOrDigit(char)"/> returns <c>false</c>.
		/// </summary>
		public const string Word = "WWVL0EtrK0ShqYWb4n1CmA";

		/// <summary>
		/// Specifies that separators are substrings <c>"\r\n"</c>, as well as single characters <c>'\r'</c> and <c>'\n'</c>.
		/// </summary>
		public const string Line = "LLeg5AWCNkGTZDkWuyEa2g";

		//note: all must be of length 22.
	}

	/// <summary>
	/// Flags for <see cref="ExtString.Segments"/> and some other functions.
	/// </summary>
	[Flags]
	public enum SegFlags : byte {
		/// <summary>
		/// Don't return empty substrings.
		/// For example, is string is <c>"one  two "</c> and separators is <c>" "</c>, return <c>{"one", "two"}</c> instead of <c>{"one", "", "two", ""}</c>.
		/// </summary>
		NoEmpty = 1,

		///// <summary>
		///// Remove spaces around separators. Only ASCII space characters.
		///// </summary>
		//TrimSpace = 2,
		//SHOULDDO: implement

		//rejected. Rarely used, makes slower, let use String.Split.
		///// <summary>
		///// The separators argument is string separator, not separator characters.
		///// </summary>
		//StringSeparator = ,
	}

	/// <summary>
	/// Struct with fields <b>int start</b> and <b>int end</b>.
	/// </summary>
	public record struct StartEnd {
		///
		public int start;
		///
		public int end;

		///
		public StartEnd(int start, int end) { this.start = start; this.end = end; }

		/// <summary>
		/// Returns <c>end - start</c>.
		/// </summary>
		public int Length => end - start;

		/// <summary>
		/// Converts this to <see cref="Range"/>.
		/// Can be used to get substring, like <c>s[x.Range]</c> instead of <c>s[x.start..x.end]</c>.
		/// </summary>
		public Range Range => start..end;

		//public static implicit operator Range(StartEnd s) => s.start..s.end; //could be used to get substring like s[se], but error

		/// <summary>
		/// Gets string span.
		/// </summary>
		public RStr Span(string s) => s.AsSpan(start, end - start);

		///
		public override string ToString() => $"({start}, {end})";
	}
}
