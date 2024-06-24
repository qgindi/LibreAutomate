
using System.Text.RegularExpressions; //for XML doc links

namespace Au.Types {
	/// <summary>
	/// Regular expression match info.
	/// Used with <see cref="regexp"/> class functions and <b>String</b> extension methods like <see cref="ExtString.RxMatch"/>.
	/// </summary>
	/// <remarks>
	/// Contains info about a regular expression match found in the subject string: index, length, substring, etc.
	/// Also contains an array of group matches, as <see cref="RXGroup"/>. Groups are regular expression parts enclosed in <c>()</c>, except <c>(?...)</c>.
	/// Group matches can be accessed like array elements. Group 0 is whole match. Group 1 is the first group. See examples.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var s = "ab cd-45-ef gh";
	/// if(s.RxMatch(@"\b([a-z]+)-(\d+)\b", out RXMatch m))
	/// 	print.it(
	/// 		m.GroupCountPlusOne, //3 (whole match and 2 groups)
	/// 		m.Start, //3, same as m[0].Index
	/// 		m.Value, //"cd-45-ef", same as m[0].Value
	/// 		m[1].Start, //3
	/// 		m[1].Value, //"cd"
	/// 		m[2].Start, //6
	/// 		m[2].Value //"45"
	/// 		);
	/// ]]></code>
	/// A group in the subject string may not exist even if whole match found. Then its <b>Exists</b> property is <c>false</c>, <b>Index</b> -1, <b>Length</b> 0, <b>Value</b> <c>null</c>.
	/// <code><![CDATA[
	/// var s = "ab cd--ef gh";
	/// if(s.RxMatch(@"\b([a-z]+)-(\d+)?-([a-z]+)\b", out RXMatch m))
	/// 	print.it(
	/// 		m.GroupCountPlusOne, //4 (whole match and 3 groups)
	/// 		m[2].Exists, //false
	/// 		m[2].Start, //-1
	/// 		m[2].Length, //0
	/// 		m[2].Value //null
	/// 		);
	/// ]]></code>
	/// </example>
	public unsafe class RXMatch {
		internal RXMatch(regexp rx, string subject, int rc, in Cpp.RegexMatch k) {
			Mark = k.Mark;
			if (rc < 0) return;
			Exists = true;
			IsPartial = rc == 0;
			StartNoK = k.indexNoK;
			_rx = rx;
			//_subject = subject;
			
			var g = _groups = new RXGroup[k.vecCount];
			var v = k.vec;
			for (int i = 0; i < g.Length; i++) {
				g[i] = new RXGroup(subject, v[i]);
			}
		}
		
		//string readonly _subject;
		readonly regexp _rx;
		readonly RXGroup[] _groups;
		
		/// <summary>
		/// Gets the subject string in which this match was found.
		/// </summary>
		public string Subject => _groups[0].Subject_;
		
		/// <summary>
		/// Gets the number of groups in the regular expression, + 1 for the whole match.
		/// </summary>
		public int GroupCountPlusOne => _groups.Length;
		
		/// <summary>
		/// Gets start offset of the match in the subject string. The same as that of group 0 (<see cref="RXGroup.Start"/>).
		/// </summary>
		public int Start => _groups[0].Start;
		
		/// <summary>
		/// Gets length of the match in the subject string. The same as that of group 0 (<see cref="RXGroup.Length"/>).
		/// </summary>
		public int Length => _groups[0].Length;
		
		/// <summary>
		/// Gets end offset of the match in the subject string (<see cref="Start"/> + <see cref="Length"/>). The same as that of group 0 (<see cref="RXGroup.End"/>).
		/// </summary>
		public int End => _groups[0].End;
		
		/// <summary>
		/// Gets substring of the subject string from <see cref="Start"/> to <see cref="End"/>. The same as that of group 0 (<see cref="RXGroup.Value"/>).
		/// </summary>
		public string Value => _groups[0].Value;
		
		/// <summary>
		/// Gets span of the subject string from <see cref="Start"/> to <see cref="End"/>. The same as that of group 0 (<see cref="RXGroup.Span"/>).
		/// </summary>
		/// <remarks>
		/// Unlike <see cref="Value"/>, does not create new string.
		/// </remarks>
		public RStr Span => _groups[0].Span;
		
