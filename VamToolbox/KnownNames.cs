using VamToolbox.Models;

namespace VamToolbox;

public static class KnownNames
{
    public static readonly string[] ExtReferencesToPresets = { ".json", ".vap", ".vaj", ".vam" }; // can it even be json?

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
    public static readonly string[] MorphDirs = { FemaleMorphsDir, FemaleGenMorphsDir, MaleMorphsDir, MaleGenMorphsDir };

    public const string FemaleHairDir = "Custom/Hair/Female";
    public const string MaleHairDir = "Custom/Hair/Male";

    public const string FemaleClothDir = "Custom/Clothing/Female";
    public const string MaleClothDir = "Custom/Clothing/Male";
    public const string SharedClothDir = "Custom/Clothing/Shared";
    public const string NeutralClothDir = "Custom/Clothing/Neutral";

    public static bool IsPotentialJsonFile(string ext) => ext is ".json" or ".vap" or ".vaj" or ".uiap";

    public static bool IsFemaleNormalMorph(this string localPath) => localPath.IsInDir(FemaleMorphsDir);
    public static bool IsFemaleGenMorph(this string localPath) => localPath.IsInDir(FemaleGenMorphsDir);
    public static bool IsMaleNormalMorph(this string localPath) => localPath.IsInDir(MaleMorphsDir);
    public static bool IsMaleGenMorph(this string localPath) => localPath.IsInDir(MaleGenMorphsDir);

    public static bool IsFemaleHair(this string localPath) => localPath.IsInDir(FemaleHairDir);
    public static bool IsMaleHair(this string localPath) => localPath.IsInDir(MaleHairDir);
    public static bool IsFemaleCloth(this string localPath) => localPath.IsInDir(FemaleClothDir);
    public static bool IsMaleCloth(this string localPath) => localPath.IsInDir(MaleClothDir);
    public static bool IsOtherCloth(this string localPath) => localPath.IsInDir(SharedClothDir) || localPath.IsInDir(NeutralClothDir);

    private static bool IsInDir(this string localPath, string dir) => localPath.Contains(dir, StringComparison.OrdinalIgnoreCase);

    public static AssetType ClassifyType(this string ext, string localPath)
    {
        AssetType type = AssetType.Unknown;
        if (ext is ".vmi" or ".vmb")
        {
            if (localPath.IsFemaleGenMorph())
                type |= AssetType.FemaleGenMorph;
            else if (localPath.IsMaleGenMorph())
                type |= AssetType.MaleGenMorph;
            else if (localPath.IsFemaleNormalMorph())
                type |= AssetType.FemaleNormalMorph;
            else if (localPath.IsMaleNormalMorph())
                type |= AssetType.MaleNormalMorph;
            else
                type |= AssetType.UnknownMorph;
        }
        else if (ext is ".vaj" or ".vam" or ".vab")
        {
            if (localPath.IsFemaleHair())
                type |= AssetType.FemaleHair;
            else if (localPath.IsMaleHair())
                type |= AssetType.MaleHair;
            else if (localPath.IsFemaleCloth())
                type |= AssetType.FemaleCloth;
            else if (localPath.IsMaleCloth())
                type |= AssetType.MaleCloth;
            else if (localPath.IsOtherCloth())
                type |= AssetType.OtherCloth;
            else
                type |= AssetType.UnknownClothOrHair;
        }

        return type;
    }
}