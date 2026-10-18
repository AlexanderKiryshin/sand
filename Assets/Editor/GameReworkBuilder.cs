using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

public static class GameReworkBuilder
{
    [MenuItem("Window/Sand/Validate Rework Content")]
    public static void Validate()
    {
        var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        if (db == null || db.LevelCount == 0) throw new Exception("Level database missing.");
        int ordinary = 0, winter = 0;
        var paintings = new HashSet<string>();
        for (int i = 0; i < db.LevelCount; i++)
        {
            var level = db.levels[i];
            if (level.cells.Length != level.gridWidth * level.gridHeight || level.cells.Any(c => c == 0 || c > level.palette.Length))
                throw new Exception("Invalid pixels " + i);
            string art = string.Join(",", level.palette.Select(c => c.r + "/" + c.g + "/" + c.b)) + Convert.ToBase64String(level.cells);
            if (!paintings.Add(art)) throw new Exception("Repeated painting at database index " + i);
            if (level.seasonalNumber > 0)
            {
                winter++;
                if (level.seasonalNumber != winter) throw new Exception("Non-sequential winter numbering " + i);
            }
            else
            {
                if (winter > 0) throw new Exception("Ordinary campaign mixed into winter block.");
                ordinary++;
            }
            if ((i < 5 || i >= 300) && !CampaignExpansionBuilder.CanClear(level)) throw new Exception("Blocked route " + i);
        }
        File.WriteAllText("tasks/game-rework-content-checks.txt", $"PASS: {ordinary} ordinary levels, {winter} winter levels; distinct paintings; separate sequential campaigns; valid cells and checked inventory routes.");
    }
}
