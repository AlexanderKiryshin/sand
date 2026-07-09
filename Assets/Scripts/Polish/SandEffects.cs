using UnityEngine;

public class SandEffects : MonoBehaviour
{
    [SerializeField] private ParticleSystem collectParticles;

    private SandCollector collector;

    private void Awake()
    {
        Transform legacyParticles = transform.Find("SandCollectParticles");
        if (legacyParticles != null)
        {
            Destroy(legacyParticles.gameObject);
        }

        collector = FindObjectOfType<SandCollector>();
        if (collector == null || collectParticles == null)
        {
            enabled = false;
            return;
        }

        collector.OnGrainCollected += HandleGrainCollected;
    }

    private void OnDestroy()
    {
        if (collector != null)
        {
            collector.OnGrainCollected -= HandleGrainCollected;
        }
    }

    private void HandleGrainCollected(byte colorIndex, int gridX)
    {
        if (collectParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = collectParticles.main;
        LevelLoader loader = FindObjectOfType<LevelLoader>();
        if (loader != null && loader.CurrentLevel != null)
        {
            Color32 color = loader.CurrentLevel.GetPaletteColor(colorIndex);
            main.startColor = new ParticleSystem.MinMaxGradient(color);
        }

        collectParticles.Emit(1);
    }
}
