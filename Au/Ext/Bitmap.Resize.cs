//The main code is from FreeImage sources.

using System.Drawing;
using System.Drawing.Imaging;

namespace Au.Types;

/// <summary>
/// Used with <see cref="ExtMisc.Resize"/>
/// </summary>
public enum BRFilter {
	/// <summary>Produces sharper image (less blurry) than <b>Graphics.DrawImage</b> with <b>InterpolationMode.HighQualityBicubic</b>.</summary>
	Lanczos3,

	/// <summary>Produces slightly sharper image (less blurry) than <b>Graphics.DrawImage</b> with <b>InterpolationMode.HighQualityBicubic</b>.</summary>
	CatmullRom,

	/// <summary>Produces image similar to <b>Graphics.DrawImage</b> with <b>InterpolationMode.HighQualityBicubic</b>.</summary>
	Bicubic
}

public static partial class ExtMisc {
	/// <summary>
	/// Resizes this image.
	/// </summary>
	/// <returns>Resized image (new object). Returns this image if new width and height would be the same as of this image.</returns>
	/// <param name="b"></param>
	/// <param name="width">New width.</param>
	/// <param name="height">New height. If <i>width</i> or <i>height</i> is 0, calculates it (preserves aspect ratio).</param>
	/// <param name="filter"></param>
	/// <param name="dispose">When resized, call <b>Dispose</b> for this object.</param>
	/// <param name="premultiplied">
	/// Let the resized bitmap have <b>PixelFormat</b> = <b>Format32bppPArgb</b>. It prevents distortions at transparent-opaque boundaries.
	/// If <c>false</c>: if this bitmap has <b>Format32bppArgb</b> or <b>Format32bppPArgb</b>, does not change, else <b>PixelFormat.Format32bppArgb</b>.
	/// </param>
	/// <exception cref="ArgumentException">Unsupported <b>PixelFormat</b>.</exception>
	public static unsafe Bitmap Resize(this Bitmap b, int width, int height, BRFilter filter, bool dispose, bool premultiplied = false) {
		int wid1 = b.Width, hei1 = b.Height;
		if (width < 1) width = Math.Max(1, Math2.MulDiv(wid1, height, hei1));
		if (height < 1) height = Math.Max(1, Math2.MulDiv(hei1, width, wid1));
		if (width == wid1 && height == hei1) return b;

		var pf = b.PixelFormat;
		if (pf == PixelFormat.Format32bppPArgb) premultiplied = true;
		else if (premultiplied) pf = PixelFormat.Format32bppPArgb;
		else if(pf is not (PixelFormat.Format32bppArgb or PixelFormat.Format32bppRgb)) pf = PixelFormat.Format32bppArgb;

		var r = new Bitmap(width, height, pf);
		var d1 = b.LockBits(new(0, 0, wid1, hei1), ImageLockMode.ReadOnly, pf);
		var d2 = r.LockBits(new(0, 0, width, height), ImageLockMode.ReadWrite, pf);
		try {
			_FreeImage.Resize((byte*)d1.Scan0, wid1, hei1, (byte*)d2.Scan0, width, height, filter, premultiplied);
		}
		finally {
			b.UnlockBits(d1);
			r.UnlockBits(d2);
		}

		if (dispose) b.Dispose();
		return r;
	}

	/// <inheritdoc cref="Resize(Bitmap, int, int, BRFilter, bool, bool)"/>
	/// <param name="factor">Scaling factor. For example 2 to make 2 times bigger, or 0.5 to make 2 times smaller.</param>
	public static Bitmap Resize(this Bitmap b, double factor, BRFilter filter, bool dispose, bool premultiplied = false) {
		if (factor == 1) return b;
		var z = b.Size;
		int wid = (z.Width * factor).ToInt(), hei = (z.Height * factor).ToInt();
		if (wid == z.Width && hei == z.Height) return b;
		return Resize(b, wid, hei, filter, dispose, premultiplied);
	}

