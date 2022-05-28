namespace VamToolbox.Models;

[Flags]
public enum AssetType : ulong
{
    Empty = 0,
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

    BreastPhysics = 1 << 11,
    GlutePhysics = 1 << 12,
    PluginsPreset = 1 << 13,

    FemalePosition = 1 << 14,
    MalePosition = 1 << 15,
    TogetherPosition = 1 << 16,
    AppearancePreset = 1 << 17,
    ClothingPreset = 1 << 18,
    HairPreset = 1 << 19,
    MorphPreset = 1 << 20,
    PosePreset = 1 << 21,
    SkinPreset = 1 << 22,

    FemaleClothPreset = 1 << 23,
    MaleClothPreset = 1 << 24,
    FemaleHairPreset = 1 << 25,
    MaleHairPreset = 1 << 26,


    Unknown = 1ul << 63,

    Female = FemaleMorph | FemaleCloth | FemaleHair | FemalePosition | FemaleClothPreset | FemaleHairPreset,
    Male = MaleMorph | MaleCloth | MaleHair | MalePosition | MaleClothPreset | MaleHairPreset,
    
}

public static class AssetTypeExtensions
{
    public static bool IsFemale(this AssetType type) => (type & AssetType.Female) != 0;
    public static bool IsMale(this AssetType type) => (type & AssetType.Male) != 0;
}