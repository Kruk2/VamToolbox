namespace VamToolbox;

public static class KnownNames
{
    public static readonly string[] ExtReferencesToPresets = {".json", ".vap", ".vaj", ".vam"}; // can it even be vab or json?

    public static readonly string[] KnownVamDirs = {
        "Custom",

        "Custom/Assets",

        "Custom/Atom",
        "Custom/Atom/Person",
        MorphsDir,
        FemaleMorphsDir,
        FemaleGenMorphsDir,
        MaleMorphsDir,
        MaleGenMorphsDir,

        "Custom/Atom/Person/Textures",
        "Custom/Atom/Person/Textures/FemaleBase",
        "Custom/Atom/Person/Textures/MaleBase",

        "Custom/Hair",
        "Custom/Hair/Female",
        "Custom/Hair/Male",

        "Custom/Scripts"
    };

    public const string MorphsDir = "Custom/Atom/Person/Morphs";
    public const string FemaleMorphsDir = "Custom/Atom/Person/Morphs/female";
    public const string FemaleGenMorphsDir = "Custom/Atom/Person/Morphs/female_genitalia";
    public const string MaleMorphsDir = "Custom/Atom/Person/Morphs/male";
    public const string MaleGenMorphsDir = "Custom/Atom/Person/Morphs/male_genitalia";
    public static readonly string[] MorphDirs = {FemaleMorphsDir, FemaleGenMorphsDir, MaleMorphsDir, MaleGenMorphsDir};

    private const string FemaleHairDir = "Custom/Hair/Female";
    private const string MaleHairDir = "Custom/Hair/Male";
    public static readonly string[] HairDirs = {FemaleHairDir, MaleHairDir};

    private const string FemaleClothDir = "Custom/Clothing/Female";
    private const string MaleClothDir = "Custom/Clothing/Male";
    public static readonly string[] ClothDirs = {FemaleClothDir, MaleClothDir};

    public static readonly string[] HairClothDirs = {FemaleHairDir, MaleHairDir, FemaleClothDir, MaleClothDir};

    public static bool IsPotentialJsonFile(string ext) => ext is ".json" or ".vap" or ".vaj";


    public static bool IsFemaleNormalMorph(this string localPath) => localPath.IsInDir(FemaleMorphsDir);
    public static bool IsFemaleGenMorph(this string localPath) => localPath.IsInDir(FemaleGenMorphsDir);

    public static bool IsMaleNormalMorph(this string localPath) => localPath.IsInDir(MaleMorphsDir);
    public static bool IsMaleGenMorph(this string localPath) => localPath.IsInDir(MaleGenMorphsDir);

    private static bool IsInDir(this string localPath, string dir) => localPath.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
}