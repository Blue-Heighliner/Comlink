namespace BlueHeighliner.Comlink;

/// <summary>Stands in for the profile type of a configuration whose users have no profile (see <see cref="IEngineBuilder.Types{TFrame, TPriority, TLevel, TAspect, TProfile}"/>). It is never created, so <see cref="UserInfo{TProfile}.Profile"/> is always <see langword="null"/>.</summary>
public sealed class NoProfile;
