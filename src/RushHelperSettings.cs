using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.RushHelper;

public class RushHelperSettings : EverestModuleSettings {
    [DefaultButtonBinding(Buttons.RightShoulder, Keys.Z)]
    public ButtonBinding UseCard { get; set; }
}