	static unsafe class _FreeImage {
		internal static void Resize(byte* src, int srcWidth, int srcHeight, byte* dst, int dstWidth, int dstHeight, BRFilter filter, bool premultiplied) {
			_Filter filt = filter switch {
				BRFilter.Lanczos3 => new _FilterLanczos3(),
				BRFilter.CatmullRom => new _FilterCatmullRom(),
				BRFilter.Bicubic => new _FilterBicubic(),
				_ => null
			};

			byte* tmp;

			if (dstWidth <= srcWidth) {
				// xy filtering

				if (srcWidth != dstWidth) {
					// source and destination widths are different so, we must
					// filter horizontally
					if (srcHeight != dstHeight) {
						// source and destination heights are also different so, we need
						// a temporary image
						tmp = MemoryUtil.Alloc(dstWidth * srcHeight * 4);
					} else {
						// source and destination heights are equal so, we can directly
						// factor into destination image (second filter method will not
						// be invoked)
						tmp = dst;
					}

					// factor source image horizontally into temporary (or destination) image
					_horizontalFilter(src, srcHeight, srcWidth, tmp, dstWidth, filt, premultiplied);
				} else {
					// source and destination widths are equal so, just copy the
					// image pointer
					tmp = src;
				}

				if (srcHeight != dstHeight) {
					// source and destination heights are different so, factor
					// temporary (or source) image vertically into destination image
					_verticalFilter(tmp, dstWidth, srcHeight, dst, dstHeight, filt, premultiplied);
				}
			} else {
				// yx filtering

				if (srcHeight != dstHeight) {
					// source and destination heights are different so, we must
					// filter vertically
					if (srcWidth != dstWidth) {
						// source and destination widths are also different so, we need
						// a temporary image
						tmp = MemoryUtil.Alloc(srcWidth * dstHeight * 4);
					} else {
						// source and destination widths are equal so, we can directly
						// factor into destination image (second filter method will not
						// be invoked)
						tmp = dst;
					}

					// factor source image vertically into temporary (or destination) image
					_verticalFilter(src, srcWidth, srcHeight, tmp, dstHeight, filt, premultiplied);
				} else {
					// source and destination heights are equal so, just copy the
					// image pointer
					tmp = src;
				}

				if (srcWidth != dstWidth) {
					// source and destination heights are different so, factor
					// temporary (or source) image horizontally into destination image
					_horizontalFilter(tmp, dstHeight, srcWidth, dst, dstWidth, filt, premultiplied);
				}
			}

			// free temporary image, if not pointing to either src or dst
			if (tmp != src && tmp != dst) MemoryUtil.Free(tmp);
		}

		const int FI_RGBA_RED = 2, FI_RGBA_GREEN = 1, FI_RGBA_BLUE = 0, FI_RGBA_ALPHA = 3;

		static void _horizontalFilter(byte* src, int height, int srcWidth, byte* dst, int dstWidth, _Filter filter, bool premultiplied) {
			// allocate and calculate the contributions
			_WeightsTable weightsTable = new(filter, dstWidth, srcWidth);

			// step through rows
			for (int y = 0; y < height; y++) {
				// factor each row
				byte* src_bits = src + srcWidth * 4 * y;
				byte* dst_bits = dst + dstWidth * 4 * y;

				for (int x = 0; x < dstWidth; x++) {
					// loop through row
					int iLeft = weightsTable.getLeftBoundary(x);
					int iLimit = weightsTable.getRightBoundary(x) - iLeft;
					byte* pixel = src_bits + iLeft * 4;
					double r = 0, g = 0, b = 0, a = 0;

					for (int i = 0; i < iLimit; i++) {
						// scan between boundaries
						// accumulate weighted effect of each neighboring pixel
						double weight = weightsTable.getWeight(x, i);
						r += (weight * pixel[FI_RGBA_RED]);
						g += (weight * pixel[FI_RGBA_GREEN]);
						b += (weight * pixel[FI_RGBA_BLUE]);
						a += (weight * pixel[FI_RGBA_ALPHA]);
						pixel += 4;
					}

					// clamp and place result in destination pixel
					int ai = Math.Clamp((int)(a + 0.5), 0, 0xFF);
					dst_bits[FI_RGBA_ALPHA] = (byte)ai;
					if (!premultiplied) ai = 0xFF; //else colors cannot be > alpha
					dst_bits[FI_RGBA_RED] = (byte)Math.Clamp((int)(r + 0.5), 0, ai);
					dst_bits[FI_RGBA_GREEN] = (byte)Math.Clamp((int)(g + 0.5), 0, ai);
					dst_bits[FI_RGBA_BLUE] = (byte)Math.Clamp((int)(b + 0.5), 0, ai);
					dst_bits += 4;
				}
			}
		}

