using UnityEngine;

public class SandEffects : MonoBehaviour
{
    [SerializeField] private ParticleSystem collectParticles;

    private SandCollector collector;

    private void Awake()
    {
        collector = FindObjectOfType<SandCollector>();
        if (collector != null)
        {
            collector.OnGrainCollected += HandleGrainCollected;
        }

        if (collectParticles == null)
        {
            collectParticles = CreateDefaultParticles();
        }
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

        var main = collectParticles.main;
        LevelLoader loader = FindObjectOfType<LevelLoader>();
        if (loader != null && loader.CurrentLevel != null)
        {
            Color32 color = loader.CurrentLevel.GetPaletteColor(colorIndex);
            main.startColor = new ParticleSystem.MinMaxGradient(color);
        }

        collectParticles.Emit(1);
    }

    private ParticleSystem CreateDefaultParticles()
    {
        GameObject particleObject = new GameObject("SandCollectParticles");
        particleObject.transform.SetParent(transform);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();

        var main = particles.main;
        main.startLifetime = 0.35f;
        main.startSpeed = 1.5f;
        main.startSize = 0.08f;
        main.maxParticles = 128;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particles.emission;
        emission.rateOverTime = 0f;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(2f, 0.1f, 0.1f);

        return particles;
    }
}
