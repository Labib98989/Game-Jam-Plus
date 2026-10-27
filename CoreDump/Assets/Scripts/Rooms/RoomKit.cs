using UnityEngine;

namespace CoreDump
{
    // What rooms are made of. Swap these materials to restyle every room at once.
    [CreateAssetMenu(menuName = "Core Dump/Room Kit")]
    public class RoomKit : ScriptableObject
    {
        [Tooltip("Room pieces have UVs in meters, so these tile by world position and never stretch.")]
        public Material floor;
        public Material wall;
        public Material ramp;
        public Material prop;
        public Material lamp;
        public Material pad;
        [Tooltip("See-through and unlit: markers, and pieces that only sometimes appear, in the room editor.")]
        public Material preview;

        public Material For(KitSlot slot) => slot switch
        {
            KitSlot.Wall => wall,
            KitSlot.Ramp => ramp,
            KitSlot.Prop => prop,
            KitSlot.Lamp => lamp,
            KitSlot.Pad => pad,
            _ => floor,
        };
    }
}