		static void _verticalFilter(byte* src, int width, int srcHeight, byte* dst, int dstHeight, _Filter filter, bool premultiplied) {
			// allocate and calculate the contributions
			_WeightsTable weightsTable = new(filter, dstHeight, srcHeight);

			// step through columns
			byte* src_base = src;
			byte* dst_base = dst;
			int src_pitch = width * 4;
			int dst_pitch = src_pitch;

			for (int x = 0; x < width; x++) {
				// work on column x in dst
				int index = x * 4;
				byte* dst_bits = dst_base + index;

				// factor each column
				for (int y = 0; y < dstHeight; y++) {
					// loop through column
					int iLeft = weightsTable.getLeftBoundary(y);
					int iLimit = weightsTable.getRightBoundary(y) - iLeft;
					byte* src_bits = src_base + iLeft * src_pitch + index;
					double r = 0, g = 0, b = 0, a = 0;

					for (int i = 0; i < iLimit; i++) {
						// scan between boundaries
						// accumulate weighted effect of each neighboring pixel
						double weight = weightsTable.getWeight(y, i);
						r += (weight * src_bits[FI_RGBA_RED]);
						g += (weight * src_bits[FI_RGBA_GREEN]);
						b += (weight * src_bits[FI_RGBA_BLUE]);
						a += (weight * src_bits[FI_RGBA_ALPHA]);
						src_bits += src_pitch;
					}

					// clamp and place result in destination pixel
					int ai = Math.Clamp((int)(a + 0.5), 0, 0xFF);
					dst_bits[FI_RGBA_ALPHA] = (byte)ai;
					if (!premultiplied) ai = 0xFF; //else colors cannot be > alpha
					dst_bits[FI_RGBA_RED] = (byte)Math.Clamp((int)(r + 0.5), 0, ai);
					dst_bits[FI_RGBA_GREEN] = (byte)Math.Clamp((int)(g + 0.5), 0, ai);
					dst_bits[FI_RGBA_BLUE] = (byte)Math.Clamp((int)(b + 0.5), 0, ai);
					dst_bits += dst_pitch;
				}
			}
		}

		struct _WeightsTable {
			_Contribution[] _table;
			int _windowSize;
			int _lineLength;

