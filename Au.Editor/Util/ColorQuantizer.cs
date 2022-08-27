using System.Drawing;
using System.Drawing.Imaging;

/// <summary>
/// Reduces image color depth with better quality than GDI/GDI+. It makes image smaller.
/// Uses color quantizer algorithm of Xiaolin Wu.
/// Same instance can be reused to quantize multiple images and avoid 0.7 MB of garbage for each; the static functions use this.
/// </summary>
unsafe class ColorQuantizer {
	/// <summary>
	/// Takes screenshot of specified rectangle in screen, quantizes colors to make smaller, compresses, Base64 encodes and returns comment string like <c>" /*image:...*/"</c>.
	/// If fails, prints warning and returns null.
	/// </summary>
	/// <param name="r">Rectangle in screen. Must be not DPI-scaled. This function inflates it if need for DPI of that screen.</param>
	public static string MakeScreenshotComment(RECT r) {
		int dpi = screen.of(r).Dpi;
		if (dpi != 96) {
			r.Inflate((Dpi.Scale(r.Width, dpi) - r.Width) / 2, (Dpi.Scale(r.Height, dpi) - r.Height) / 2);
		}
		try {
			var b = uiimage.capture(r);
			var a = Quantize(b, 16, dpi);
			var z = Convert2.BrotliCompress(a);
			return "/*image:"
				//+ "\r\n" //rejected
				+ "WkJN" + Convert.ToBase64String(z) + "*/";
		}
		catch (Exception e1) { print.warning("MakeScreenshotComment() failed. " + e1.ToStringWithoutStack()); return null; }
	}

	[ThreadStatic] static WeakReference<ColorQuantizer> t_wr;

