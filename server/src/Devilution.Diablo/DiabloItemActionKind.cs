namespace Devilution.Diablo;

/** Semantic action supplied by a content-owned item, independent of spells. */
public enum DiabloItemActionKind
{
    None,
    Identify,
    ReadBook,
    RestorePartialLife,
    RestoreFullLife,
    RestorePartialMana,
    RestoreFullMana,
    RestorePartialLifeAndMana,
    RestoreFullLifeAndMana,
    IncreaseStrength,
    IncreaseMagic,
    IncreaseDexterity,
    IncreaseVitality,
    IncreaseAllAttributes,
    ApplyBlacksmithOil,
    ApplyAccuracyOil,
    ApplySharpnessOil,
    ApplyFortitudeOil,
    OpenCathedralMap,
    ReadQuestNote,
}
