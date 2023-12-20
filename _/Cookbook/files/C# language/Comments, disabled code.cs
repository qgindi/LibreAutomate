/// Comments are used to explain code or temporarily disable code. It can be any text, it isn't executed.

//This is a line comment.
print.it(1); //another comment

/*
This
is a block comment.
*/
print.it(1 /*another comment*/);

//print.it("disabled");
//print.it("code");

/// Two quick ways to convert code to comments and back:
/// 1. Right-click the gray margin at the left. Select several lines if need.
/// 2. Select or click the code, and click toolbar button Comment or Uncomment.

/// <google C# XML documentation comments>XML documentation comments<> start with ///.

/// Comments specific to this program:

/*/ metacomments created by the Properties dialog /*/

//.
print.it("folded code");
print.it("like #region/#endregion");
//..

/// Another way to disable code - <+lang directive #if #else>#if, #endif, #else, #elif and #define<>.

#if true
print.it(1);
// ...
#else
print.it(2);
// ...
#endif
