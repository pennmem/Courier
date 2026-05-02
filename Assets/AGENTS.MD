# AGENTS.md

## Project
Unity project being migrated from macOS standalone to WebGL.

## Goal
Make the existing game compile, build, and run as Unity WebGL with minimal behavior changes.

## Rules
- Make small, reviewable changes.
- Do not rewrite unrelated systems.
- Do not edit scenes, prefabs, materials, or ProjectSettings unless needed.
- Preserve existing macOS behavior.
- Prefer platform guards over deleting working code.
- Use `#if UNITY_WEBGL && !UNITY_EDITOR` for WebGL-only code.
- Explain every changed file after edits.

## Unity WebGL constraints
Check for:
- `System.Threading`
- `Thread`
- `Timer`
- `Task.Run`
- raw sockets / `System.Net`
- native plugins
- macOS-specific paths
- direct filesystem assumptions
- AOT/reflection issues
- browser input/audio/fullscreen/cursor-lock issues

## Verification
A task is done only when:
- Unity compiles without new errors.
- WebGL build starts in browser.
- Browser console has no new blocking red errors.
- Gameplay smoke test still passes.