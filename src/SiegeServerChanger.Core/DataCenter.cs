namespace SiegeServerChanger.Core;

/// <summary>A selectable DataCenterHint value plus a human-readable name.</summary>
public sealed record DataCenter(string Id, string DisplayName)
{
    public const string DefaultId = "default";

    /// <summary>Region code without the provider prefix, e.g. "ap-northeast-2".</summary>
    public string Region => Id.Contains('/') ? Id[(Id.IndexOf('/') + 1)..] : Id;

    public override string ToString() => $"{DisplayName} ({Id})";
}
