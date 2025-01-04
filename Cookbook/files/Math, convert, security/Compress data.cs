/// If need to compress/decompress data without creating files, use class <see cref="Convert2"/>. The <b>DeflateX<> functions use zip compression; the <b>BrotliX<> functions compress better.

byte[] b = new byte[10000]; for (int i = 0; i < b.Length; i++) b[i] = (byte)i;
byte[] b2 = Convert2.DeflateCompress(b);
print.it(b2.Length);
byte[] b3 = Convert2.DeflateDecompress(b2);
print.it(b3.Length);

/// If need to convert compressed data (or any binary data) from/to string, the best way is Base64 encoding.

string s1 = Convert.ToBase64String(b2);
b2 = Convert.FromBase64String(s1);

/// See also recipe <+recipe>Zip files<>.
