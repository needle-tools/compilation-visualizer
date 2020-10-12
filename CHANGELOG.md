# Changelog
All notable changes to this package will be documented in this file.
The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/) and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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