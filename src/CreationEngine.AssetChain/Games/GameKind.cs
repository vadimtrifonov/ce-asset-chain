using Mutagen.Bethesda;
using CreationEngine.AssetChain.Games.Skyrim;
using CreationEngine.AssetChain.Games.Starfield;

namespace CreationEngine.AssetChain.Games;

internal enum GameKind
{
    SkyrimSE,
    SkyrimVR,
    Starfield
}

internal static class GameSupport
{
    internal static IGameSupport Create(GameKind game) => game switch
    {
        GameKind.SkyrimSE => new SkyrimGame(GameRelease.SkyrimSE),
        GameKind.SkyrimVR => new SkyrimGame(GameRelease.SkyrimVR),
        GameKind.Starfield => new StarfieldGame(),
        _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
    };
}