		/// <summary>
		/// Returns <see cref="RXGroup.ToString"/> of group 0.
		/// </summary>
		public override string ToString() => _groups[0].ToString();
		
		/// <summary>
		/// Gets substring of the subject string from <see cref="Start"/> to <see cref="End"/>. The same as that of group 0 (<see cref="RXGroup.GetValue_"/>).
		/// </summary>
		/// <remarks>
		/// Use this function instead of <see cref="Value"/> with results of <b>regexp</b> functions where subject is <b>ReadOnlySpan</b>.
		/// </remarks>
		/// <param name="subject">Must be the same subject string as passed to the <b>regexp</b> function that returned this result.</param>
		internal string GetValue_(RStr subject) => _groups[0].GetValue_(subject);
		
		/// <summary>
		/// Gets span of the subject string from <see cref="Start"/> to <see cref="End"/>. The same as that of group 0 (<see cref="RXGroup.GetSpan_"/>).
		/// </summary>
		/// <remarks>
		/// Use this function instead of <see cref="Span"/> with results of <b>regexp</b> functions where subject is <b>ReadOnlySpan</b>.
		/// </remarks>
		/// <param name="subject">Must be the same subject string as passed to the <b>regexp</b> function that returned this result.</param>
		internal RStr GetSpan_(RStr subject) => _groups[0].GetSpan_(subject);
		
		/// <summary>
		/// Gets start offset of whole match regardless of <c>\K</c>.
		/// When the regular expression contains <c>\K</c>, this is less than <see cref="Start"/>.
		/// </summary>
		public int StartNoK { get; private set; }
		
		/// <summary>
		/// Gets the name of a found mark, or <c>null</c>.
		/// </summary>
		/// <remarks>
		/// Marks can be inserted in regular expression pattern like <c>(*MARK:name)</c> or <c>(*:name)</c>.
		/// After a full successful match, it is the last mark encountered on the matching path through the pattern. After a "no match" or a partial match, it is the last encountered mark. For example, consider this pattern: <c>"^(*MARK:A)((*MARK:B)a|b)c"</c>. When it matches <c>"bc"</c>, the mark is <c>A</c>. The <c>B</c> mark is "seen" in the first branch of the group, but it is not on the matching path. On the other hand, when this pattern fails to match <c>"bx"</c>, the mark is <c>B</c>.
		/// </remarks>
		public string Mark { get; private set; }
		
		/// <summary>
		/// Gets the return value of the <see cref="regexp.Match(string, out RXMatch, Range?, RXMatchFlags)"/> call.
		/// </summary>
		/// <remarks>
		/// Can be <c>false</c> only when the function returned <c>false</c> but a mark is available (see <see cref="Mark"/>). Otherwise, when the function returns <c>false</c>, it returns <c>null</c> instead of a <b>RXMatch</b> object.
		/// When <c>false</c>, all properties except <b>Exists</b> and <b>Mark</b> have undefined values or throw exception.
		/// </remarks>
		public bool Exists { get; private set; }
		
		/// <summary>
		/// Returns <c>true</c> if this match is partial.
		/// Partial match is possible if used a <b>PARTIAL_</b> flag.
		/// </summary>
		public bool IsPartial { get; private set; }
		
		/// <summary>
		/// Gets group info. Index 0 is whole match. Index 1 is the first group.
		/// </summary>
		/// <param name="group">1-based group index, or 0 for whole match.</param>
		/// <exception cref="IndexOutOfRangeException">Invalid <i>group</i>. Max valid value is <see cref="GroupCountPlusOne"/>.</exception>
		public ref RXGroup this[int group] => ref _groups[group];
		
		/// <summary>
		/// Gets group info of a named group.
		/// </summary>
		/// <param name="groupName">
		/// Group name.
		/// In regular expression, to set name of group <c>(text)</c>, use <c>(?&lt;NAME&gt;text)</c>.
		/// </param>
		/// <exception cref="ArgumentException">Unknown group name.</exception>
		/// <remarks>
		/// If multiple groups have this name, prefers the first group that matched (<see cref="RXGroup.Exists"/> is <c>true</c>).
		/// </remarks>
		public ref RXGroup this[string groupName] {
			get {
				int i = GroupNumberFromName(groupName);
				if (i < 0) throw new ArgumentException("Unknown group name.");
				return ref _groups[i];
			}
		}
		
