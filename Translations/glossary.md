# Translation Glossary

## Format Variables

Source strings use fmt-style replacement fields. Preserve every field and its
type exactly in the translated string.

- `{}` inserts a value with default formatting.
- `{:s}` inserts text.
- `{:d}` inserts a decimal integer.
- `{:+d}` inserts a signed decimal integer.
- Positional fields such as `{0}` and `{1}` identify arguments explicitly and
  may be reordered when the target language requires it.

Do not translate text inside braces, remove fields, add spaces inside a field,
or change its type. Keep non-positional fields in source order. Literal braces
must remain escaped in the same way as the source string.

## Item Names

Affix templates combine base item names with prefixes or suffixes. Languages
may change surrounding words or use positional fields to fit their grammar, but
must preserve both item-name arguments.

## Names and Terminology

Keep established Diablo names when they read naturally in the target language.
Use consistent terms for repeated concepts such as life, mana, recovery,
resistance, and equipment. Prefer the terminology already used by nearby
translated strings.

New source messages must be extracted into the translation catalog; adding an
untracked line to a `.po` file does not create a runtime message.

## References

- [Diablo creatures](https://diablo-archive.fandom.com/wiki/Monsters_(Diablo_I))
- [Diablo unique creatures](https://diablo-archive.fandom.com/wiki/Category:Diablo_I_Unique_Monsters)
- [Hellfire bestiary](https://diablo.fandom.com/wiki/Hellfire_Bestiary)
- [Diablo unique weapons](https://diablo-archive.fandom.com/wiki/Unique_Weapons_(Diablo_I))
- [Diablo normal weapons](https://diablo-archive.fandom.com/wiki/Normal_Weapons_(Diablo_I))
- [Diablo and Hellfire unique armor](https://diablo.fandom.com/wiki/List_of_Unique_Body_Armor_(Diablo_I))
- [Diablo normal armor](https://diablo-archive.fandom.com/wiki/Normal_Armor_(Diablo_I))
- [Diablo and Hellfire unique rings](https://diablo.fandom.com/wiki/List_of_Unique_Rings_(Diablo_I))
- [Diablo and Hellfire unique amulets](https://diablo.fandom.com/wiki/List_of_Unique_Amulets_(Diablo_I))
- [Diablo affixes](https://diablo2.diablowiki.net/D1_Modifiers)
