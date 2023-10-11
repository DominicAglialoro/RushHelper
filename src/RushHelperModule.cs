using System;

namespace Celeste.Mod.RushHelper;

public class RushHelperModule : EverestModule {
    public static RushHelperModule Instance { get; private set; }

    public override Type SessionType => typeof(RushHelperSession);
    public static RushHelperSession Session => (RushHelperSession) Instance._Session;

    public RushHelperModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(RushHelperModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(HeavenRushModule), LogLevel.Info);
#endif
    }

    public override void Load() {
        InputExtensions.Load();
        MapMetaExtensions.Load();
        PlayerExtensions.Load();
    }

    public override void Initialize() { }

    public override void Unload() {
        InputExtensions.Unload();
        MapMetaExtensions.Unload();
        PlayerExtensions.Unload();
    }
}