/// To hash data, use classes from .NET namespace `System.Security.Cryptography`.
using System.Security.Cryptography;

var b = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
string pw = "password", s2 = "more";
string filePath = process.thisExePath;

byte[] sha256 = SHA256.HashData(b); //hash bytes
print.it(sha256);
print.it(Convert.ToHexStringLower(sha256)); //get hex string
print.it(System.Buffers.Text.Base64Url.EncodeToString(sha256)); //get Base64 string
print.it(SHA256.HashData(pw.ToUTF8())); //hash string
var fileHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(filePath))); //hash file
print.it("file hash", fileHash);

/// To compute a hash from multiple parts without at first concatenating them you can use <see cref="IncrementalHash"/>.

var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
hasher.AppendData(b);
hasher.AppendData(s2.ToUTF8());
byte[] hash = hasher.GetHashAndReset();

/// If you need a smaller hash, you can take part of an SHA256 hash.

byte[] hash8bytes = sha256[..8];
print.it(hash8bytes);
print.it(BitConverter.ToInt64(hash8bytes));

/// To encrypt/decrypt data can be used functions of class <see cref="Convert2"/>. If that is too simple, try <see cref="System.Security.Cryptography"/> (<.x>Convert2<> uses it internally) or cryptography libraries from NuGet.

var data = "Encryption example.";
var key = "password";
var enc = Convert2.AesEncryptS(data, key);
print.it(enc);
var dec = Convert2.AesDecryptS(enc, key);
print.it(dec);
