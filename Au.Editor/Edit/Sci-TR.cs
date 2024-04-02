//#define TRACE_TEMP_RANGES

using Au.Controls;
using static Au.Controls.Sci;

partial class SciCode : KScintilla {
	[Flags]
	public enum TempRangeFlags {
		/// <summary>
		/// Call onLeave etc when current position != current end of range.
		/// </summary>
		LeaveIfPosNotAtEndOfRange = 1,
		
		/// <summary>
		/// Call onLeave etc when range text modified.
		/// </summary>
		LeaveIfRangeTextModified = 2,
		
		/// <summary>
		/// Don't add new range if already exists a range with same current from, to, owner and flags. Then returns that range.
		/// </summary>
		NoDuplicate = 4,
	}
	
	public interface ITempRange {
		/// <summary>
		/// Removes this range from the collection of ranges of the document.
		/// Optional. Temp ranges are automatically removed sooner or later.
		/// Does nothing if already removed.
		/// </summary>
		void Remove();
		
		/// <summary>
		/// Gets current start and end positions of this range added with <see cref="ETempRanges_Add"/>.
		/// Returns false if the range is removed; then sets from = to = -1.
		/// </summary>
		bool GetCurrentFromTo(out int from, out int to, bool utf8 = false);
		
		/// <summary>
		/// Gets current start position of this range added with <see cref="ETempRanges_Add"/>. UTF-16.
		/// Returns -1 if the range is removed.
		/// </summary>
		int CurrentFrom { get; }
		
		/// <summary>
		/// Gets current end position of this range added with <see cref="ETempRanges_Add"/>. UTF-16.
		/// Returns -1 if the range is removed.
		/// </summary>
		int CurrentTo { get; }
		
		object Owner { get; }
		
		/// <summary>
		/// Any data. Not used by temp range functions.
		/// </summary>
		object OwnerData { get; set; }
	}
	
	class _TempRange : ITempRange {
		SciCode _doc;
		readonly object _owner;
		readonly int _fromUtf16;
		internal readonly int from;
		internal int to;
		internal readonly Action onLeave;
		readonly TempRangeFlags _flags;
		
		internal _TempRange(SciCode doc, object owner, int fromUtf16, int fromUtf8, int toUtf8, Action onLeave, TempRangeFlags flags) {
			_doc = doc;
			_owner = owner;
			_fromUtf16 = fromUtf16;
			from = fromUtf8;
			to = toUtf8;
			this.onLeave = onLeave;
			_flags = flags;
		}
		
		public void Remove() {
			_TraceTempRange("remove", _owner);
			if (_doc != null) {
				_doc._tempRanges.Remove(this);
				_doc = null;
			}
		}
		
		internal void Leaved() => _doc = null;
		
		public bool GetCurrentFromTo(out int from, out int to, bool utf8 = false) {
			if (_doc == null) { from = to = -1; return false; }
			if (utf8) {
				from = this.from;
				to = this.to;
			} else {
				from = _fromUtf16;
				to = CurrentTo;
			}
			return true;
		}
		
		public int CurrentFrom => _doc != null ? _fromUtf16 : -1;
		
		public int CurrentTo => _doc?.aaaPos16(to) ?? -1;
		
		public object Owner => _owner;
		
		public object OwnerData { get; set; }
		
		internal bool MustLeave(int pos, int pos2, int modLen) {
			return pos < from || pos2 > to
				|| (0 != (_flags & TempRangeFlags.LeaveIfPosNotAtEndOfRange) && pos2 != to)
				|| (0 != (_flags & TempRangeFlags.LeaveIfRangeTextModified) && modLen != 0);
		}
		
		internal bool Contains(int pos, object owner, bool endPosition)
			=> (endPosition ? (pos == to) : (pos >= from || pos <= to)) && (owner == null || ReferenceEquals(owner, _owner));
		
		internal bool Equals(int from2, int to2, object owner2, TempRangeFlags flags2) {
			if (from2 != from || to2 != to || flags2 != _flags
				//|| onLeave2 != onLeave //delegate always different if captured variables
				//|| !ReferenceEquals(onLeave2?.Method, onLeave2?.Method) //can be used but slow. Also tested Target, always different.
				) return false;
			return ReferenceEquals(owner2, _owner);
		}
		
		public override string ToString() => $"({CurrentFrom}, {CurrentTo}), owner={_owner}";
	}
	
	List<_TempRange> _tempRanges = new();
	
