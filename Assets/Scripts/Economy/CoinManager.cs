using System;
using MirraGames.SDK;
using UnityEngine;

public class CoinManager : MonoBehaviour
{
    [SerializeField] private int coinsOnComplete = 50;

    private int coins;
    private int coinsInPreviousLevel;
    private int oneCoinCost;
    private int pendingCoinAdds;
    private int targetCoinsAfterAnimation;
    private bool dataLoaded;
    private int pendingCoinRewards;

    public event Action<int> OnCoinsChanged;
    public static CoinManager instance;

    public int Coins => coins;
    public int CoinsOnComplete => coinsOnComplete;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        TryLoadData();
    }

    private System.Collections.IEnumerator Start()
    {
        yield return new WaitUntil(() => GameProgressStore.IsPreview || GameSdkReadiness.IsReady);
        TryLoadData();

        if (CoinSpawner.Instance != null)
        {
            CoinSpawner.Instance.CoinReachedTarget += AddCoinsFromAnimation;
        }

        TryPlayPendingCoinAnimation();
        OnCoinsChanged?.Invoke(coins);
    }

    private void OnDisable()
    {
        if (CoinSpawner.Instance != null)
        {
            CoinSpawner.Instance.CoinReachedTarget -= AddCoinsFromAnimation;
        }
    }

    public void AddCoinsOnComplete()
    {
        if (!TryLoadData())
        {
            pendingCoinRewards += coinsOnComplete;
            return;
        }
        SnapAnimationIfNeeded();
        coins += coinsOnComplete;
        Analytic.GoldChanged(ProgressSaveService.CurrentLevelIndex.ToString(), coins);
        SaveCoins();
        OnCoinsChanged?.Invoke(coins);
    }

    public void AddCoinsOnDoubleReward()
    {
        if (!TryLoadData())
        {
            pendingCoinRewards += coinsOnComplete * 2;
            return;
        }
        SnapAnimationIfNeeded();
        coins += coinsOnComplete * 2;
        Analytic.GoldChanged(ProgressSaveService.CurrentLevelIndex.ToString(), coins);
        SaveCoins();
        OnCoinsChanged?.Invoke(coins);
    }

    public void AddCoinsOnBuy(int amount)
    {
        if (!TryLoadData())
        {
            pendingCoinRewards += amount;
            return;
        }
        SnapAnimationIfNeeded();
        coins += amount;
        coinsInPreviousLevel = coins;
        Analytic.GoldChanged(ProgressSaveService.CurrentLevelIndex.ToString(), coins);
        SaveCoins();
        OnCoinsChanged?.Invoke(coins);
    }

    public bool TryToSpendCoins(int value)
    {
        if (!TryLoadData()) return false;
        SnapAnimationIfNeeded();
        if (value <= coins)
        {
            coins -= value;
            coinsInPreviousLevel = coins;
            Analytic.GoldChanged(ProgressSaveService.CurrentLevelIndex.ToString(), coins);
            SaveCoins();
            OnCoinsChanged?.Invoke(coins);
            return true;
        }

        return false;
    }

    private void TryPlayPendingCoinAnimation()
    {
        if (coins == 0 || coins <= coinsInPreviousLevel)
        {
            return;
        }

        if (CoinSpawner.Instance == null || CoinSpawner.Instance.coinCount <= 0)
        {
            coinsInPreviousLevel = coins;
            SaveCoins();
            return;
        }

        int delta = coins - coinsInPreviousLevel;
        int coinCount = CoinSpawner.Instance.coinCount;
        oneCoinCost = Mathf.Max(1, delta / coinCount);
        pendingCoinAdds = coinCount;
        targetCoinsAfterAnimation = coins;
        coins = coinsInPreviousLevel;
        CoinSpawner.Instance.SpawnCoins();
        OnCoinsChanged?.Invoke(coins);
    }

    private void AddCoinsFromAnimation()
    {
        if (pendingCoinAdds <= 0)
        {
            return;
        }

        pendingCoinAdds--;
        if (pendingCoinAdds == 0)
        {
            coins = targetCoinsAfterAnimation;
            coinsInPreviousLevel = targetCoinsAfterAnimation;
            SaveCoins();
        }
        else
        {
            coins += oneCoinCost;
        }

        Analytic.GoldChanged(ProgressSaveService.CurrentLevelIndex.ToString(), coins);
        OnCoinsChanged?.Invoke(coins);
    }

    private void SnapAnimationIfNeeded()
    {
        if (pendingCoinAdds <= 0)
        {
            return;
        }

        pendingCoinAdds = 0;
        coins = targetCoinsAfterAnimation;
        coinsInPreviousLevel = targetCoinsAfterAnimation;
        SaveCoins();
        OnCoinsChanged?.Invoke(coins);
    }

    private bool TryLoadData()
    {
        if (dataLoaded) return true;
        // A local fallback read before SDK startup must never become the cloud balance.
        if (!GameProgressStore.IsPreview && !GameSdkReadiness.IsReady) return false;

        LoadDataOrInit();
        dataLoaded = true;
        if (pendingCoinRewards != 0)
        {
            coins += pendingCoinRewards;
            coinsInPreviousLevel = coins;
            pendingCoinRewards = 0;
            SaveCoins();
        }
        return true;
    }

    private void LoadDataOrInit()
    {
        if (GameProgressStore.HasKey("Coins"))
        {
            coins = GameProgressStore.GetInt("Coins");
            coinsInPreviousLevel = GameProgressStore.GetInt("CoinsInPreviosLevel");
        }
        else
        {
            coins = 0;
            coinsInPreviousLevel = 0;
            // As in buses, do not save defaults over a missing/failed cloud read.
        }
    }

    private void SaveCoins()
    {
        GameProgressStore.SetInt("CoinsInPreviosLevel", coinsInPreviousLevel);
        GameProgressStore.SetInt("Coins", coins);
        GameProgressStore.Save();
    }
}
