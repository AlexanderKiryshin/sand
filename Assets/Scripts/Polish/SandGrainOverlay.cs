using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class SandGrainOverlay : MonoBehaviour
{
    [SerializeField] private RawImage rawImage;
    [SerializeField] [Range(0f, 1f)] private float grainStrength = 0.12f;

    private Texture2D noiseTexture;

    private void Awake()
    {
        if (rawImage == null)
        {
            rawImage = GetComponent<RawImage>();
        }

        noiseTexture = BuildNoiseTexture(64, 64);
        if (rawImage != null)
        {
            rawImage.texture = noiseTexture;
            rawImage.color = new Color(1f, 1f, 1f, grainStrength);
        }
    }

    private static Texture2D BuildNoiseTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte value = (byte)(Random.Range(0, 2) * 255);
                pixels[y * width + x] = new Color32(value, value, value, 255);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
