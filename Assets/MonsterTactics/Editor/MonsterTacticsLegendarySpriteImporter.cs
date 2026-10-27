using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    public sealed class MonsterTacticsLegendarySpriteImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/MonsterTactics/Resources/LegendarySprites/") ||
                !assetPath.EndsWith(".png")) return;
            TextureImporter ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spritePixelsPerUnit = 96f;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
