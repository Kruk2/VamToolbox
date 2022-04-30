namespace VamToolbox.Models;

[Flags]
public enum AssetType
{
    Unknown = 0,

    FemaleNormalMorph = 1 << 0,
    FemaleGenMorph = 1 << 1,
    MaleNormalMorph = 1 << 2,
    MaleGenMorph = 1 << 3,
    FemaleMorph = FemaleNormalMorph | FemaleGenMorph,
    MaleMorph = MaleNormalMorph | MaleGenMorph,
    Morph = FemaleMorph | MaleMorph,

    FemaleHair = 1 << 4,
    MaleHair = 1 << 5,
    Hair = FemaleHair | MaleHair,

    FemaleCloth= 1 << 6,
    MaleCloth = 1 << 7,
    OtherCloth = 1 << 8,
    Cloth = FemaleCloth | MaleCloth | OtherCloth,

    ClothOrHair = Cloth | Hair,
    ClothOrHairOrMorph = Cloth | Hair | Morph,

    Female = FemaleMorph | FemaleCloth | FemaleHair,
    Male = MaleMorph | MaleCloth | MaleHair
}

public static class AssetTypeExtensions
{
    public static bool IsFemale(this AssetType type) => (type & AssetType.Female) != 0;
    public static bool IsMale(this AssetType type) => (type & AssetType.Male) != 0;
}