<!-- title: netAmermaid --> <!-- of the printed HTML see https://github.com/yzhang-gh/vscode-markdown#print-markdown-to-html -->
# netAmermaid <!-- omit in toc -->

An automated documentation tool for visually exploring
[.NET assemblies](https://learn.microsoft.com/en-us/dotnet/standard/assembly/) (_*.dll_ files)
along type relations using rapid diagramming.

<img src="./html/netAmermaid.ico" align="right" title="" />

<!--TOC-->
- [What can it do for you and how?](#what-can-it-do-for-you-and-how)
- [Check out the demo](#check-out-the-demo)
- [Optimized for exploration and sharing](#optimized-for-exploration-and-sharing)
- [Generate a HTML diagrammer using the console app](#generate-a-html-diagrammer-using-the-console-app)
  - [Manually before use](#manually-before-use)
  - [Automatically](#automatically)
    - [After building](#after-building)
    - [After publishing](#after-publishing)
  - [Options](#options)
  - [Advanced configuration examples](#advanced-configuration-examples)
    - [Shorten member names](#shorten-member-names)
    - [Hide common inheritance noise](#hide-common-inheritance-noise)
    - [Adjust for custom XML docucmentation file names](#adjust-for-custom-xml-docucmentation-file-names)
- [Tips for rendering diagrams using the HTML diagrammer](#tips-for-rendering-diagrams-using-the-html-diagrammer)
- [Disclaimer](#disclaimer)
<!--/TOC-->

# What can it do for you and how?

netAmermaid helps you create meaningful [class diagrams](https://mermaid.js.org/syntax/classDiagram.html) in two simple steps:

1. Point the **command line tool** at an assembly to extract its type information
and **generate a [HTML5](https://en.wikipedia.org/wiki/HTML5#New_APIs) diagramming app** from it.
You can script this step and run it just before using the diagrammer - or
hook it into your build pipeline to automate it for continuous integration.
2. Open the **HTML diagrammer** to select types and **render class diagrams** from them
within a couple of keystrokes - after which you can interact with the diagram directly
to unfold the domain along type raleations. At any point, familiar key commands will copy the diagram to your clipboard
or export it as either SVG or PNG.

If [XML documentation comments are available](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/#create-xml-documentation-output),
they'll be used to annotate types and members on your diagrams.

# Check out the demo

Have a look at the diagrammer generated for [SubTubular](https://github.com/h0lg/SubTubular):
It's got some [type relations](https://raw.githack.com/h0lg/SubTubular/netAmermaid/netAmermaid/class-diagram-generator.html?direction=LR&types=Caption&types=CaptionTrack&types=PaddedMatch&types=PaddedMatch_IncludedMatch&types=Video&types=VideoSearchResult&types=VideoSearchResult_CaptionTrackResult)
and [inheritance](https://raw.githack.com/h0lg/SubTubular/netAmermaid/netAmermaid/class-diagram-generator.html?direction=TB&types=RemoteValidated&types=SearchChannel&types=SearchCommand&types=SearchCommand_Shows&types=SearchPlaylist&types=SearchPlaylistCommand&types=SearchPlaylistCommand_OrderOptions&types=SearchVideos)
going on that offer a good playground.

> Wouldn't it be great to show off netAmermaid's capabilities applied to itself?
Sure - but with the console app being as simple as it is, its class diagrams
are pretty boring and don't get the benefit across.
As with any documentation, netAmermaid becomes more useful with higher complexity.
So you could say it offers little value to itself - 
but it rather likes to call that selfless and feel good about it.

# Optimized for exploration and sharing
It is not the goal of the HTML diagrammer to create the perfect diagram -
so you'll find few options to customize the layout.
This is - to some degree - due to the nature of generative diagramming itself,
while at other times the [mermaid API](https://mermaid.js.org/syntax/classDiagram.html) poses the limiting factor.
Having said that, you can usually find a direction in which the automated layout works reasonably well.

Instead, think of the diagrammer as
- a browser for **exploring domains**
- a visual design aid for **reasoning about type relations and inheritance**
- a **communication tool** for contributors and users to share aspects of a model
- a **documentation** you don't have to write.

You'll find controls and key bindings to help you get those things done as quickly and efficiently as possible.

# Generate a HTML diagrammer using the console app

Once you have an output folder in mind, you can adopt either of the following strategies
to generate a HTML diagrammer from a .Net assembly using the console app.

## Manually before use

**Create the output folder** in your location of choice and inside it **a new shell script**.

Using the CMD shell in a Windows environment for example, you'd create a `regenerate.cmd` looking somewhat like this:

<pre>
..\..\path\to\netAmermaid.exe --assembly ..\path\to\your\assembly.dll --output-folder .
</pre>

With this script in place, run it to (re-)generate the HTML diagrammer at your leisure. Note that `--output-folder .` directs the output to the current directory.

## Automatically

If you want to deploy an up-to-date HTML diagrammer as part of your live documentation,
you'll want to automate its regeneration to keep it in sync with your codebase.

For example, you might like to share the diagrammer on a web server or - in general - with users
who cannot or may not regenerate it; lacking either access to the netAmermaid console app or permission to use it.

In such cases, you can dangle the regeneration off the end of either your build or deployment pipeline.
Note that the macros used here apply to [MSBuild](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild) for [Visual Studio](https://learn.microsoft.com/en-us/visualstudio/ide/reference/pre-build-event-post-build-event-command-line-dialog-box) and your mileage may vary with VS for Mac or VS Code.

### After building

To regenerate the HTML diagrammer from your output assembly after building,
add something like the following to your project file.
Note that the `Condition` here is optional and configures this step to only run after `Release` builds.

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent" Condition="'$(Configuration)' == 'Release'">
  <Exec Command="$(SolutionDir)..\path\to\netAmermaid.exe --assembly $(TargetPath) --output-folder $(ProjectDir)netAmermaid" />
</Target>
```

### After publishing

If you'd rather regenerate the diagram after publishing instead of building, all you have to do is change the `AfterTargets` to `Publish`.
Note that the `Target` `Name` doesn't matter here and that the diagrammer is generated into a folder in the `PublishDir` instead of the `ProjectDir`.

```xml
<Target Name="GenerateHtmlDiagrammer" AfterTargets="Publish">
  <Exec Command="$(SolutionDir)..\path\to\netAmermaid.exe --assembly $(TargetPath) --output-folder $(PublishDir)netAmermaid" />
</Target>
```


## Options

The command line app exposes the following parameters.

| shorthand, name            |                                                                                                                                                                                                                                  |
| :------------------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `-a`, `--assembly`         | Required. The path or file:// URI of the .NET assembly to generate a HTML diagrammer for.                                                                                                                                        |
| `-o`, `--output-folder`    | The path of the folder to generate the HTML diagrammer into.                                                                                                                                                                     |
| `-b`, `--base-types`       | A regular expression matching the names of common base types in the `assembly`. Set to make displaying repetitive and noisy inheritance details on your diagrams optional via a control in the HTML diagrammer.                  |
| `-n`, `--strip-namespaces` | Space-separated namespace names that are removed for brevity when displaying member details. Note that the order matters: e.g. replace 'System.Collections' before 'System' to remove both of them completely.                   |
| `-d`, `--docs`             | The path or file:// URI of the XML file containing the `assembly`'s documentation comments. You only need to set this if a) you want your diagrams annotated with them and b) the file name differs from that of the `assembly`. |


## Advanced configuration examples

Above example shows how the most important options are used. Let's have a quick look at the remaining ones, which allow for customization in your project setup and diagrams.

### Shorten member names

You can reduce the noise in the member lists of classes on your diagrams by supplying a space-separated list of namespaces to omit from the output like so:

<pre>
netAmermaid.exe <b>--strip-namespaces System.Collections.Generic System</b> --assembly ..\path\to\your\assembly.dll --output-folder .
</pre>

Note how `System` is replaced **after** other namespaces starting with `System.` to achieve complete removal.
Otherwise `System.Collections.Generic` wouldn't match the `Collections.Generic` left over after removing `System.`, resulting in partial removal only.

### Hide common inheritance noise

Supply a regular expression matching the names of the common base types in your assembly to make displaying
repetitive and noisy inheritance details on your diagrams optional using a checkbox in the HTML diagrammer.

Let's imagine, for example, an assembly containing the base types `IntegerKeyedEntity` and a `GuidKeyedEntity`
(for entities identified by `int` or `Guid` keys respectively) and that most of the entity types derive from them.
Class diagrams created *to visualize type relations* from that assembly would not only contain relationship arrows between types,
but also one inheritance arrow *for each entity* pointing to its respective base type - creating lots of noise.

To avoid this, you could configure the generation of the diagrammer as follows:

<pre>
netAmermaid.exe <b>--base-types (IntegerKeyedEntity|GuidKeyedEntity)</b> --assembly ..\path\to\your\assembly.dll --output-folder .
</pre>

Alternatively, a regular expression like `\w*KeyedEntity` would work just fine - if you value brevity over readability.

### Adjust for custom XML docucmentation file names

If - for whatever reason - you have customized your XML documentation file output name, you can specify a custom path to pick it up from.

<pre>
netAmermaid.exe <b>--docs ..\path\to\your\docs.xml</b> --assembly ..\path\to\your\assembly.dll --output-folder .
</pre>

# Tips for rendering diagrams using the HTML diagrammer

- The type filter is focused by default. That means you can **immediately start typing**
to select the type you want to use as a starting point for your diagram and **hit Enter to render** it.
- After rendering, you can **explore the domain along type relations** by clicking related types on the diagram to toggle them in the filter and trigger re-rendering.
- The diagram has a **layout direction**, i.e. **rendering depends on the order of your selection**! Use [Alt] + [Arrow Up|Down] to move selected types.
- Did you notice the **key bindings** pointed out in the tooltips? They're trying to help you get stuff done as quickly and efficiently as possible.
- You can **copy and save your diagrams**, but you don't have to; you can just **share the URL**
to your type selection with people having access to the HTML diagrammer.

# Disclaimer

No mermaids were harmed in the writing of this software and you shouldn't interpret the name as inciting capture of or violence against magical creatures.

We would never - [they're doing a great job and we love and respect them for it](https://mermaid.js.org/).