		/// <summary>
		/// Finds a named group and returns its 1-based index. Returns -1 if not found.
		/// </summary>
		/// <param name="groupName">
		/// Group name.
		/// In regular expression, to set name of group <c>(text)</c>, use <c>(?&lt;NAME&gt;text)</c>.
		/// </param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <remarks>
		/// If multiple groups have this name, prefers the first group that matched (<see cref="RXGroup.Exists"/> is <c>true</c>).
		/// </remarks>
		/// <seealso cref="regexp.GetGroupNumberOf"/>
		public int GroupNumberFromName(string groupName) {
			Not_.Null(groupName);
			fixed (char* p = groupName) return GroupNumberFromName_(p, groupName.Length, out _);
		}
		
		/// <summary>
		/// Finds a named group and returns its 1-based index. Returns -1 if not found.
		/// </summary>
		/// <param name="groupName">
		/// Group name.
		/// In regular expression, to set name of group <c>(text)</c>, use <c>(?&lt;NAME&gt;text)</c>.
		/// </param>
		/// <param name="notUnique">Receives <c>true</c> if multiple groups have this name.</param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <remarks>
		/// If multiple groups have this name, prefers the first group that matched (<see cref="RXGroup.Exists"/> is <c>true</c>).
		/// </remarks>
		/// <seealso cref="regexp.GetGroupNumberOf"/>
		public int GroupNumberFromName(string groupName, out bool notUnique) {
			Not_.Null(groupName);
			fixed (char* p = groupName) return GroupNumberFromName_(p, groupName.Length, out notUnique);
		}
		
		//Used by regexp.ReplaceAll to avoid repl.Substring.
		internal int GroupNumberFromName_(char* s, int len, out bool notUnique) {
			notUnique = false;
			if (len > 32 || len < 1) return -1;
			int step; ushort* first, last;
			if (s[len] == '\0') {
				step = Cpp.pcre2_substring_nametable_scan(_rx._CodeHR, s, &first, &last);
			} else {
				var p = stackalloc char[33];
				int i; for (i = 0; i < len; i++) p[i] = s[i]; p[i] = '\0';
				step = Cpp.pcre2_substring_nametable_scan(_rx._CodeHR, p, &first, &last);
			}
			if (step <= 0) return -1;
			int R = 0;
			notUnique = last > first;
			for (; first <= last; first += step) {
				int r = *first;
				if (_groups[r].Start >= 0) return r; //return the first that is set
				if (R == 0) R = r; //if none is set, return the first
			}
			return R;
		}
		
		/// <summary>
		/// Returns expanded version of the specified replacement pattern.
		/// </summary>
		/// <param name="repl">
		/// Replacement pattern.
		/// Can consist of any combination of literal text and substitutions like <c>$1</c>.
		/// Supports .NET regular expression substitution syntax. See <see cref="Regex.Replace(string, string, int)"/>. Also: replaces <c>$*</c> with the name of the last encountered mark; replaces <c>${+func}</c> and <c>${+func(n)}</c> with the return value of a function registered with <see cref="regexp.addReplaceFunc"/>.
		/// </param>
		/// <exception cref="ArgumentException">
		/// - Invalid <c>$replacement</c>.
		/// - Used a <b>PARTIAL_</b> flag.
		/// - The regular expression contains <c>(?=...\K)</c>.
		/// </exception>
		/// <remarks>
		/// Works like <see cref="Match.Result"/>.
		/// See also: <see cref="regexp.Replace(string, string, int, Range?, RXMatchFlags)"/>.
		/// </remarks>
		public string ExpandReplacement(string repl) {
			if (repl.NE()) return repl;
			using (new StringBuilder_(out var b)) {
				regexp.ExpandReplacement_(this, repl, b);
				return b.ToString();
			}
		}
	}
	
