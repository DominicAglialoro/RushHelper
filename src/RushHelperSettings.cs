using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.RushHelper;

public class RushHelperSettings : EverestModuleSettings {
    [DefaultButtonBinding(Buttons.RightShoulder, Keys.V)]
    public ButtonBinding UseCard { get; set; }
}