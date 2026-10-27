using UnityEngine;

namespace CoreDump
{
    [CreateAssetMenu(menuName = "Core Dump/Parts/Head")]
    public class HeadDefinition : PartDefinition
    {
        public override PartCategory Category => PartCategory.Head;
        public override ModFamily TakesMods => ModFamily.Vision;
        public override bool CanHold(PartDefinition part) => part is ModDefinition mod && mod.Family == ModFamily.Vision;
        public override PartModule AddModule(GameObject host) => host.AddComponent<Head>();
    }
}