	/// <summary>
	/// Regular expression group match info.
	/// Used with <see cref="RXMatch"/>, <see cref="regexp"/> and some <b>String</b> extension methods.
	/// </summary>
	/// <remarks>
	/// Groups are regular expression parts enclosed in <c>()</c>. Except non-capturing parts, like <c>(?:...)</c> and <c>(?options)</c>. A <b>RXGroup</b> variable contains info about a group found in the subject string: index, length, substring.
	/// 
	/// Some groups specified in regular expression may not exist in the subject string even if it matches the regular expression. For example, regular expression <c>"A(\d+)?B"</c> matches string <c>"AB"</c>, but group <c>(\d+)</c> does not exist. Then <see cref="Exists"/> is <c>false</c>, <see cref="Start"/> -1, <see cref="Length"/> 0, <see cref="Value"/> <c>null</c>.
	/// 
	/// When a group matches multiple times, the <b>RXGroup</b> variable contains only the last instance. For example, if subject is <c>"begin 12 345 67 end"</c> and regular expression is <c>(\d+ )+</c>, value of group 1 is <c>"67"</c>. If you need all instances (<c>"12"</c>, <c>"345"</c>, <c>"67"</c>), instead use .NET <see cref="Regex"/> and <see cref="Group.Captures"/>. Also you can get all instances with <see cref="regexp.Callout"/>.
	/// 
	/// Examples and more info: <see cref="RXMatch"/>, <see cref="regexp"/>.
	/// </remarks>
	public struct RXGroup {
		readonly string _subject;
		readonly int _index; //offset in _subject, or -1 if this group does not exist
		readonly int _len; //length, or 0 if this group match does not exist
		
		internal RXGroup(string subject, int start, int end) {
			_subject = subject;
			_index = start;
			_len = end - start; //note: can be <0 if (?=...\K). It's OK.
		}
		
		internal RXGroup(string subject, StartEnd r) {
			_subject = subject;
			_index = r.start;
			_len = r.Length; //note: can be <0 if (?=...\K). It's OK.
		}
		
		internal string Subject_ => _subject;
		
		/// <summary>
		/// Gets start offset of the group match in the subject string.
		/// </summary>
		public int Start => _index;
		
		/// <summary>
		/// Gets length of the group match in the subject string.
		/// </summary>
		public int Length => _len;
		
		/// <summary>
		/// Gets end offset of the group match in the subject string (<see cref="Start"/> + <see cref="Length"/>).
		/// </summary>
		public int End => _index + _len;
		
		/// <summary>
		/// Returns <c>true</c> if the group exists in the subject string, <c>false</c> if does not exist.
		/// More info in <see cref="RXGroup"/> topic. Example in <see cref="RXMatch"/> topic.
		/// </summary>
		/// <remarks>
		/// Other ways to detect it: if a group does not exist, its <b>Index</b> is -1 and <b>Value</b> is <c>null</c>.
		/// </remarks>
		public bool Exists => _index >= 0;
		
		/// <summary>
		/// Gets span of the subject string from <see cref="Start"/> to <see cref="End"/>.
		/// </summary>
		/// <returns><c>default</c> if the group does not exist in the subject string (see <see cref="Exists"/>).</returns>
		/// <remarks>
		/// Unlike <see cref="Value"/>, does not create new string.
		/// </remarks>
		public RStr Span => _len > 0 ? _subject.AsSpan(_index, _len) : (_index < 0 ? default : ""); //_len can be < 0
		
		/// <summary>
		/// Gets substring of the subject string from <see cref="Start"/> to <see cref="End"/>.
		/// </summary>
		/// <returns><c>null</c> if the group does not exist in the subject string (see <see cref="Exists"/>).</returns>
		/// <remarks>
		/// Creates new string each time. See also <see cref="Span"/>.
		/// </remarks>
		public string Value => _len > 0 ? _subject[_index..End] : (_index < 0 ? null : ""); //_len can be < 0
		
		/// <summary>
		/// Returns <see cref="Value"/>.
		/// </summary>
		public override string ToString() => Value;
		//public override string ToString() => _subject != null ? Value : $"{_index}..{End}";
		
		/// <summary>
		/// Gets substring of the subject string from <see cref="Start"/> to <see cref="End"/>.
		/// Returns <c>null</c> if the group does not exist in the subject string (see <see cref="Exists"/>).
		/// </summary>
		/// <remarks>
		/// Use this function instead of <see cref="Value"/> with results of <b>regexp</b> functions where subject is <b>ReadOnlySpan</b>.
		/// </remarks>
		/// <param name="subject">Must be the same subject string as passed to the <b>regexp</b> function that returned this result.</param>
		internal string GetValue_(RStr subject) => _len > 0 ? subject[_index..End].ToString() : (_index < 0 ? null : "");
		
