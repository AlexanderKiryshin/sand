using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class YoutubeBootstrapScene : MonoBehaviour
{
    private IEnumerator Start()
    {
        if (YoutubePlatform.IsPlayer) yield return new WaitUntil(() => YoutubePlatform.Ready);
        yield return SceneManager.LoadSceneAsync("GameScene");
    }
}
