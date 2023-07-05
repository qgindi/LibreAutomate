/*/
role classLibrary;
define IDE_LA,NO_GLOBAL,NO_DEFAULT_CHARSET_UNICODE;
noWarnings 419,649;
preBuild ..\@Au.Editor\_prePostBuild.cs;
outputPath %folders.Workspace%\..\Au.Editor;
miscFlags 1;
noRef *\Au.dll;
resource resources\red_cross_cursor.cur /path;
/*/

