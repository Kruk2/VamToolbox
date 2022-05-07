namespace VamToolbox.Models;

[Flags]
public enum AssetType
{
    Unknown = 0,

    FemaleNormalMorph = 1 << 0,
    FemaleGenMorph = 1 << 1,
    MaleNormalMorph = 1 << 2,
    MaleGenMorph = 1 << 3,
    UnknownMorph = 1 << 4,
    FemaleMorph = FemaleNormalMorph | FemaleGenMorph,
    MaleMorph = MaleNormalMorph | MaleGenMorph,
    ValidMorph = FemaleMorph | MaleMorph,
    Morph = FemaleMorph | MaleMorph | UnknownMorph,

    FemaleHair = 1 << 5,
    MaleHair = 1 << 6,
    ValidHair = FemaleHair | MaleHair,

    FemaleCloth = 1 << 7,
    MaleCloth = 1 << 8,
    OtherCloth = 1 << 9,
    ValidCloth = FemaleCloth | MaleCloth | OtherCloth,

    UnknownClothOrHair = 1 << 10,
    ValidClothOrHair = ValidCloth | ValidHair,
    ValidClothOrHairOrMorph = ValidCloth | ValidHair | ValidMorph,

    Female = FemaleMorph | FemaleCloth | FemaleHair,
    Male = MaleMorph | MaleCloth | MaleHair
}

public static class AssetTypeExtensions
{
    public static bool IsFemale(this AssetType type) => (type & AssetType.Female) != 0;
    public static bool IsMale(this AssetType type) => (type & AssetType.Male) != 0;
}