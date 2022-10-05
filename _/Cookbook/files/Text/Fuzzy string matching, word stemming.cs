/// Use fuzzy string matching to compare or find approximately matching words, for example misspelled. We'll use library <link https://github.com/JakeBayer/FuzzySharp>FuzzySharp<>. NuGet: <+nuget>FuzzySharp<>.
///
/// Use word stemming (remove suffix) to find all word forms (with/without any suffix). We'll use library <link https://github.com/nemec/porter2-stemmer>porter2-stemmer<>. NuGet: <+nuget>Porter2Stemmer<>. English only.

/*/ nuget -\FuzzySharp; nuget -\Porter2Stemmer; /*/

/// Get % of similarity.

string s1 = "National Geographic";
string s2 = "nationl geografik";

print.it(FuzzySharp.Fuzz.PartialRatio(s1, s2, FuzzySharp.PreProcess.PreprocessMode.Full));

/// Get Levenshtein distance.

print.it(FuzzySharp.Levenshtein.EditDistance(s1.Lower(), s2.Lower()));

/// The FuzzySharp web page gives code examples for most functions.

/// Get word stem.

var stemmer = new Porter2Stemmer.EnglishPorter2Stemmer();
print.it(stemmer.Stem("friend").Value);
print.it(stemmer.Stem("friendly").Value);
print.it(stemmer.Stem("friends").Value);
print.it(stemmer.Stem("friend's").Value);
