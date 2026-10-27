using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // Meshes made for a built room, destroyed with it so loading room after room doesn't leak them.
    public class OwnedMeshes : MonoBehaviour
    {
        public List<Mesh> meshes = new();

        void OnDestroy()
        {
            foreach (var mesh in meshes)
                if (mesh) Destroy(mesh);
        }
    }
}
