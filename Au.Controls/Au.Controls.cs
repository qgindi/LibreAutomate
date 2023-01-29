/*/
role classLibrary;
define IDE_LA,NO_GLOBAL,NO_DEFAULT_CHARSET_UNICODE;
noWarnings 1591,419,649;
testInternal Au;
outputPath %folders.Workspace%\dll;
miscFlags 1;
noRef *\_\Au.dll;
pr \Au.sln\@Au\Au.cs;
resource Themes\Generic.xaml /embedded;
/*/
