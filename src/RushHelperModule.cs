using System;
using Celeste.Mod.ProgHelper;

namespace Celeste.Mod.RushHelper;

public class RushHelperModule : EverestModule {
    public static RushHelperModule Instance { get; private set; }

    public override Type SettingsType => typeof(RushHelperSettings);
    public static RushHelperSettings Settings => (RushHelperSettings) Instance._Settings;

    public bool ProgHelperLoaded { get; private set; }

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

    public override void Load() {
        PlayerExtensions.Load();

        ProgHelperLoaded = Everest.Loader.DependencyLoaded(new EverestModuleMetadata {
            Name = "ProgHelper",
            Version = new Version(1, 0, 0)
        });
    }

    public override void Unload() => PlayerExtensions.Unload();
}