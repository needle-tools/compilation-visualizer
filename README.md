# Compilation Visualizer for Unity

![Unity Version Compatibility](https://img.shields.io/badge/Unity-2018.4%20%E2%80%94%202020.2-brightgreen) [![openupm](https://img.shields.io/npm/v/com.needle.compilation-visualizer?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.needle.compilation-visualizer/)

## What's this?
This tool visualizes the assembly compilation process in Unity3D. It hooks into the Editor-provided events and nicely draws them on a timeline. That's especially helpful when trying to optimize compile times and dependencies between assemblies.  

Besides showing a graphical view of compilation, selecting an assembly shows both dependencies and dependents of that assembly.  

The screenshots show full compilations; but the timeline works as well for partial compilations (e.g. you changed a single script and Unity only recompiles the relevant parts of the dependency chain).

## Quick Start

Compilation Visualizer is available on OpenUPM: https://openupm.com/packages/com.needle.compilation-visualizer/  

If you're on Unity 2019.4+:
- open `Edit/Project Settings/Package Manager`
- add a new Scoped Registry:
  ```
  Name: OpenUPM
  URL:  https://package.openupm.com/
  Scope(s): com.needle.compilation-visualizer
  ```
- click <kbd>Save</kbd>
- open Package Manager
- click <kbd>+</kbd>
- select <kbd>Add from Git URL</kbd>
- paste `com.needle.compilation-visualizer`
- click <kbd>Add</kbd>.

You can open the **Compilation Visualizer** by selecting `Window > Analysis > Compilation Timeline`.

![Compilation Process](https://github.com/needle-tools/compilation-visualizer/wiki/images/compact-view-recompile.gif)

If you want to trigger a recompile, you can either use the "Recompile" button, or `Right Click > Reimport` a script or folder with scripts to cause that to be recompiled.  

## Screenshots
![Compilation Process](https://github.com/needle-tools/compilation-visualizer/wiki/images/expanded-view-recompile.gif)
![Coloring Options](https://github.com/needle-tools/compilation-visualizer/wiki/images/coloring-options.gif)
![Compact View](https://github.com/needle-tools/compilation-visualizer/wiki/images/compact-view.png)
![Compact View with selected assembly](https://github.com/needle-tools/compilation-visualizer/wiki/images/compact-view-selection.png)
![Expanded View](https://github.com/needle-tools/compilation-visualizer/wiki/images/expanded-view.png)
![Expanded view with selected assembly](https://github.com/needle-tools/compilation-visualizer/wiki/images/expanded-view-selection.png)

## Compatibility to 2018.4, 2019.1, 2019.2
While most functionality works great those versions, some minor things are different:
- slightly less accurate total compilation time on 2018.4 — 2019.1+ has events for the entire compilation while on 2018.4 the last finished assembly compilation is used as end date.
- no PackageInfo for now on 2018.4/2019.1 as `PackageInfo.FindForAsset` doesn't exist.  
_Future Work: there's ways to still find the right package._

## Contact
<b>[needle — tools for unity](https://needle.tools)</b> • 
[@NeedleTools](https://twitter.com/NeedleTools) • 
[@marcel_wiessler](https://twitter.com/marcel_wiessler) • 
[@hybridherbst](https://twitter.com/hybdridherbst)
