using System;
using System.IO;
namespace SevenZip {

	class LzmaAlone {
		public static void Decompress(string inputName, string outputName) {
			using var inStream = new FileStream(inputName, FileMode.Open, FileAccess.Read);
			using var outStream = new FileStream(outputName, FileMode.Create, FileAccess.Write);

			byte[] properties = new byte[5];
			if (inStream.Read(properties, 0, 5) != 5)
				throw (new Exception("input .lzma is too short"));
			Compression.LZMA.Decoder decoder = new Compression.LZMA.Decoder();
			decoder.SetDecoderProperties(properties);
			long outSize = 0;
			for (int i = 0; i < 8; i++) {
				int v = inStream.ReadByte();
				if (v < 0)
					throw (new Exception("Can't Read 1"));
				outSize |= ((long)(byte)v) << (8 * i);
			}
			long compressedSize = inStream.Length - inStream.Position;
			decoder.Code(inStream, outStream, compressedSize, outSize, null);
		}
	}
}
