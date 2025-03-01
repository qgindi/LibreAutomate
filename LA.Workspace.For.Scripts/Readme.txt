This is for creating docs which is done within working LibraAutomate (LA) environment

Setup the LA workspace
1.  Edit files.xml in this directory.  Change the Scripts path in line 3 to the absolute path to the scripts directory.  
	The relative path from this readme file is ..\Scripts but the absolute path will depend 
	upon the name and location of your repository for LA
2.  Build LA --> Install LA --> Run LA
3.	Within LA, Open the workspace whose relative path from this readme file is ..\LA.Workspace.For.Scripts
	(LA menu --> File --> workspace --> open workspace

Build the docs
	From LA run the script @Au docs --> Au docs.  This will run the _Build function.  See the documentation for that function for further details.
	If starting from nothing, need to enable the booleans cookbook, preprocess, docFxBuild, and postprocess.

To use the docs
	Copy the docs created in the previous step to their final destination.  Expected location is \Program files\LibreAutomate\Docs.
	Download the program DocFx.exe which requries the .NET SDK
	Turn on location documentation From LA --> tools --> options.  May need to modify the default location
	