		/// <summary>
		/// Gets span of the subject string from <see cref="Start"/> to <see cref="End"/>.
		/// Returns <c>null</c> if the group does not exist in the subject string (see <see cref="Exists"/>).
		/// </summary>
		/// <remarks>
		/// Use this function instead of <see cref="Span"/> with results of <b>regexp</b> functions where subject is <b>ReadOnlySpan</b>.
		/// </remarks>
		/// <param name="subject">Must be the same subject string as passed to the <b>regexp</b> function that returned this result.</param>
		internal RStr GetSpan_(RStr subject) => _len > 0 ? subject.Slice(_index, _len) : (_index < 0 ? default : "");
		
		///
		public static implicit operator Range(RXGroup g) => g.Exists ? g.Start..g.End : default;
	}
	
	
	#region callout
	
	/// <summary>
	/// Managed version of PCRE API struct <b>pcre2_callout_block</b>.
	/// When you set <see cref="regexp.Callout"/>, your callout function's parameter is of this type.
	/// More info in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2callout.html">pcre2callout</see>.
	/// Most properties are <b>pcre2_callout_block</b> fields as documented in PCRE help. Other properties and methods are easier/safer versions of unsafe fields like <b>offset_vector</b>.
	/// </summary>
	public unsafe struct RXCalloutData {
#pragma warning disable 649 //field never assigned
		struct pcre2_callout_block {
			public int version;
			public readonly int callout_number, capture_top, capture_last;
			public readonly nint* vec;
			public readonly char* mark, subject;
			public readonly nint subject_length;
			public readonly nint start_match;
			public readonly nint current_position;
			public readonly nint pattern_position;
			public readonly nint next_item_length;
			public readonly nint callout_string_offset;
			public readonly nint callout_string_length;
			public readonly char* callout_string;
			public readonly int callout_flags;
		}
#pragma warning restore 649
		
		//We use pointer instead of adding pcre2_callout_block fields to this struct. Other ways are not good:
		//	Passing whole block to the final callback by value is slow (104 bytes, tested speed). Also then cannot have Result like now.
		//	With 'in' fast, but then users have to declare lambda parameters like 'in RXCalloutData d'. Now just 'd'.
		pcre2_callout_block* _p;
		
		/// <summary>
		/// Sets the return value of the callout function, as documented in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2callout.html">pcre2callout</see>.
		/// Default 0.
		/// If 1, matching fails at the current point, but the testing of other matching possibilities goes ahead, just as if a lookahead assertion had failed.
		/// If -1 (<b>PCRE2_ERROR_NOMATCH</b>), the match function returns <c>false</c> (no match). Values less tan -2 are PCRE error codes and cause exception.
		/// </summary>
		public int Result { set => _p->version = value; internal get => _p->version; }
		
		internal RXCalloutData(void* calloutBlock) {
			_p = (pcre2_callout_block*)calloutBlock;
			Result = 0;
		}
		
		/// <summary>
		/// Callout number, eg 5 for <c>"(?C5)"</c>.
		/// More info in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2callout.html">pcre2callout</see>.
		/// </summary>
		public int callout_number => _p->callout_number;
		
		/// <summary>
		/// One more than the number of the highest numbered captured group so far.
		/// More info in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2callout.html">pcre2callout</see>.
		/// </summary>
		public int capture_top => _p->capture_top;
		
		/// <summary>
		/// The number of the most recently captured group.
		/// More info in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2callout.html">pcre2callout</see>.
		/// </summary>
		public int capture_last => _p->capture_last;
		
		/// <summary>
		/// Flags.
		/// 1 <b>PCRE2_CALLOUT_STARTMATCH</b>, 2 <b>PCRE2_CALLOUT_BACKTRACK</b>.
		/// More info in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2callout.html">pcre2callout</see>.
		/// </summary>
		public int callout_flags => _p->callout_flags;
		
