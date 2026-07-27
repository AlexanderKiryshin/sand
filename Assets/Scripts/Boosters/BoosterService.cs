using System;
using UnityEngine;

public class BoosterService : MonoBehaviour
{
    public static BoosterService instance;

    public event Action OnChargesChanged;
    public event Action<BoosterType> OnBoosterActivated;
    public event Action OnBoosterModeCancelled;

    public BoosterType? ActiveTargetMode { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void SyncUnlocks(int currentLevelIndex)
    {
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
        {
            if (currentLevelIndex < BoosterCatalog.GetUnlockLevelIndex(type))
            {
                continue;
            }

            string grantedKey = BoosterCatalog.GetGrantedKey(type);
            if (GameProgressStore.GetBool(grantedKey))
            {
                continue;
            }

            SetCharges(type, BoosterCatalog.StartCharges);
            GameProgressStore.SetBool(grantedKey, true);
            GameProgressStore.Save();
        }

        OnChargesChanged?.Invoke();
    }

    public bool IsUnlocked(BoosterType type, int currentLevelIndex)
    {
        return currentLevelIndex >= BoosterCatalog.GetUnlockLevelIndex(type);
    }

    public int GetCharges(BoosterType type)
    {
        return GameProgressStore.GetInt(BoosterCatalog.GetSaveKey(type), 0);
    }

    public void GrantCharges(BoosterType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetCharges(type, GetCharges(type) + amount);
        OnChargesChanged?.Invoke();
    }

    public void GrantChargesToAll(int amount)
    {
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
        {
            GrantCharges(type, amount);
        }
    }

    public bool TryConsume(BoosterType type)
    {
        int charges = GetCharges(type);
        if (charges <= 0)
        {
            return false;
        }

        SetCharges(type, charges - 1);
        OnChargesChanged?.Invoke();
        Analytic.BonusUsed(ProgressSaveService.CurrentLevelIndex.ToString(), BoosterCatalog.GetLocalizationKey(type));
        return true;
    }

    public bool IsTutorialSeen(BoosterType type)
    {
        return GameProgressStore.GetBool(BoosterCatalog.GetTutorialSeenKey(type));
    }

    public void MarkTutorialSeen(BoosterType type)
    {
        GameProgressStore.SetBool(BoosterCatalog.GetTutorialSeenKey(type), true);
        GameProgressStore.Save();
    }

    public BoosterType? GetPendingTutorial(int levelIndex)
    {
        foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
        {
            if (BoosterCatalog.GetUnlockLevelIndex(type) == levelIndex && !IsTutorialSeen(type))
            {
                return type;
            }
        }

        return null;
    }

    public void BeginTargetMode(BoosterType type)
    {
        ActiveTargetMode = type;
        OnBoosterActivated?.Invoke(type);
    }

    public void CancelTargetMode()
    {
        if (!ActiveTargetMode.HasValue)
        {
            return;
        }

        ActiveTargetMode = null;
        OnBoosterModeCancelled?.Invoke();
    }

    public void ClearTargetMode()
    {
        ActiveTargetMode = null;
    }

    private static void SetCharges(BoosterType type, int value)
    {
        GameProgressStore.SetInt(BoosterCatalog.GetSaveKey(type), Mathf.Max(0, value));
        GameProgressStore.Save();
    }
}
