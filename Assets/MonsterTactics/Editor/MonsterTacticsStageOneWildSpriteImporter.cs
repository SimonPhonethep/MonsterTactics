using UnityEditor;

namespace MonsterTactics.Editor
{
    /// <summary>Matches the existing Unity sprite import settings and frame animator.</summary>
    public sealed class MonsterTacticsStageOneWildSpriteImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/MonsterTactics/Resources/WildSprites/") ||
                !assetPath.EndsWith(".png")) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 96f;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