		/// <summary>
		/// The offset within the subject string at which the current match attempt started. But depends on <c>\K</c> etc.
		/// More info in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2callout.html">pcre2callout</see>.
		/// </summary>
		public int start_match => (int)_p->start_match;
		
		/// <summary>
		/// The current offset within the subject string.
		/// </summary>
		public int current_position => (int)_p->current_position;
		
		/// <summary>
		/// The offset in the regular expression to the next item to be matched.
		/// </summary>
		public int pattern_position => (int)_p->pattern_position;
		
		/// <summary>
		/// The length of the next item to be processed in the regular expression.
		/// More info in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2callout.html">pcre2callout</see>.
		/// </summary>
		public int next_item_length => (int)_p->next_item_length;
		
		/// <summary>
		/// The callout string offset in the regular expression. Used with callouts like <c>"(?C'calloutString')"</c>.
		/// More info in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2callout.html">pcre2callout</see>.
		/// </summary>
		public int callout_string_offset => (int)_p->callout_string_offset;
		
		/// <summary>
		/// The callout string, eg <c>"xyz"</c> for <c>"(?C'xyz')"</c>.
		/// More info in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2callout.html">pcre2callout</see>.
		/// </summary>
		public string callout_string => _p->callout_string == null ? null : new string(_p->callout_string, 0, (int)_p->callout_string_length);
		
		/// <summary>
		/// The most recently passed <c>(*MARK)</c>, <c>(*PRUNE)</c>, or <c>(*THEN)</c> item in the match, or <c>null</c> if no such items have been passed.
		/// More info in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2callout.html">pcre2callout</see>.
		/// </summary>
		public string mark => _p->mark == null ? null : new string(_p->mark);
		
		/// <summary>
		/// Gets the start index and length of the specified group in the subject string.
		/// </summary>
		/// <param name="group">Group number (1-based index).</param>
		/// <exception cref="ArgumentOutOfRangeException"><i>group</i> must be &gt; 0 and &lt; <b>capture_top</b>.</exception>
		public (int index, int length) Group(int group) {
			if (group <= 0 || group >= _p->capture_top) throw new ArgumentOutOfRangeException(nameof(group), "Must be > 0 and < capture_top.");
			var v = _p->vec;
			int i = (int)v[group *= 2];
			return (i, (int)v[group + 1] - i);
		}
		
		/// <summary>
		/// Gets the value (substring) of the specified group.
		/// </summary>
		/// <param name="group">Group number (1-based index).</param>
		/// <exception cref="ArgumentOutOfRangeException"><i>group</i> must be &gt; 0 and &lt; <b>capture_top</b>.</exception>
		public string GroupValue(int group) {
			var (i, len) = Group(group);
			if (i < 0) return null;
			if (len == 0) return "";
			return new string(_p->subject, i, len);
		}
		
		/// <summary>
		/// Gets the start index and length of the most recently captured group in the subject string.
		/// </summary>
		public (int index, int length) LastGroup => Group(_p->capture_last);
		
		/// <summary>
		/// Gets the value (substring) of the most recently captured group.
		/// </summary>
		public string LastGroupValue => GroupValue(_p->capture_last);
	}
	
