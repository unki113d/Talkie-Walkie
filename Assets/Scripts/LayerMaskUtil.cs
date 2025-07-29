using UnityEngine;

namespace Utils
{
    public static class LayerMaskUtil
    {
        public static bool ContainsLayer(LayerMask layerMask, GameObject gameObject) =>
            (layerMask.value & 1 << gameObject.layer) > 0;
    }
}