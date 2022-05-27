using VamToolbox.Models;

namespace VamToolbox;

public static class KnownNames
{
    public static readonly string[] ExtReferencesToPresets = { ".json", ".vap", ".vaj", ".vam" };
    public static readonly string[] PreviewExtensions = { ".jpg", ".jpeg", ".png" };

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

    public const string FemalePositionsDir = "Custom/Positions/Female";
    public const string MalePositionsDir = "Custom/Positions/Male";
    public const string TogetherPositionsDir = "Custom/Positions/Together";

    // does these have gender?
    public const string GlutePhysicsDir = "Custom/Atom/Person/GlutePhysics";
    public const string BreastPhysicsDir = "Custom/Atom/Person/BreastPhysics";
    public const string PluginsPresetDir = "Custom/PluginPresets";
    public const string AppearancePresetsDir = "Custom/Atom/Person/Appearance";
    public const string ClothingPresetsDir = "Custom/Atom/Person/Clothing";
    public const string HairPresetsDir = "Custom/Atom/Person/Hair";
    public const string PosePresetsDir = "Custom/Atom/Person/Pose";
    public const string SkinPresetsDir = "Custom/Atom/Person/Skin";

    public static bool IsPotentialJsonFile(string ext) => ext is ".json" or ".vap" or ".vaj" or ".uiap";

    public static bool IsOtherCloth(this string localPath) => localPath.IsInDir(SharedClothDir) || localPath.IsInDir(NeutralClothDir);
    private static bool IsInDir(this string localPath, string dir) => localPath.Contains(dir, StringComparison.OrdinalIgnoreCase);

    public static AssetType ClassifyType(this string ext, string localPath)
    {
        var type = AssetType.Unknown;
        if (ext is ".vmi" or ".vmb") {

            if (localPath.IsInDir(FemaleGenMorphsDir))
                type |= AssetType.FemaleGenMorph;
            else if (localPath.IsInDir(FemaleMorphsDir))
                type |= AssetType.FemaleNormalMorph;
            else if (localPath.IsInDir(MaleGenMorphsDir))
                type |= AssetType.MaleGenMorph;
            else if (localPath.IsInDir(MaleMorphsDir))
                type |= AssetType.MaleNormalMorph;
            else
                type |= AssetType.UnknownMorph;
        } else if (ext is ".vaj" or ".vam" or ".vab") {
            if (localPath.IsInDir(FemaleHairDir))
                type |= AssetType.FemaleHair;
            else if (localPath.IsInDir(FemaleClothDir))
                type |= AssetType.FemaleCloth;
            else if (localPath.IsInDir(MaleHairDir))
                type |= AssetType.MaleHair;
            else if (localPath.IsInDir(MaleClothDir))
                type |= AssetType.MaleCloth;
            else if (localPath.IsOtherCloth())
                type |= AssetType.OtherCloth;
            else
                type |= AssetType.UnknownClothOrHair;
        } else if (ext is ".vap") {
            if (localPath.IsInDir(BreastPhysicsDir)) {
                type = AssetType.BreastPhysics;
            } else if (localPath.IsInDir(GlutePhysicsDir)) {
                type = AssetType.GlutePhysics;
            } else if (localPath.IsInDir(PluginsPresetDir)) {
                type = AssetType.PluginsPreset;
            } else if (localPath.IsInDir(FemalePositionsDir)) {
                type = AssetType.FemalePosition;
            } else if (localPath.IsInDir(MalePositionsDir)) {
                type = AssetType.MalePosition;
            } else if (localPath.IsInDir(TogetherPositionsDir)) {
                type = AssetType.TogetherPosition;
            } else if (localPath.IsInDir(AppearancePresetsDir)) {
                type = AssetType.AppearancePreset;
            } else if (localPath.IsInDir(ClothingPresetsDir)) {
                type = AssetType.ClothingPreset;
            } else if (localPath.IsInDir(HairPresetsDir)) {
                type = AssetType.HairPreset;
            } else if (localPath.IsInDir(MorphsDir)) {
                type = AssetType.MorphPreset;
            } else if (localPath.IsInDir(PosePresetsDir)) {
                type = AssetType.PosePreset;
            } else if (localPath.IsInDir(SkinPresetsDir)) {
                type = AssetType.SkinPreset;
            } else if (localPath.IsInDir(FemaleClothDir)) {
                type = AssetType.FemaleClothPreset;
            } else if (localPath.IsInDir(MaleClothDir)) {
                type = AssetType.MaleClothPreset;
            } else if (localPath.IsInDir(FemaleHairDir)) {
                type = AssetType.FemaleHairPreset;
            } else if (localPath.IsInDir(MaleHairDir)) {
                type = AssetType.MaleHairPreset;
            } else {
                // glass preset? interesting
                type = AssetType.Unknown;
            }

        }

        return type;
    }
}