	#endregion
	
#pragma warning disable 1591 //no XML doc
	/// <summary>
	/// Flags for <see cref="regexp"/> constructor.
	/// Documented in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2api.html">pcre2api</see>.
	/// </summary>
	/// <remarks>
	/// Many options also can be specified in regular expression (RE):
	/// - These can be anywhere in RE: <c>(?i)</c> <b>CASELESS</b>, <c>(?m)</c> <b>MULTILINE</b>, <c>(?s)</c> <b>DOTALL</b>, <c>(?n)</c> <b>NO_AUTO_CAPTURE</b>, <c>(?x)</c> <b>EXTENDED</b>, <c>(?xx)</c> <b>EXTENDED_MORE</b>, <c>(?J)</c> <b>DUPNAMES</b>, <c>(?U)</c> <b>UNGREEDY</b>. Can be multiple, like <c>(?ms)</c>. Can be unset, like <c>(?-i)</c>. RE <c>"\Qtext\E"</c> is like RE <c>"text"</c> with flag <b>LITERAL</b>.
	/// - Instead of <b>ANCHORED</b> can be used <c>\G</c> at the start of RE. Or <c>^</c>, except in multiline mode.
	/// - Instead of <b>ENDANCHORED</b> can be used <c>\z</c> at the end of RE. Or <c>$</c>, except in multiline mode.
	/// - Flag UTF is implicitly added if RE contains non-ASCII characters and there is no flag <b>NEVER_UTF</b>.
	/// - These must be at the very start and are named like flags: <c>(*UTF)</c>, <c>(*UCP)</c>, <c>(*NOTEMPTY)</c>, <c>(*NOTEMPTY_ATSTART)</c>, <c>(*NO_AUTO_POSSESS)</c>, <c>(*NO_DOTSTAR_ANCHOR)</c>, <c>(*NO_START_OPT)</c>.
	/// - More info in <see href="https://www.pcre.org/current/doc/html/pcre2pattern.html">PCRE syntax reference</see>.
	/// 
	/// Some of <b>RXFlags</b> flags also exist in <see cref="RXMatchFlags"/>. You can set them either when calling <b>regexp</b> constructor or when calling <b>regexp</b> functions that have parameter <i>more</i>. You can use different flags for each function call with the same <b>regexp</b> variable.
	/// </remarks>
	[Flags]
	public enum RXFlags : ulong {
		ANCHORED = 0x80000000,
		ENDANCHORED = 0x20000000,
		NO_UTF_CHECK = 0x40000000,
		
		ALLOW_EMPTY_CLASS = 0x00000001,
		ALT_BSUX = 0x00000002,
		AUTO_CALLOUT = 0x00000004,
		CASELESS = 0x00000008,
		DOLLAR_ENDONLY = 0x00000010,
		DOTALL = 0x00000020,
		DUPNAMES = 0x00000040,
		EXTENDED = 0x00000080,
		FIRSTLINE = 0x00000100,
		MATCH_UNSET_BACKREF = 0x00000200,
		MULTILINE = 0x00000400,
		NEVER_UCP = 0x00000800,
		NEVER_UTF = 0x00001000,
		NO_AUTO_CAPTURE = 0x00002000,
		NO_AUTO_POSSESS = 0x00004000,
		NO_DOTSTAR_ANCHOR = 0x00008000,
		NO_START_OPTIMIZE = 0x00010000,
		UCP = 0x00020000,
		UNGREEDY = 0x00040000,
		
		/// <summary>
		/// Fully support Unicode text (case-insensitivity etc). More info in PCRE documentation topic <see href="https://www.pcre.org/current/doc/html/pcre2unicode.html">pcre2unicode</see>.
		/// This flag is implicitly added if regular expression contains non-ASCII characters and there is no flag <b>NEVER_UTF</b>.
		/// </summary>
		UTF = 0x00080000,
		
		NEVER_BACKSLASH_C = 0x00100000,
		ALT_CIRCUMFLEX = 0x00200000,
		ALT_VERBNAMES = 0x00400000,
		//USE_OFFSET_LIMIT = 0x00800000, //used with pcre2_set_offset_limit(), but currently we don't support it
		EXTENDED_MORE = 0x01000000,
		LITERAL = 0x02000000,
		
		//PCRE2_EXTRA_ flags.
		
		//ALLOW_SURROGATE_ESCAPES = 0x1L << 32, //not used with UTF-16
		//BAD_ESCAPE_IS_LITERAL = 0x2L << 32, //dangerous
		MATCH_WORD = 0x4L << 32,
		MATCH_LINE = 0x8L << 32,
		
		//Match API flags. regexp ctor moves them to a field that later is combined with RXMatchFlags when calling the match API.
		
		NOTBOL = 0x00000001L << 56, //hi byte of long
		NOTEOL = 0x00000002L << 56,
		NOTEMPTY = 0x00000004L << 56,
		NOTEMPTY_ATSTART = 0x00000008L << 56,
		PARTIAL_SOFT = 0x00000010L << 56,
		PARTIAL_HARD = 0x00000020L << 56,
	}
	
	/// <summary>
	/// Flags for <see cref="regexp"/> class functions.
	/// Documented in PCRE help topic <see href="https://www.pcre.org/current/doc/html/pcre2api.html">pcre2api</see>.
	/// </summary>
	/// <remarks>
	/// These flags also exist in <see cref="RXFlags"/> (<b>regexp</b> constructor flags). You can set them either when calling constructor or when calling other functions.
	/// </remarks>
	[Flags]
	public enum RXMatchFlags : uint {
		//These are the same as in RXFlags, and can be used either when compiling or when matching.
		
