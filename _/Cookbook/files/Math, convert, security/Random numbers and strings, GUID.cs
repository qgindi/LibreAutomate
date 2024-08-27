/// Use class <see cref="Random"/> to generate random numbers or data when speed is more important than quality.

for (int i = 0; i < 10; i++) {
	print.it(Random.Shared.Next()); //0 to int.MaxValue
}

for (int i = 0; i < 10; i++) {
	print.it(Random.Shared.Next(0, 10)); //0 to 9
}

for (int i = 0; i < 10; i++) {
	print.it(Random.Shared.NextDouble()); //0 to 1.0
}

var b = new byte[10];
for (int i = 0; i < 10; i++) {
	Random.Shared.NextBytes(b);
	print.it("bytes", b);
}

/// To generate random numbers of good quality can be used cryptography functions.

for (int i = 0; i < 10; i++) {
	print.it(System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 100)); //0 to 99
}

/// The easiest way to generate a random string of good quality - create new GUID and convert to string.

print.it(Guid.NewGuid().ToString());

/// If need shorter strings of same quality, convert GUID to Base64 string. Note: Base64 can't be used in file names and URLs.

print.it(Convert.ToBase64String(Guid.NewGuid().ToByteArray()));

/// If need random strings of other formats (eg certain set of characters), look on the internet, for example in <google>NuGet<>.
