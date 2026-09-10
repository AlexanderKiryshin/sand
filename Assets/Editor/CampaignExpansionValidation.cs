using UnityEngine;

public static class CampaignExpansionValidation
{
    public static void BuildAndCheck()
    {
        WebAnalyticsSetup.Configure();
        CampaignAnalyticsChecks.Run();
        CampaignCrateChecks.Run();
        CampaignExpansionBuilder.Build();
        Debug.Log("CAMPAIGN_BUILD_PASS");
        CampaignIterationChecks.Run();
        CampaignSimulationChecks.Run();
        Debug.Log("CAMPAIGN_EXPANSION_ALL_PASS");
    }
    public static void Check()
    {
        CampaignExpansionBuilder.Audit();
        CampaignCrateChecks.Run();
        CampaignAnalyticsChecks.Run();
        CampaignIterationChecks.Run();
        CampaignSimulationChecks.Run();
        Debug.Log("CAMPAIGN_EXPANSION_ALL_PASS");
    }
    public static void ResumePhysics()
    {
        CampaignExpansionBuilder.Audit();
        CampaignLinkedRouteChecks.Run();
        CampaignCrateChecks.Run();
        CampaignIterationChecks.Run();
        CampaignSimulationChecks.RunFrom38();
        Debug.Log("CAMPAIGN_EXPANSION_ALL_PASS");
    }
    public static void RebalanceCratesAndCheck()
    {
        var db = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        foreach (var level in db.levels)
            if (level.campaignTemplateVersion > 0 && level.alternatingCrates)
            {
                level.keyGates = level.linkedBuckets = false;
                UnityEditor.EditorUtility.SetDirty(level);
            }
        UnityEditor.AssetDatabase.SaveAssets();
        CampaignExpansionBuilder.Audit();
        CampaignLinkedRouteChecks.Run();
        CampaignCrateChecks.Run();
        CampaignAnalyticsChecks.Run();
        CampaignFeederQueueChecks.Run();
        CampaignIterationChecks.Run();
        CampaignSimulationChecks.Run();
        Debug.Log("CAMPAIGN_EXPANSION_ALL_PASS");
    }
    public static void VerifyEarlyLevels()
    {
        CampaignFeederQueueChecks.Run();
        CampaignSimulationChecks.RunBefore58();
        Debug.Log("CAMPAIGN_EARLY_LEVELS_PASS");
    }
    public static void CheckAccessFix()
    {
        CampaignExpansionBuilder.Audit();
        CampaignLinkedRouteChecks.Run();
        CampaignCrateChecks.Run();
        CampaignAnalyticsChecks.Run();
        CampaignFeederQueueChecks.Run();
        CampaignIterationChecks.Run();
        CampaignSimulationChecks.RunAfterAccessFix();
        Debug.Log("CAMPAIGN_ACCESS_FIX_PASS");
    }
}
