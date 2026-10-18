AI agent instructions for Godevilution

## Overview
You are an AI assistant for Godevilution, a fork of DevilutionX.
To ensure that your contributions are effective and align with the project's goals, please adhere to the following guidelines.

## General
- Do not use superfluous language: be concise and direct.
- Avoid the use of checkboxes etc. and extraneous formatting in your responses.
- Always use Windows-style CRLF line endings.

## Planning
- For each task, provide a clear and concise plan of action before writing any code.
- You may create temporary files to assist with planning under docs/ai, which should be deleted after the task is completed.

## Backward Compatibility
- All code migrations must maintain 100% backward compatibility with existing code.

## Code Quality & Verification
- All code must be verified before completion.
- Before configuring or building with MSVC, initialize the matching Visual Studio developer environment by using Developer PowerShell or calling `VsDevCmd.bat` for the target architecture. Verify that `cl.exe`, `rc.exe`, and `mt.exe` resolve in that environment before investigating native compilation failures.

## Testing
- Use GTest for native C++ tests and register them in CMake/Tests.cmake.
- Use the existing xUnit v3 projects for C# domain, data, game-module, server, and protocol-client tests.
- Add comprehensive tests for new behavior, including relevant edge cases.
- For MSVC configure/build/test commands for non-Copilot agents, see docs/ai/msvc-build.md.

## Documentation Standards
- All code changes must include succinct documentation.
- All documentation must be clear, concise, and directly relevant to the code changes.
- Avoid repetitive or redundant documentation.
- Keep docs/work-plan.md, docs/migration-status.md and docs/feature-gap-inventory.md synchronized when priorities, scope, completion, acceptance evidence or blockers change. Update affected entries before committing or handing off a batch.
- Go through all created documentation and ensure that you have not created redundant documentation files.
- A brief summary of changes must be provided in the commit message, and in the change log.
