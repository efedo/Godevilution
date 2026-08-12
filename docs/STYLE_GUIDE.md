# Godevilution Style Guide

This guide captures local preferences that are not obvious from the compiler, formatter, or surrounding code.

## General

- Prioritize code that is easy to read: minimal clutter, clear flow, and good logical organization.
- Prefer small, behavior-preserving changes over broad rewrites.
- Match nearby code unless this guide says otherwise.
- Keep names descriptive, but avoid ceremony. Use the shortest name that stays clear at the call site.
- Prefer lines no longer than 120 columns. A little over is acceptable when wrapping would make the code harder to read.
- Use CRLF line endings for project-authored source and documentation unless `.editorconfig` specifies a platform format such as LF for shell scripts, translations, or Unix metadata.

## Comments

- Do not add Doxygen-style annotation for ordinary declarations. Avoid `@brief`, `@param`, and banner comments unless maintaining an existing API block that already uses them.
- Prefer succinct trailing comments where they fit naturally:

  ```cpp
  int remainingFrames = 2; // Skip recovery frames for fast block.
  ```

- If a trailing comment needs one more line, continue it on the following line with indentation that keeps it visually attached:

  ```cpp
  int remainingFrames = 2; // Skip recovery frames for fast block,
                           // preserving vanilla animation timing.
  ```

- Document parameters and return values only when their meaning is not obvious from the signature, name, or surrounding type.
- When a function needs parameter or return-value notes, use plain line comments before the declaration instead of Doxygen tags:

  ```cpp
  // Gets the most valuable item out of all the player's items that match the given predicate.
  // itemPredicate: The predicate used to match the items.
  // return: The most valuable matching item, or nullptr if none was found.
  template <typename TPredicate>
  const Item *GetMostValuableItem(const TPredicate &itemPredicate) const;
  ```

- Use a short leading comment only when the explanation needs more room or applies to several lines.
- When a comment must precede a function or variable because it needs more space than a trailing comment allows, put a blank line before the comment to avoid ambiguity with the previous declaration or statement.
- Comment intent, invariants, compatibility behavior, and surprising constraints. Do not restate the code.
- Keep existing tags such as `BUGFIX`, `CODEFIX`, and `todo` when they are part of the surrounding convention.

## Headers

- Header files should include a short file description at the top.
- Headers should expose the smallest useful interface.
- Prefer declarations in headers and implementations in `.cpp` files.
- As a rule of thumb, only keep functions in headers when the full definition and body fit comfortably on one line.
- Keep tiny accessors inline when they are obvious, single-line, and unlikely to pull extra dependencies into the header.
- Avoid adding includes to headers when a forward declaration or moving implementation to a `.cpp` file is enough.

## Classes And Free Functions

- Put behavior on a class when it primarily operates on that object's state and does not need to coordinate a broader subsystem.
- Keep free functions for lookups, orchestration, multi-object operations, and operations whose natural owner is a module rather than one object.
- When migrating legacy free functions into a class, prefer a compile-preserving mechanical change first, then clean up internals in a follow-up pass.

## API Modernization via Facade

When refactoring a procedural or legacy API that serves a natural module or subsystem:

1. **Create a namespace facade** (`ModuleName::FunctionName`) that wraps the legacy functions.
2. **Improve names** in the facade for clarity and domain intent.
3. **Group related operations** logically (lookup, lifecycle, processing, interaction, etc.).
4. **Keep the old API intact** for backward compatibility; it remains a valid, if discouraged, choice for new code.
5. **Migrate call sites mechanically first**, then improve internals in follow-up passes once confidence is high.

Example: `ObjectManager` wraps the legacy `objects.h` API with a cleaner interface:
- `ObjectManager::FindObject()` instead of `FindObjectAtPosition()`
- `ObjectManager::Operate()` instead of `OperateObject()`
- `ObjectManager::Break()` instead of `BreakObject()`

This approach allows gradual, low-risk migration without requiring a full rewrite.

## Naming

- Prefer project-local naming for existing APIs, but use clearer names when creating new interfaces.
- Avoid encoding implementation details in names unless they are part of a stable domain term.
- Use positive boolean names when practical, such as `isActive`, `canCast`, or `hasNoLife`.

## Tests

- Refactors should be verified with the narrowest affected tests first, then a broader build or test pass when call sites are widespread.
- Add new tests for behavior changes. For pure mechanical refactors, existing behavioral coverage is acceptable if it exercises the affected paths.
