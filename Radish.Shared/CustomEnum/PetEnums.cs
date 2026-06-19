namespace Radish.Shared.CustomEnum;

/// <summary>宠物照顾动作类型</summary>
public static class PetCareActionTypes
{
    public const string Feed = "feed";
    public const string Clean = "clean";
    public const string Play = "play";
    public const string Rest = "rest";

    public static readonly IReadOnlyList<string> All = [Feed, Clean, Play, Rest];
}

/// <summary>宠物心情类型</summary>
public static class PetMoodTypes
{
    public const string Happy = "happy";
    public const string Calm = "calm";
    public const string Tired = "tired";
    public const string Hungry = "hungry";
    public const string Messy = "messy";
}
