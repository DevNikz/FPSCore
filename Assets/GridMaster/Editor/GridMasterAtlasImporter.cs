using UnityEditor;

namespace GridMaster.EditorTools
{
    // Forces the correct import settings on the label SDF atlas (linear data, no mips, clamped).
    public class GridMasterAtlasImporter : AssetPostprocessor
    {
        public const string AtlasName = "GridMasterLabelAtlas";

        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains(AtlasName))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.None;
        }
    }
}
