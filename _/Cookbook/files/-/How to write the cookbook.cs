/*
The source of cookbook folders and recipes is workspace "Cookbook" in app folder.
The program presents it as the cookbook tree: folder -> folder, script -> recipe, class -> recipe, others ignored.
	Ignores files and folders if name starts with "-". Files/folders with such names are not added to the program setup file.
When a node is selected in the tree, the program parses the script and presents as a recipe.
A script can contain doc comments and codes.
Doc comments (/// TEXT and /** TEXT) are rendered as text, except if starts with <summary>.
	Can contain <see cref> and Au output tags.
	Must be at the start of a line.
Other code is rendered as code examples. Including //comments.
Below is an example script.
*/

/// A quick brown <b>fox</b> jumped over the lazy <google>dog<>.
/// See also <see cref="print.it"/> and <see cref="Console.Write"/>.
print.it("text");

/// A quick brown fox jumped over the lazy dog.

/// A quick brown fox jumped over the lazy dog.

void Local(int i) {
	print.it(1);
}

class C1 {
	/// This is part of code, not text.
	public int Prop => 0;
}

namespace Na {
	/// This is part of code, not text.
	class C1 {
		/// This is part of code, not text.
		public int Prop => 0;
	}
}

/// A quick brown fox jumped over the lazy dog.