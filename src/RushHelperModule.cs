using System;

namespace Celeste.Mod.RushHelper;

public class RushHelperModule : EverestModule {
    public static RushHelperModule Instance { get; private set; }

    public override Type SettingsType => typeof(RushHelperSettings);
    public static RushHelperSettings Settings => (RushHelperSettings) Instance._Settings;

    public RushHelperModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(RushHelperModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(RushHelperModule), LogLevel.Info);
#endif
    }

    public override void Load() => PlayerExtensions.Load();

    public override void Unload() => PlayerExtensions.Unload();
}