		ANCHORED = 0x80000000,
		ENDANCHORED = 0x20000000,
		NO_UTF_CHECK = 0x40000000,
		
		//These are only for matching. Also added to the hi int of RXFlags.
		
		NOTBOL = 0x00000001,
		NOTEOL = 0x00000002,
		NOTEMPTY = 0x00000004,
		NOTEMPTY_ATSTART = 0x00000008,
		PARTIAL_SOFT = 0x00000010,
		PARTIAL_HARD = 0x00000020,
	}
#pragma warning restore 1591
	
	internal static unsafe partial class Cpp {
		/// <summary>This and related API are documented in the C++ dll project.</summary>
		[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
		internal static extern nint Cpp_RegexCompile(string rx, nint len, RXFlags flags, out int codeSize, out BSTR errStr);
		
		[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int Cpp_RegexDtor(IntPtr code);
		
		/// <summary>This and related API are documented in the C++ dll project.</summary>
		internal struct RegexMatch {
			public StartEnd* vec;
			public int vecCount;
			public int indexNoK;
			public char* mark;
			
			public string Mark => mark == null ? null : new(mark, 0, mark[-1]);
		}
		
		internal unsafe delegate int PcreCalloutT(void* calloutBlock, void* param);
		
		/// <summary>This and related API are documented in the C++ dll project.</summary>
		[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int Cpp_RegexMatch(HandleRef code, char* s, nint len, nint start, RXMatchFlags flags,
			PcreCalloutT callout, out RegexMatch m, bool needM, out BSTR errStr);
		//note: don't use [MarshalAs(UnmanagedType.BStr)] out string errStr, it makes much slower.
		
		//rejected
		//[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
		//internal static extern int Cpp_RegexSubstitute(HandleRef code, string s, nint len, nint start, PCRE2_SUBSTITUTE_ flags,
		//	string repl, nint rlen, [Out] char[] outputbuffer, ref nint outlen, out BSTR errStr);
		
		#region PCRE API
		
		//internal enum PCRE2_ERROR_
		//{
		//	PARTIAL = 0, //note: the PCRE API value is -2, but our C++ dll API then returns 0
		//	NOMATCH = -1,
		//	CALLOUT = -37,
		//	NOMEMORY = -48,
		//	NOUNIQUESUBSTRING = -50,
		//	//others not useful
		//}
		
		[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int pcre2_pattern_info(HandleRef code, PCRE2_INFO_ what, void* where);
		
		[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int pcre2_substring_nametable_scan(HandleRef code, char* name, ushort** first, ushort** last);
		
		internal enum PCRE2_SUBSTITUTE_ {
			EXTENDED = 0x00000200,
			GLOBAL = 0x00000100,
			OVERFLOW_LENGTH = 0x00001000,
			UNKNOWN_UNSET = 0x00000800,
			UNSET_EMPTY = 0x00000400,
		}
		
		internal enum PCRE2_INFO_ {
			ALLOPTIONS = 0,
			//ARGOPTIONS = 1,
			//BACKREFMAX = 2,
			//BSR = 3,
			CAPTURECOUNT = 4,
			//FIRSTCODEUNIT = 5,
			//FIRSTCODETYPE = 6,
			//FIRSTBITMAP = 7,
			//HASCRORLF = 8,
			//JCHANGED = 9,
			//JITSIZE = 10,
			//LASTCODEUNIT = 11,
			//LASTCODETYPE = 12,
			//MATCHEMPTY = 13,
			//MATCHLIMIT = 14,
			//MAXLOOKBEHIND = 15,
			//MINLENGTH = 16,
			//NAMECOUNT = 17,
			//NAMEENTRYSIZE = 18,
			//NAMETABLE = 19,
			//NEWLINE = 20,
			//DEPTHLIMIT = 21,
			//SIZE = 22,
			//HASBACKSLASHC = 23,
			//FRAMESIZE = 24,
			//HEAPLIMIT = 25,
		}
		
		#endregion
	}
}
