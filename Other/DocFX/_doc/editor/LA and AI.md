---
uid: ai
---

# LibreAutomate and AI

This article is about LA features that use AI.

## LA documentation search

In the **Help** panel you can use AI-based semantic search to find LA documentation articles. Type a short meaningful search phrase or question in the search field, and press `Enter` or click the **AI search** button. The panel will display a list of LA documentation articles that contain the requested information. The results are much better than a keyword search. The list is ordered by semantic similarity to the search phrase.

The list of results often is not perfect. At the top are the best matches. Items at the bottom may even be not relevant to the search phrase. The tool does not uses slow and expensive AI LLM chat models. It uses an AI embedding model. Normally it takes less than 1 s.

For best results, use a short and meaningful phrase or question, like `paste text` or `how to copy selected text into a variable`. It should contain a *single* task, action or concept.

This feature uses AI models from Voyage, Gemini or Mistral, accessed via web API. They will cost you a few cents per month, depending on usage. Create an account on that AI company's website (Voyage, Google GCP, Mistral), and generate an API key there. In LibreAutomate, in **Options > AI** enter the API key and select an AI model you want to use.

LibreAutomate uses AI models that worked best when testing. But it also allows users to add other models to the list; ask about it in the LA forum or in the Discussions of the GitHub repository.

The tested AI embedding models are listed below, from best to worst (for LA documentation search):
1. Voyage `voyage-4`, Gemini `gemini-embedding-001`, Voyage `voyage-4-large`
1. Gemini `gemini-embedding-2`
1. Voyage `voyage-4-lite`, Mistral `codestral-embed`, Mistral `mistral-embed`, Voyage `voyage-3.5`
1. Cohere `embed-v4.0`, Voyage `voyage-code-4`
1. OpenAI `text-embedding-3-small`

### Improving AI chat answers and generated code

You probably noticed that AI chat bots/apps like ChatGPT, Gemini or Claude usually generate incorrect code when they are asked to use LibreAutomate API. It is because they know too little about the LA API and other LA features. To make AI answers much better, give the AI relevant LA documentation.

When the **Help** panel shows AI search results, the **AI search** button is replaced by button **Copy results for AI chat**. It copies the found articles to the clipboard. Also adds the search phrase and instructions for AI. Paste in an AI chat message (ChatGPT etc).

If your AI chat message is just that text, the AI will assume the search phrase is your question, and will answer it. But the AI answer likely will be better if you write a longer question in the AI chat message, and paste the AI search results below it (or anywhere in the message). You can even paste results of several AI searches in various places in the message.

## MCP server

External AI apps and tools, installed on your computer, can automatically retrieve LA documentation via MCP (Model Context Protocol). Examples: Copilot in Visual Studio, Claude Desktop, n8n. It improves AI chat answers, generated code, AI decisions, etc.

LA has an MCP server that includes tools to get LA documentation. Whenever AI needs information about LA automation API or other features, it calls a tool with a search query (AI-generated). The tool finds relevant LA documentation articles and returns them to the AI. The tool uses the same AI search engine as in the **Help** panel, and the **Options > AI** settings.

LA MCP server details:
- Type: `stdio`.
- Command path: the main LA program file. Usually `C:/Program Files/LibreAutomate/Au.Editor.exe`.
- Command arguments: `/mcp`.

AI apps/tools have a MCP configuration UI or file. Add the LA MCP server there. Example MCP server entry in file `C:\Users\Me\.mcp.json` used by VS Copilot:

```json
    "LibreAutomate": {
      "type": "stdio",
      "command": "C:/Program Files/LibreAutomate/Au.Editor.exe /mcp",
      "disabled": false
    }
```

A MCP client (AI app/tool) runs the program and communicates with it via standard input/output streams. It discovers the tools, and calls them when AI asks.

Tell AI to use the MCP tools. For example add "Use LibreAutomate MCP" in your chat message. Also AI apps like VS Copilot usually have an "AI instructions" file where you can add information about the MCP tools. For example VS Copilot uses file `.github\copilot-instructions.md` in the solution directory. Tell AI to use the file.

<details>
<summary>Example instructions file</summary>

```markdown
# Custom Instructions

## C# Instructions
Applies to: `**/*.cs`

You help LibreAutomate users write C# code and use the app.

Environment: Windows 11, C# 14, .NET 10.

C# code that uses LibreAutomate usually is a script.

Script code should be concise and easy to understand even for non-programmers. Top-level statements; type definitions below. No try/catch at the top level of code.

Prefer LibreAutomate APIs. They're optimized for reliability, simplicity and concise code. Many methods are high-level; just find the right one.

To get information about LibreAutomate API or IDE, use MCP server `LibreAutomate`. You can use it in two ways:
- Call tool `find_la_docs`. It finds and returns the requested information.
- Call tool `get_la_docs_toc` to get the table of contents of the LibreAutomation documentation. Select up to 100 article names, and call tool `get_la_docs` to get the articles.

Don't need `using` directives for LibreAutomate API.

To create WPF windows, use class `wpfBuilder` from LibreAutomate. XAML is used rarely and requires `XamlReader`.

Many LibreAutomate parameter types have implicit conversion operators. Use it to make the code more concise.

LibreAutomate users also can use:
- Libraries from NuGet. Tool to install: menu **Tools > NuGet**.
- Windows API. Add declarations in `unsafe class api : NativeApi { }`.

In your messages prefer ASCII punctuation (`'`, `-`). Don't use emoji in code.

When user asks "do something", usually it actually means "create code to do something", or "explain how to do something".

When user message contains a file path or URL, don't try to load/fetch the file, unless the user asks to do it. The path/URL is likely meant to use in generated code.

In agent mode you may edit files, and build if need. Don't run the program.

```
</details>

To see how AI uses LA MCP tools, check **Options > AI > MCP > Print tool calls**. When a tool is called, it prints the tool name, arguments and results.

Before installing a new LibreAutomate version, close apps that use the MCP server.

## Find icons by name or image

AI also can help you find icons in the **Icons** tool. It can search by name or/and image.

How to use it:
- Type a short search phrase in the search field.
- Or/and copy an icon image (PNG format) to the clipboard. You can find icons on the internet.
- Then click the **AI search** button. After a second it displays icons whose names are similar to the search phrase or/and images are similar to the image in the clipboard.

This feature uses an AI model provided by Voyage (an AI company). You probably already have a Voyage API key and use it for LA documentation search. If not, create a Voyage account, generate an API key, and enter it in **Options > AI**.
