using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ProjectBootstrap
{
    static ProjectBootstrap()
    {
        EditorApplication.delayCall += EnsureProjectContent;
    }

    private static void EnsureProjectContent()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string levelPath = "Assets/Levels/Level_01_Heart.asset";
        if (!File.Exists(levelPath))
        {
            SampleLevelGenerator.GenerateHeartLevel();
        }

        if (!File.Exists("Assets/Scenes/GameScene.unity"))
        {
            GameSceneBuilder.BuildGameScene();
        }

        GameSceneBuilder.EnsureConveyorSettingsAsset();
    }
}
