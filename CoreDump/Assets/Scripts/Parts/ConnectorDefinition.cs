using UnityEngine;

namespace CoreDump
{
    [CreateAssetMenu(menuName = "Core Dump/Parts/Connector")]
    public class ConnectorDefinition : PartDefinition
    {
        public override bool CanHold(PartDefinition part) => true;
        public override PartCategory Category => PartCategory.Structure;
        public override PartModule AddModule(GameObject host) => host.AddComponent<Connector>();
    }
}
