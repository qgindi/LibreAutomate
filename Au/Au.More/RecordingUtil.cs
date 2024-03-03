namespace Au.More
{
	/// <summary>
	/// Functions for keyboard/mouse/etc recorder tools.
	/// </summary>
	public static partial class RecordingUtil
	{
		/// <summary>
		/// Converts multiple recorded mouse movements to string for <see cref="mouse.moveBy(string, double)"/>.
		/// </summary>
		/// <param name="recorded">
		/// List of x y distances from previous.
		/// The first distance is from mouse position before the first movement; at run time it will be distance from <see cref="mouse.lastXY"/>.
		/// To create <b>uint</b> value from distance <i>dx</i> <i>dy</i> use <see cref="Math2.MakeLparam"/> and cast to <b>uint</b>.
		/// </param>
		/// <param name="withSleepTimes">
		/// <i>recorded</i> also contains sleep times (milliseconds) alternating with distances.
		/// It must start with a sleep time. Example: {time1, dist1, time2, dist2}. Another example: {time1, dist1, time2, dist2, time3}. This is invalid: {dist1, time1, dist2, time2}.
		/// </param>
		public static string MouseToString(IEnumerable<uint> recorded, bool withSleepTimes) {
			var a = new List<byte>();

			byte flags = 0;
			if (withSleepTimes) flags |= 1;
			a.Add(flags);

			int pdx = 0, pdy = 0;
			bool isSleep = withSleepTimes;
			foreach (var u in recorded) {
				int v, nbytes = 4;
				if (isSleep) {
					v = (int)Math.Min(u, 0x3fffffff);
					if (v > 3) v--; //_SendMove usually takes 0.5-1.5 ms
					if (v <= 1 << 6) nbytes = 1;
					else if (v <= 1 << 14) nbytes = 2;
					else if (v <= 1 << 22) nbytes = 3;

					//print.it($"nbytes={nbytes}    sleep={v}");
					//never mind: ~90% is 7. Removing it would make almost 2 times smaller string. But need much more code. Or compress (see comment below).
				} else {
					//info: to make more compact, we write not distances (dx dy) but distance changes (x y).
					int dx = Math2.LoShort((nint)u), x = dx - pdx; pdx = dx;
					int dy = Math2.HiShort((nint)u), y = dy - pdy; pdy = dy;

					if (x >= -4 && x < 4 && y >= -4 && y < 4) nbytes = 1; //3+3+2=8 bits, 90%
					else if (x >= -64 && x < 64 && y >= -64 && y < 64) nbytes = 2; //7+7+2=16 bits, ~10%
					else if (x >= -1024 && x < 1024 && y >= -1024 && y < 1024) nbytes = 3; //11+11+2=24 bits, ~0%

					int shift = nbytes * 4 - 1, mask = (1 << shift) - 1;
					v = (x & mask) | ((y & mask) << shift);

					//print.it($"dx={dx} dy={dy}    x={x} y={y}    nbytes={nbytes}    v=0x{v:X}");
				}
				v <<= 2; v |= (nbytes - 1);
				for (; nbytes != 0; nbytes--, v >>= 8) a.Add((byte)v);
				isSleep ^= withSleepTimes;
			}

			//rejected: by default compresses to ~80% (20% smaller). When withSleepTimes, to ~50%, but never mind, rarely used.
			//print.it(a.Count, Convert2.Compress(a.ToArray()).Length);

			return Convert.ToBase64String(a.ToArray());
		}
	}
}
