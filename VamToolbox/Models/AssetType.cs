namespace VamToolbox.Models;

[Flags]
public enum AssetType
{
    Unknown = 0,

    FemaleNormalMorph = 1,
    FemaleGenMorph = 2,
    MaleNormalMorph = 4,
    MaleGenMorph = 8,
    MorphInWrongDirectory = 16,
    FemaleMorph = FemaleNormalMorph | FemaleGenMorph,
    MaleMorph = MaleNormalMorph | MaleGenMorph,
    Morph = FemaleMorph | MaleMorph
}