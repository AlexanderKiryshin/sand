using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    [SerializeField] private LevelDatabase levelDatabase;
    [SerializeField] private FallingSandSimulator sandSimulator;
    [SerializeField] private SandRenderer sandRenderer;
    [SerializeField] private SandCollector sandCollector;
    [SerializeField] private ConveyorBelt conveyorBelt;

    private SandGrid currentGrid;
    private LevelData currentLevel;

    public LevelData CurrentLevel { get { return currentLevel; } }
    public SandGrid CurrentGrid { get { return currentGrid; } }

    public LevelData LoadLevel(int index)
    {
        if (levelDatabase == null)
        {
            Debug.LogError("LevelDatabase is not assigned.");
            return null;
        }

        currentLevel = levelDatabase.GetLevel(index);
        if (currentLevel == null)
        {
            Debug.LogError("Level not found at index " + index);
            return null;
        }

        currentGrid = new SandGrid(currentLevel, sandRenderer != null ? sandRenderer.SubGrainsPerAxis : 1);
        float grainStrength = SandGrainColorBaker.GrainNoiseEnabled && sandRenderer != null
            ? sandRenderer.GrainStrength
            : 0f;
        currentGrid.BakeDisplayColors(grainStrength);

        if (sandRenderer != null)
        {
            sandRenderer.Initialize(currentGrid);
        }

        if (sandSimulator != null)
        {
            sandSimulator.Initialize(currentGrid, sandCollector);
        }

        if (conveyorBelt != null)
        {
            conveyorBelt.ResetConveyor();
        }

        if (sandSimulator != null)
        {
            sandSimulator.Wake();
        }

        return currentLevel;
    }
}
