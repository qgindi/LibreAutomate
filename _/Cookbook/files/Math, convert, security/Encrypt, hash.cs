/// To encrypt/decrypt data can be used functions of class <see cref="Convert2"/>. If that is too simple, try <see cref="System.Security.Cryptography"/> (<b>Convert2<> uses it internally) or cryptography libraries from NuGet.

var data = "Encryption example.";
var key = "password";
var enc = Convert2.AesEncryptS(data, key);
print.it(enc);
var dec = Convert2.AesDecryptS(enc, key);
print.it(dec);

/// Class <see cref="Hash"/> contains simple-to-use and fast functions to hash data.

var b = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
string pw = "password", s2 = "more";

print.it(Hash.Fnv1(b));
print.it(Hash.Fnv1(pw));

print.it(Hash.MD5(b, false));
print.it(Hash.MD5(pw, false));

var md5 = new Hash.MD5Context();
md5.Add(pw);
md5.Add(s2);
print.it(md5.Hash.ToString());

print.it(Hash.Crypto(b, "SHA256", false));
print.it(Hash.Crypto(pw, "SHA256", false));