	/// <summary>
	/// Marks a temporary working range of text and later notifies when it is leaved.
	/// Will automatically update range bounds when editing text inside it.
	/// Supports many ranges, possibly overlapping.
	/// The returned object can be used to get range info or remove it.
	/// Used mostly for code info, eg to cancel the completion list or signature help.
	/// </summary>
	/// <param name="owner">Owner of the range. See also <see cref="ITempRange.OwnerData"/>.</param>
	/// <param name="from">Start of range, UTF-16.</param>
	/// <param name="to">End of range, UTF-16. Can be = from.</param>
	/// <param name="onLeave">
	/// Called when current position changed and is outside this range (before from or after to) or text modified outside it. Then also forgets the range.
	/// Called after removing the range.
	/// If leaved several ranges, called in LIFO order.
	/// Can be null.
	/// </param>
	/// <param name="flags"></param>
	public ITempRange ETempRanges_Add(object owner, int from, int to, Action onLeave = null, TempRangeFlags flags = 0) {
		int fromUtf16 = from;
		aaaNormalizeRange(true, ref from, ref to);
		//print.it(fromUtf16, from, to, aaaCurrentPos8);
#if DEBUG
		if (!(aaaCurrentPos8 >= from && (flags.Has(TempRangeFlags.LeaveIfPosNotAtEndOfRange) ? aaaCurrentPos8 == to : aaaCurrentPos8 <= to))) {
			Debug_.Print("bad");
			//CiUtil.HiliteRange(from, to);
		}
#endif
		
		if (flags.Has(TempRangeFlags.NoDuplicate)) {
			for (int i = _tempRanges.Count; --i >= 0;) {
				var t = _tempRanges[i];
				if (t.Equals(from, to, owner, flags)) return t;
			}
		}
		
		_TraceTempRange("ADD", owner);
		var r = new _TempRange(this, owner, fromUtf16, from, to, onLeave, flags);
		_tempRanges.Add(r);
		return r;
	}
	
	/// <summary>
	/// Gets ranges containing the specified position and optionally of the specified owner, in LIFO order.
	/// It's safe to remove the retrieved ranges while enumerating.
	/// </summary>
	/// <param name="position"></param>
	/// <param name="owner">If not null, returns only ranges where ReferenceEquals(owner, range.owner).</param>
	/// <param name="endPosition">position must be at the end of the range.</param>
	/// <param name="utf8"></param>
	public IEnumerable<ITempRange> ETempRanges_Enum(int position, object owner = null, bool endPosition = false, bool utf8 = false) {
		if (!utf8) position = aaaPos8(position);
		for (int i = _tempRanges.Count; --i >= 0;) {
			var r = _tempRanges[i];
			if (r.Contains(position, owner, endPosition)) yield return r;
		}
	}
	
	/// <summary>
	/// Gets ranges of the specified owner, in LIFO order.
	/// It's safe to remove the retrieved ranges while enumerating.
	/// </summary>
	/// <param name="owner">Returns only ranges where ReferenceEquals(owner, range.owner).</param>
	public IEnumerable<ITempRange> ETempRanges_Enum(object owner) {
		for (int i = _tempRanges.Count; --i >= 0;) {
			var r = _tempRanges[i];
			if (ReferenceEquals(owner, r.Owner)) yield return r;
		}
	}
	
	internal void ETempRanges_HidingOrClosingActiveDoc_() {
		if (_tempRanges.Count == 0) return;
		//_TraceTempRange("CLEAR", null);
		for (int i = _tempRanges.Count; --i >= 0;) {
			var r = _tempRanges[i];
			_TraceTempRange("leave", r.Owner);
			r.Leaved();
			r.onLeave?.Invoke();
		}
		_tempRanges.Clear();
	}
	
	void _TempRangeOnModifiedOrPosChanged(MOD mod, int pos, int len) {
		if (_tempRanges.Count == 0) return;
		if (mod == 0) pos = aaaCurrentPos8;
		int pos2 = pos;
		if (mod.Has(MOD.SC_MOD_DELETETEXT)) { pos2 += len; len = -len; }
		List<Action> aOL = null;
		for (int i = _tempRanges.Count; --i >= 0;) {
			var r = _tempRanges[i];
			if (r.MustLeave(pos, pos2, len)) {
				_TraceTempRange("leave", r.Owner);
				_tempRanges.RemoveAt(i);
				r.Leaved();
				if (r.onLeave is { } ol) (aOL ??= new()).Add(ol);
			} else {
				r.to += len;
				Debug.Assert(r.to >= r.from);
			}
		}
		if (aOL != null) foreach (var ol in aOL) ol();
	}
	
	[Conditional("TRACE_TEMP_RANGES")]
	static void _TraceTempRange(string action, object owner) => print.it(action, owner);
}
