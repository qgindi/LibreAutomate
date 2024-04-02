/*/
role classLibrary;
define IDE_LA,AU,NO_GLOBAL,NO_DEFAULT_CHARSET_UNICODE;
noWarnings 419,649;
preBuild ..\@Au.Editor\_prePostBuild.cs;
outputPath %folders.Workspace%\dll;
miscFlags 1;
noRef *\Au.dll;
resource resources\red_cross_cursor.cur /path;
/*/

//Using outputPath %folders.Workspace%\dll; instead of outputPath %folders.Workspace%\..\Au.Editor; because the compiler would replace Au.dll with the older version.
//	_prePostBuild will copy Au.dll to Au.Editor.
