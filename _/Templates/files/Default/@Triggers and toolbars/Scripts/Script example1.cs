/*/ c ..\Functions\Class1.cs; /*/ //.
script.setup(trayIcon: true);
//..

//This is an example script used in script.run examples somewhere in this project folder.
//It runs in separate process. You can edit and run it without restarting the main script process (triggers and toolbars).
//Scripts don't have to be in this project folder.
//Although this script is in the project folder, it is not part of the project (only the first script is).
//Therefore other files of the project folder are not included in the compilation, and their classes/functions are not available.
//But you can explicitly specify class files. Use the Properties dialog. See the "c ..." in the first line.

dialog.show("trigger action script example");
Class1.Function1();