	/// <summary>
	/// Quantizes bitmap.
	/// </summary>
	/// <param name="b">Bitmap with pixel bit count 32 or 24. If 32, alpha is ignored.</param>
	/// <param name="nColors">Max count of colors desired. Must be 2 to 256 inclusive. For example a 4-bit bitmap can have max 16 colors; 8-bit - max 256 colors.</param>
	/// <param name="dpi">Image resolution as DPI. If more than 96, this function writes this info to the bitmap header.</param>
	/// <returns>.bmp file data (BITMAPFILEHEADER, BITMAPINFOHEADER, color table, pixel bits) of quantized bitmap.</returns>
	/// <exception cref="ArgumentException"></exception>
	public static byte[] Quantize(Bitmap b, int nColors, int dpi = 0) {
		t_wr ??= new(null); if (!t_wr.TryGetTarget(out var q)) t_wr.SetTarget(q = new()); //avoid 0.7 MB of garbage each time

		var d = b.LockBits(new(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, b.PixelFormat);
		try {
			var p = (byte*)d.Scan0;
			bool topDown = d.Stride >= 0;
			if (!topDown) p -= -d.Stride * (d.Height - 1);

			var a = q.Quantize(b.Width, b.Height, Image.GetPixelFormatSize(d.PixelFormat), p, ref nColors, topDown, dpi);

			//print.it(a.Length, a);
			//var z=Convert2.Compress(a);
			//print.it(a.Length, z.Length);
			//print.it(Convert.ToBase64String(z));

			return a;
		}
		finally { b.UnlockBits(d); }
	}

	/// <summary>
	/// Quantizes bitmap.
	/// </summary>
	/// <param name="b">Bitmap with pixel bit count 32 or 24. If 32, alpha is ignored.</param>
	/// <param name="nColors">Max count of colors desired. Must be 2 to 256 inclusive. For example a 4-bit bitmap can have max 16 colors; 8-bit - max 256 colors.</param>
	/// <param name="dpi">Image resolution as DPI. If more than 96, this function writes this info to the bitmap header.</param>
	/// <returns>Quantized bitmap.</returns>
	/// <exception cref="ArgumentException"></exception>
	public static Bitmap QuantizeB(Bitmap b, int nColors, int dpi = 0) => new(new MemoryStream(Quantize(b, nColors, dpi)));

	// DIB data
	int _width, _height, _lineSize, _colorBytes;
	byte* _bits;

	struct Box { public int r0, r1, g0, g1, b0, b1, vol; }

	const int FI_RGBA_RED = 2, FI_RGBA_GREEN = 1, FI_RGBA_BLUE = 0;
	const int FIQ_SIZE_3D = 33 * 33 * 33;

	float[] _gm2;
	int[] _wt, _mr, _mg, _mb;
	ushort[] Qadd;
	byte[] _tag;

	/// <summary>
	/// Quantizes bitmap.
	/// </summary>
	/// <param name="width"></param>
	/// <param name="height"></param>
	/// <param name="bitCount">Pixel bit count of the input bitmap. Must be 32 or 24. If 32, alpha is ignored.</param>
	/// <param name="bits">Pixels.</param>
	/// <param name="nColors">Input - max count of colors desired. Must be 2 to 256 inclusive. For example a 4-bit bitmap can have max 16 colors; 8-bit - max 256 colors. Output - actual count of colors in color table of the returned bitmap.</param>
	/// <param name="topDown">Top-down bitmap. For example GDI+ (System.Drawing) bitmaps usually are top-down.</param>
	/// <param name="dpi">Image resolution as DPI. If more than 96, this function writes this info to the bitmap header.</param>
	/// <returns>.bmp file data (BITMAPFILEHEADER, BITMAPINFOHEADER, color table, pixel bits) of quantized bitmap.</returns>
	/// <exception cref="ArgumentException"></exception>
	public byte[] Quantize(int width, int height, int bitCount, byte* bits, ref int nColors, bool topDown, int dpi = 0) {
		if (!(bits != null && bitCount is (32 or 24) && width > 0 && height > 0 && nColors >= 2 && nColors <= 256)) throw new ArgumentException();

		_width = width;
		_height = height;
		_lineSize = _LineSize(bitCount);
		_colorBytes = bitCount / 8;
		_bits = bits;

		//Debug_.MemorySetAnchor_();
		static void _Array3D<T>(ref T[] a) { if (a == null) a = new T[FIQ_SIZE_3D]; else Array.Clear(a, 0, a.Length); }
		_Array3D(ref _gm2);
		_Array3D(ref _wt);
		_Array3D(ref _mr);
		_Array3D(ref _mg);
		_Array3D(ref _mb);
		_Array3D(ref _tag);
		Qadd = new ushort[_width * _height];

		var cube = stackalloc Box[256];
		int next;
		int i, weight;
		int k;
		var vv = stackalloc float[256]; float temp;

		// Compute 3D histogram

		Hist3D(_wt, _mr, _mg, _mb, _gm2);

		// Compute moments

		M3D(_wt, _mr, _mg, _mb, _gm2);

		cube[0].r0 = cube[0].g0 = cube[0].b0 = 0;
		cube[0].r1 = cube[0].g1 = cube[0].b1 = 32;
		next = 0;

		for (i = 1; i < nColors; i++) {
			if (Cut(ref cube[next], ref cube[i])) {
				// volume test ensures we won't try to cut one-cell box
				vv[next] = (cube[next].vol > 1) ? Var(cube[next]) : 0;
				vv[i] = (cube[i].vol > 1) ? Var(cube[i]) : 0;
			} else {
				vv[next] = 0f;   // don't try to split this box again
				i--;              // didn't create box i
			}

			next = 0; temp = vv[0];

			for (k = 1; k <= i; k++) {
				if (vv[k] > temp) {
					temp = vv[k]; next = k;
				}
			}

			if (temp <= 0.0) {
				nColors = i + 1;

				// Error: "Only got 'nColors' boxes"

				break;
			}
		}

		// Partition done

		// Allocate a new dib

		int bpp = nColors > 16 ? 8 : 4, lineSize = _LineSize(bpp), paletteSize = nColors * 4, bitsSize = _height * lineSize;
		int headersSize = sizeof(BITMAPFILEHEADER) + sizeof(Api.BITMAPINFOHEADER), bitsOffset = headersSize + paletteSize;
		var ret = new byte[bitsOffset + bitsSize];
		fixed (byte* pret = ret) {

			// create an optimized palette

			var new_pal = (RGBQUAD*)(pret + headersSize);

			for (k = 0; k < nColors; k++) {
				Mark(cube[k], k, _tag);
				weight = Vol(cube[k], _wt);

				if (weight != 0) {
					new_pal[k].rgbRed = (byte)(((float)Vol(cube[k], _mr) / (float)weight) + 0.5f);
					new_pal[k].rgbGreen = (byte)(((float)Vol(cube[k], _mg) / (float)weight) + 0.5f);
					new_pal[k].rgbBlue = (byte)(((float)Vol(cube[k], _mb) / (float)weight) + 0.5f);
				} else {
					// Error: bogus box 'k'

					new_pal[k].rgbRed = new_pal[k].rgbGreen = new_pal[k].rgbBlue = 0;
				}
			}

			// create pixel bits

			var newBits = pret + bitsOffset;
			for (int y = 0; y < _height; y++) {
				byte* new_bits = newBits + (y * lineSize);
				int yy = topDown ? _height - y - 1 : y; //always save as bottom-up
				int lineStart = yy * _width;

				if (bpp == 8) {
					for (int x = 0; x < _width; x++)
						new_bits[x] = _tag[Qadd[lineStart + x]];
				} else {
					int x, n = _width & ~1; //because using x+1
					for (x = 0; x < n; x += 2)
						new_bits[x / 2] = (byte)((_tag[Qadd[lineStart + x]] << 4) | (_tag[Qadd[lineStart + x + 1]] & 0xF));
					if (n != _width) new_bits[x / 2] = (byte)(_tag[Qadd[lineStart + x]] << 4);
				}
			}

			// output 'new_pal' as color look-up table contents,
			// 'new_bits' as the quantized image (array of table addresses).
			var f2 = (BITMAPFILEHEADER*)pret;
			f2->bfType = Math2.MakeWord('B', 'M'); f2->bfSize = ret.Length; f2->bfOffBits = bitsOffset;
			var h2 = (Api.BITMAPINFOHEADER*)(f2 + 1);
			h2->biSize = sizeof(Api.BITMAPINFOHEADER); h2->biPlanes = 1;
			h2->biBitCount = (ushort)bpp; h2->biClrUsed = nColors;
			h2->biWidth = _width; h2->biHeight = _height;
			if (dpi > 96) h2->biXPelsPerMeter = h2->biYPelsPerMeter = (dpi * 39.370079).ToInt();
		}

		//Debug_.MemoryPrint_();
		return ret;

		int _LineSize(int bitCount) => Math2.AlignUp(_width * bitCount, 32) / 8;
	}

	// 3D array indexation
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static int FIQ_INDEX(int r, int g, int b) => (r << 10) + (r << 6) + r + (g << 5) + g + b;

	// Histogram is in elements 1..HISTSIZE along each axis,
	// element 0 is for base or marginal value
	// NB: these must start out 0!

	// Build 3-D color histogram of counts, r/g/b, c^2
	void Hist3D(int[] vwt, int[] vmr, int[] vmg, int[] vmb, float[] m2) {
		int ind = 0;
		int inr, ing, inb;
		var table = stackalloc int[256];
		int i, y, x;

		for (i = 0; i < 256; i++)
			table[i] = i * i;

		for (y = 0; y < _height; y++) {
			var bits = _bits + y * _lineSize;

			for (x = 0; x < _width; x++) {
				inr = (bits[FI_RGBA_RED] >> 3) + 1;
				ing = (bits[FI_RGBA_GREEN] >> 3) + 1;
				inb = (bits[FI_RGBA_BLUE] >> 3) + 1;
				ind = FIQ_INDEX(inr, ing, inb);
				Qadd[y * _width + x] = (ushort)ind;
				// [inr][ing][inb]
				vwt[ind]++;
				vmr[ind] += bits[FI_RGBA_RED];
				vmg[ind] += bits[FI_RGBA_GREEN];
				vmb[ind] += bits[FI_RGBA_BLUE];
				m2[ind] += (float)(table[bits[FI_RGBA_RED]] + table[bits[FI_RGBA_GREEN]] + table[bits[FI_RGBA_BLUE]]);
				bits += _colorBytes;
			}
		}
	}
	//___________________________________________

	// At conclusion of the histogram step, we can interpret
	// wt[r][g][b] = sum over voxel of P(c)
	// mr[r][g][b] = sum over voxel of r*P(c)  ,  similarly for mg, mb
	// m2[r][g][b] = sum over voxel of c^2*P(c)
	// Actually each of these should be divided by 'ImageSize' to give the usual
	// interpretation of P() as ranging from 0 to 1, but we needn't do that here.

	// We now convert histogram into moments so that we can rapidly calculate
	// the sums of the above quantities over any desired box.

	// Compute cumulative moments
	static void M3D(int[] vwt, int[] vmr, int[] vmg, int[] vmb, float[] m2) {
		int ind1, ind2;
		byte i, r, g, b;
		int line, line_r, line_g, line_b;
		int* area = stackalloc int[33], area_r = stackalloc int[33], area_g = stackalloc int[33], area_b = stackalloc int[33];
		float line2; var area2 = stackalloc float[33];

		for (r = 1; r <= 32; r++) {
			for (i = 0; i <= 32; i++) {
				area2[i] = 0;
				area[i] = area_r[i] = area_g[i] = area_b[i] = 0;
			}
			for (g = 1; g <= 32; g++) {
				line2 = 0;
				line = line_r = line_g = line_b = 0;
				for (b = 1; b <= 32; b++) {
					ind1 = FIQ_INDEX(r, g, b); // [r][g][b]
					line += vwt[ind1];
					line_r += vmr[ind1];
					line_g += vmg[ind1];
					line_b += vmb[ind1];
					line2 += m2[ind1];
					area[b] += line;
					area_r[b] += line_r;
					area_g[b] += line_g;
					area_b[b] += line_b;
					area2[b] += line2;
					ind2 = ind1 - 1089; // [r-1][g][b]
					vwt[ind1] = vwt[ind2] + area[b];
					vmr[ind1] = vmr[ind2] + area_r[b];
					vmg[ind1] = vmg[ind2] + area_g[b];
					vmb[ind1] = vmb[ind2] + area_b[b];
					m2[ind1] = m2[ind2] + area2[b];
				}
			}
		}
	}
	//___________________________________________

	// Compute sum over a box of any given statistic
	static int Vol(in Box cube, int[] mmt) {
		return mmt[FIQ_INDEX(cube.r1, cube.g1, cube.b1)]
			  - mmt[FIQ_INDEX(cube.r1, cube.g1, cube.b0)]
			  - mmt[FIQ_INDEX(cube.r1, cube.g0, cube.b1)]
			  + mmt[FIQ_INDEX(cube.r1, cube.g0, cube.b0)]
			  - mmt[FIQ_INDEX(cube.r0, cube.g1, cube.b1)]
			  + mmt[FIQ_INDEX(cube.r0, cube.g1, cube.b0)]
			  + mmt[FIQ_INDEX(cube.r0, cube.g0, cube.b1)]
			  - mmt[FIQ_INDEX(cube.r0, cube.g0, cube.b0)];
	}
	//___________________________________________

	// The next two routines allow a slightly more efficient calculation
	// of Vol() for a proposed subbox of a given box.  The sum of Top()
	// and Bottom() is the Vol() of a subbox split in the given direction
	// and with the specified new upper bound.

	// Compute part of Vol(cube, mmt) that doesn't depend on r1, g1, or b1
	// (depending on dir)
	static int Bottom(in Box cube, byte dir, int[] mmt) {
		switch (dir) {
		case FI_RGBA_RED:
			return -mmt[FIQ_INDEX(cube.r0, cube.g1, cube.b1)]
					+ mmt[FIQ_INDEX(cube.r0, cube.g1, cube.b0)]
					+ mmt[FIQ_INDEX(cube.r0, cube.g0, cube.b1)]
					- mmt[FIQ_INDEX(cube.r0, cube.g0, cube.b0)];
		case FI_RGBA_GREEN:
			return -mmt[FIQ_INDEX(cube.r1, cube.g0, cube.b1)]
					+ mmt[FIQ_INDEX(cube.r1, cube.g0, cube.b0)]
					+ mmt[FIQ_INDEX(cube.r0, cube.g0, cube.b1)]
					- mmt[FIQ_INDEX(cube.r0, cube.g0, cube.b0)];
		case FI_RGBA_BLUE:
			return -mmt[FIQ_INDEX(cube.r1, cube.g1, cube.b0)]
					+ mmt[FIQ_INDEX(cube.r1, cube.g0, cube.b0)]
					+ mmt[FIQ_INDEX(cube.r0, cube.g1, cube.b0)]
					- mmt[FIQ_INDEX(cube.r0, cube.g0, cube.b0)];
		}

		return 0;
	}
	//___________________________________________

	// Compute remainder of Vol(cube, mmt), substituting pos for
	// r1, g1, or b1 (depending on dir)
	static int Top(in Box cube, byte dir, int pos, int[] mmt) {
		switch (dir) {
		case FI_RGBA_RED:
			return mmt[FIQ_INDEX(pos, cube.g1, cube.b1)]
				   - mmt[FIQ_INDEX(pos, cube.g1, cube.b0)]
				   - mmt[FIQ_INDEX(pos, cube.g0, cube.b1)]
				   + mmt[FIQ_INDEX(pos, cube.g0, cube.b0)];
		case FI_RGBA_GREEN:
			return mmt[FIQ_INDEX(cube.r1, pos, cube.b1)]
				   - mmt[FIQ_INDEX(cube.r1, pos, cube.b0)]
				   - mmt[FIQ_INDEX(cube.r0, pos, cube.b1)]
				   + mmt[FIQ_INDEX(cube.r0, pos, cube.b0)];
		case FI_RGBA_BLUE:
			return mmt[FIQ_INDEX(cube.r1, cube.g1, pos)]
				   - mmt[FIQ_INDEX(cube.r1, cube.g0, pos)]
				   - mmt[FIQ_INDEX(cube.r0, cube.g1, pos)]
				   + mmt[FIQ_INDEX(cube.r0, cube.g0, pos)];
		}

		return 0;
	}
	//___________________________________________

	// Compute the weighted variance of a box 
	// NB: as with the raw statistics, this is really the variance * ImageSize 
	float Var(in Box cube) {
		float dr = (float)Vol(cube, _mr);
		float dg = (float)Vol(cube, _mg);
		float db = (float)Vol(cube, _mb);
		float xx = _gm2[FIQ_INDEX(cube.r1, cube.g1, cube.b1)]
				- _gm2[FIQ_INDEX(cube.r1, cube.g1, cube.b0)]
				 - _gm2[FIQ_INDEX(cube.r1, cube.g0, cube.b1)]
				 + _gm2[FIQ_INDEX(cube.r1, cube.g0, cube.b0)]
				 - _gm2[FIQ_INDEX(cube.r0, cube.g1, cube.b1)]
				 + _gm2[FIQ_INDEX(cube.r0, cube.g1, cube.b0)]
				 + _gm2[FIQ_INDEX(cube.r0, cube.g0, cube.b1)]
				 - _gm2[FIQ_INDEX(cube.r0, cube.g0, cube.b0)];

		return (xx - (dr * dr + dg * dg + db * db) / (float)Vol(cube, _wt));
	}
	//___________________________________________

	// We want to minimize the sum of the variances of two subboxes.
	// The sum(c^2) terms can be ignored since their sum over both subboxes
	// is the same (the sum for the whole box) no matter where we split.
	// The remaining terms have a minus sign in the variance formula,
	// so we drop the minus sign and MAXIMIZE the sum of the two terms.
	float Maximize(in Box cube, byte dir, int first, int last, out int cut, int whole_r, int whole_g, int whole_b, int whole_w) {
		int half_r, half_g, half_b, half_w;
		int i;
		float temp;

		int base_r = Bottom(cube, dir, _mr);
		int base_g = Bottom(cube, dir, _mg);
		int base_b = Bottom(cube, dir, _mb);
		int base_w = Bottom(cube, dir, _wt);

		float max = 0f;

		cut = -1;

		for (i = first; i < last; i++) {
			half_r = base_r + Top(cube, dir, i, _mr);
			half_g = base_g + Top(cube, dir, i, _mg);
			half_b = base_b + Top(cube, dir, i, _mb);
			half_w = base_w + Top(cube, dir, i, _wt);

			// now half_x is sum over lower half of box, if split at i

			if (half_w == 0) {      // subbox could be empty of pixels!
				continue;           // never split into an empty box
			} else {
				temp = ((float)half_r * half_r + (float)half_g * half_g + (float)half_b * half_b) / half_w;
			}

			half_r = whole_r - half_r;
			half_g = whole_g - half_g;
			half_b = whole_b - half_b;
			half_w = whole_w - half_w;

			if (half_w == 0) {      // subbox could be empty of pixels!
				continue;           // never split into an empty box
			} else {
				temp += ((float)half_r * half_r + (float)half_g * half_g + (float)half_b * half_b) / half_w;
			}

			if (temp > max) {
				max = temp;
				cut = i;
			}
		}

		return max;
	}
	//___________________________________________

	bool Cut(ref Box set1, ref Box set2) {
		byte dir;
		int cutr, cutg, cutb;

		int whole_r = Vol(set1, _mr);
		int whole_g = Vol(set1, _mg);
		int whole_b = Vol(set1, _mb);
		int whole_w = Vol(set1, _wt);

		float maxr = Maximize(set1, FI_RGBA_RED, set1.r0 + 1, set1.r1, out cutr, whole_r, whole_g, whole_b, whole_w);
		float maxg = Maximize(set1, FI_RGBA_GREEN, set1.g0 + 1, set1.g1, out cutg, whole_r, whole_g, whole_b, whole_w);
		float maxb = Maximize(set1, FI_RGBA_BLUE, set1.b0 + 1, set1.b1, out cutb, whole_r, whole_g, whole_b, whole_w);

		if ((maxr >= maxg) && (maxr >= maxb)) {
			dir = FI_RGBA_RED;

			if (cutr < 0) {
				return false; // can't split the box
			}
		} else if ((maxg >= maxr) && (maxg >= maxb)) {
			dir = FI_RGBA_GREEN;
		} else {
			dir = FI_RGBA_BLUE;
		}

		set2.r1 = set1.r1;
		set2.g1 = set1.g1;
		set2.b1 = set1.b1;

		switch (dir) {
		case FI_RGBA_RED:
			set2.r0 = set1.r1 = cutr;
			set2.g0 = set1.g0;
			set2.b0 = set1.b0;
			break;

		case FI_RGBA_GREEN:
			set2.g0 = set1.g1 = cutg;
			set2.r0 = set1.r0;
			set2.b0 = set1.b0;
			break;

		case FI_RGBA_BLUE:
			set2.b0 = set1.b1 = cutb;
			set2.r0 = set1.r0;
			set2.g0 = set1.g0;
			break;
		}

		set1.vol = (set1.r1 - set1.r0) * (set1.g1 - set1.g0) * (set1.b1 - set1.b0);
		set2.vol = (set2.r1 - set2.r0) * (set2.g1 - set2.g0) * (set2.b1 - set2.b0);

		return true;
	}
	//___________________________________________

	static void Mark(in Box cube, int label, byte[] tag) {
		for (int r = cube.r0 + 1; r <= cube.r1; r++) {
			for (int g = cube.g0 + 1; g <= cube.g1; g++) {
				for (int b = cube.b0 + 1; b <= cube.b1; b++) {
					tag[FIQ_INDEX(r, g, b)] = (byte)label;
				}
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	internal struct BITMAPFILEHEADER {
		public ushort bfType;
		public int bfSize;
		public ushort bfReserved1;
		public ushort bfReserved2;
		public int bfOffBits;
	}

#pragma warning disable CS0649 //field never assigned

	internal struct RGBQUAD {
		public byte rgbBlue;
		public byte rgbGreen;
		public byte rgbRed;
		public byte rgbReserved;
	}
}
