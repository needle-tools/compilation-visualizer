# Changelog
All notable changes to this package will be documented in this file.
The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/) and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.8.4] - 2023-08-22
- catch IOException and log a better message for it
- ensure compatibility with 2023.1

## [1.8.3] - 2022-09-20
- fixed compile errors on <2020.2 and 2022.2+
- added explicit C# language feature tests (experimental)

## [1.8.2] - 2022-07-09
- added Unit Test explanation and howto to the Readme

## [1.8.1] - 2022-06-13
- added Editor tests to check player script compilation on all BuildTargets. 
  Can easily be used by adding it to the `testables` array in `manifest.json`:
  ```
  "testables": [
    "com.needle.compilation-visualizer"
  ]
  ```

## [1.8.0] - 2022-03-04
- added ability to zoom
  - use the range slider or <kbd>Alt + MouseWheel</kbd> to zoom
  - <kbd>Alt + rightclick</kbd> resets zoom
- fixed performance regression when using the Bee backend
- fixed "Compile Player Scripts" on newer Bee versions (different cache structure)

## [1.7.0] - 2021-11-10
- fixed null texture warning in some cases after domain reload
- fixed pid filtering in 2021.2.0f1+ (change in release - no support for 2021.2 beta anymore)
- fixed reload timings incorrectly being changed when reloading assemblies on entering playmode

## [1.6.0] - 2021-07-26
- added ability to compile player scripts and see results from that
- fixed some timings issues with the way Unity reports compilation start/end
- fixed compilation timeline continuing to wait for frames when compilation had errors
- fixed Reload times that were incorrect in some cases

## [1.5.0] - 2021-06-29
- added support for 2021.2.0b1+
- fixed some potential nullrefs when previous compilation trace can't be found

## [1.4.0] - 2021-02-04
- added proper coloring for Light skin support
- changed hacky Bee recompile to new `RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)`
- removed support for 2021.1.0a1–2021.1.0b1 (supported on 0b2+)
- removed result locking support on 2021 for now (as it doesn't currently work due to the compilation changes there)

## [1.3.0-preview] - 2020-12-08
- added experimental support for Unity 2021 (new compilation pipeline)
- added additional timing info on 2021+ for "entire compilation" vs "Csc compilation process"
- added dropdown for opening profiler.json and Chrome Trace visualizer for a more detailled look at compilation data

## [1.2.2] - 2020-10-25
- fixed NullRef with empty iterations data (#3)
- fixed: Recompile can only be pressed when not compiling already

## [1.2.1] - 2020-10-12
- fixed UI freeze when window wasn't opened for a long time
- fixed some warning logs and potential nullrefs

## [1.2.0] - 2020-09-26
- added reload indicator
- added safeguard against Unity crashes that can result in incorrect data being stored
- fixed additional information being logged when selecting entries even when AllowLogging was off

## [1.2.0-preview.2] - 2020-08-28
- added ability to show/hide Assembly reload times
- fixed some timing display issues
- fixed locked window sometimes still getting new data

## [1.2.0-preview] - 2020-08-28
- experimental support for iterative compilation (where compilation happens in multiple passes)
  - seems there are some bugs in Unity around these
  - incorrect compilation end times but correct compilation start times
  - EditorWindows are refreshed on first iteration but subsequent iterations just block
  - full AssemblyReload happens once for each iteration
  - on 2020.2 sometimes there are up to 10 (!) iterations, slowing everything down considerably

## [1.1.2] - 2020-08-22
- fixed logging edge case
- added coloring options

## [1.1.1] - 2020-08-22
- re-added Recompile button for older Unity versions through reflection
- simplified logging of precompiled assembly reference paths

## [1.1.0] - 2020-08-22
- added support for 2018.4, 2019.1, 2019.2
- added compatibility info and a bit more explanation to the Readme
- simplified "Refresh / Auto Refresh" to a single window lock icon consistent with other Unity windows

## [1.0.2] - 2020-08-12
- added brief readme with getting started and screenshots

## [1.0.1] - 2020-08-12
- changed author to "Needle"

## [1.0.0] - 2020-08-10
- initial package version