# Encrypt, hash
To encrypt/decrypt data can be used functions of class <a href='/api/Au.More.Convert2.html'>Convert2</a>. If that is too simple, try <a href='https://www.google.com/search?q=System.Security.Cryptography+namespace'>System.Security.Cryptography</a> (<b>Convert2</b> uses it internally) or cryptography libraries from NuGet.

```csharp
var data = "Encryption example.";
var key = "password";
var enc = Convert2.AesEncryptS(data, key);
print.it(enc);
var dec = Convert2.AesDecryptS(enc, key);
print.it(dec);
```

Class <a href='/api/Au.More.Hash.html'>Hash</a> contains simple-to-use and fast functions to hash data.

```csharp
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
```

