using System;
using Monocle;

namespace Celeste.Mod.RushHelper;

[Tracked]
public class UseCardListener : Component {
    public Action<AbilityCardType> OnUseCard;

    public UseCardListener(Action<AbilityCardType> onUseCard) : base(false, false) => OnUseCard = onUseCard;
}