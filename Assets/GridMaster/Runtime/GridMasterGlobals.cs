using UnityEngine;

namespace GridMaster
{
    // Drives the scene-wide grid scale used by GridMaster materials (per material: Ignore / Multiply / Override).
    // Add one to the scene. If several exist, the most recently enabled or edited one wins.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Grid Master/Grid Master Globals")]
    public class GridMasterGlobals : MonoBehaviour
    {
        public const string GlobalScaleName = "_GM_GlobalScale";
        private const float MinimumScale = 0.0001f;
        private static readonly int GlobalScaleId = Shader.PropertyToID(GlobalScaleName);

        [SerializeField, Min(MinimumScale)]
        [Tooltip("Multiply mode: scales each material's Cell Size. Override mode: used directly as the cell size in world units.")]
        private float globalScale = 1f;

        public float GlobalScale
        {
            get => globalScale;
            set
            {
                globalScale = Mathf.Max(MinimumScale, value);
                SetGlobalGridScale(globalScale);
            }
        }

        // 0 means "no global scale": materials fall back to their own Cell Size.
        public static void SetGlobalGridScale(float scale)
        {
            Shader.SetGlobalFloat(GlobalScaleId, scale);
        }

        private void OnEnable()
        {
            SetGlobalGridScale(Mathf.Max(MinimumScale, globalScale));
        }

        private void OnValidate()
        {
            globalScale = Mathf.Max(MinimumScale, globalScale);
            if (isActiveAndEnabled)
            {
                SetGlobalGridScale(globalScale);
            }
        }

        private void OnDisable()
        {
            SetGlobalGridScale(0f);
        }
    }
}
