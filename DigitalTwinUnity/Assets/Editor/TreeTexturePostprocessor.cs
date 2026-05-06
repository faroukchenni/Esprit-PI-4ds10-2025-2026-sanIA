using UnityEditor;

public class TreeTexturePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("Gradient Pallete256")) return;
        TextureImporter ti = (TextureImporter)assetImporter;
        ti.filterMode = UnityEngine.FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.anisoLevel = 0;
    }
}
