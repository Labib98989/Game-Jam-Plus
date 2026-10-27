using UnityEngine;

namespace CoreDump
{
    // The mod types. Each belongs to one family and only chains with its own type.
    // Movement: Power, Recharge. Offense: Damage, Fire Rate, Reload Speed, Recoil. Vision: Wide Lens, Thermal Eye.
    public enum ModType { Power, Recharge, Damage, FireRate, ReloadSpeed, Recoil, WideLens, ThermalEye }

    // A mod boosts every part of its family that touches it. Mods of the same type that touch form
    // a chain, and the longer the chain, the bigger the boost for every part touching any of it.
    // Mods hold other mods (of any type), but no other parts.
    [CreateAssetMenu(menuName = "Core Dump/Parts/Mod")]
    public class ModDefinition : PartDefinition
    {
        public ModType type;
        [Tooltip("What each level of a chain gives. A share for most types (0.25 is 25% more), degrees of view for Wide Lens, meters of sight for Thermal Eye.")]
        public float amountPerLevel = 0.25f;
        [Tooltip("Each extra mod in a chain adds this much of a level less than the one before it.")]
        public float chainFalloff = 0.85f;
        [Tooltip("The highest level a chain can reach.")]
        public float maxLevel = 6f;

        public ModFamily Family => FamilyOf(type);

        public static ModFamily FamilyOf(ModType type) => type switch
        {
            ModType.Power or ModType.Recharge => ModFamily.Movement,
            ModType.WideLens or ModType.ThermalEye => ModFamily.Vision,
            _ => ModFamily.Offense,
        };

        public override PartCategory Category => Family switch
        {
            ModFamily.Movement => PartCategory.MovementMods,
            ModFamily.Vision => PartCategory.VisionMods,
            _ => PartCategory.OffenseMods,
        };

        public override bool CanHold(PartDefinition part) => part is ModDefinition;

        public override string AttachRule => Family == ModFamily.Vision
            ? "Vision mods attach to the body, the head, a connector or another mod"
            : "Mods attach to the body, a connector or another mod";

        public override PartModule AddModule(GameObject host) => host.AddComponent<Mod>();

        // What a chain of this type does, for the build screen. `amount` is what the chain gives.
        public static string Describe(ModType type, float amount) => type switch
        {
            ModType.Power => $"+{amount:P0} power",
            ModType.Recharge => $"+{amount:P0} recharge",
            ModType.Damage => $"+{amount:P0} damage",
            ModType.FireRate => $"+{amount:P0} fire rate",
            ModType.ReloadSpeed => $"+{amount:P0} reload speed",
            ModType.Recoil => $"+{amount:P0} kick",
            ModType.WideLens => $"+{amount:0} degrees of view",
            ModType.ThermalEye => $"sees enemies within {amount:0} m",
            _ => type.ToString(),
        };
    }
}