			public _WeightsTable(_Filter filter, int uDstSize, int uSrcSize) {
				double dWidth;
				double dFScale;
				double dFilterWidth = filter.width;

				// factor factor
				double dScale = (double)uDstSize / uSrcSize;

				if (dScale < 1.0) {
					// minification
					dWidth = dFilterWidth / dScale;
					dFScale = dScale;
				} else {
					// magnification
					dWidth = dFilterWidth;
					dFScale = 1.0;
				}

				// allocate a new line contributions structure
				//
				// window size is the number of sampled pixels
				_windowSize = 2 * (int)Math.Ceiling(dWidth) + 1;
				// length of dst line (no. of rows / cols) 
				_lineLength = uDstSize;

				// allocate list of contributions 
				_table = new _Contribution[_lineLength];
				for (int u = 0; u < _lineLength; u++) {
					// allocate contributions for every pixel
					_table[u].Weights = new double[_windowSize];
				}

				// offset for discrete to continuous coordinate conversion
				double dOffset = 0.5 / dScale;

				for (int u = 0; u < _lineLength; u++) {
					// scan through line of contributions

					// inverse mapping (discrete dst 'u' to continous src 'dCenter')
					double dCenter = u / dScale + dOffset;

					// find the significant edge points that affect the pixel
					int iLeft = Math.Max(0, (int)(dCenter - dWidth + 0.5));
					int iRight = Math.Min((int)(dCenter + dWidth + 0.5), uSrcSize);

					_table[u].Left = iLeft;
					_table[u].Right = iRight;

					double dTotalWeight = 0;  // sum of weights (initialized to zero)
					for (int iSrc = iLeft; iSrc < iRight; iSrc++) {
						// calculate weights
						double weight = dFScale * filter.Filter(dFScale * (iSrc + 0.5 - dCenter));
						_table[u].Weights[iSrc - iLeft] = weight;
						dTotalWeight += weight;
					}
					if ((dTotalWeight > 0) && (dTotalWeight != 1)) {
						// normalize weight of neighbouring points
						for (int iSrc = iLeft; iSrc < iRight; iSrc++) {
							// normalize point
							_table[u].Weights[iSrc - iLeft] /= dTotalWeight;
						}
					}

					// simplify the filter, discarding null weights at the right
					{
						int iTrailing = iRight - iLeft - 1;
						while (_table[u].Weights[iTrailing] == 0) {
							_table[u].Right--;
							iTrailing--;
							if (_table[u].Right == _table[u].Left) {
								break;
							}
						}

					}

				} // next dst pixel
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public double getWeight(int dst_pos, int src_pos) {
				return _table[dst_pos].Weights[src_pos];
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int getLeftBoundary(int dst_pos) {
				return _table[dst_pos].Left;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int getRightBoundary(int dst_pos) {
				return _table[dst_pos].Right;
			}

			struct _Contribution {
				public double[] Weights;
				public int Left, Right;
			}
		}

#if true
		abstract class _Filter {
			public abstract double width { get; }

			public abstract double Filter(double dVal);
		}

		class _FilterLanczos3 : _Filter {
			public override double width => 3;

			public override double Filter(double dVal) {
				dVal = Math.Abs(dVal);
				if (dVal < width) {
					return (_sinc(dVal) * _sinc(dVal / width));
				}
				return 0;
			}

			static double _sinc(double value) {
				if (value != 0) {
					value *= Math.PI;
					return (Math.Sin(value) / value);
				}
				return 1;
			}
		}

		class _FilterCatmullRom : _Filter {
			public override double width => 2;

			public override double Filter(double dVal) {
				if (dVal < -2) return 0;
				if (dVal < -1) return (0.5 * (4 + dVal * (8 + dVal * (5 + dVal))));
				if (dVal < 0) return (0.5 * (2 + dVal * dVal * (-5 - 3 * dVal)));
				if (dVal < 1) return (0.5 * (2 + dVal * dVal * (-5 + 3 * dVal)));
				if (dVal < 2) return (0.5 * (4 + dVal * (-8 + dVal * (5 - dVal))));
				return 0;
			}
		}

		class _FilterBicubic : _Filter {
			readonly double p0, p2, p3;
			readonly double q0, q1, q2, q3;

			public _FilterBicubic() {
				double b = 1 / 3d, c = b;
				p0 = (6 - 2 * b) / 6;
				p2 = (-18 + 12 * b + 6 * c) / 6;
				p3 = (12 - 9 * b - 6 * c) / 6;
				q0 = (8 * b + 24 * c) / 6;
				q1 = (-12 * b - 48 * c) / 6;
				q2 = (6 * b + 30 * c) / 6;
				q3 = (-b - 6 * c) / 6;
			}

			public override double width => 2;

			public override double Filter(double dVal) {
				dVal = Math.Abs(dVal);
				if (dVal < 1)
					return (p0 + dVal * dVal * (p2 + dVal * p3));
				if (dVal < 2)
					return (q0 + dVal * (q1 + dVal * (q2 + dVal * q3)));
				return 0;
			}
		}
#else
		class _Filter {
			public readonly double width = 3;

			public double Filter(double dVal) {
				dVal = Math.Abs(dVal);
				if (dVal < width) {
					return (_sinc(dVal) * _sinc(dVal / width));
				}
				return 0;
			}

			static double _sinc(double value) {
				if (value != 0) {
					value *= Math.PI;
					return (Math.Sin(value) / value);
				}
				return 1;
			}
		}
#endif
	}
}
