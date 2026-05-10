using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class GameSceneManager : MonoSingleton<GameSceneManager>
{
    // Scene Names
    public const string SCENE_INTRO = "Intro";
    public const string SCENE_LOBBY = "Lobby";
    public const string SCENE_INGAME = "Ingame";

    private bool m_IsLoadingScene = false;

    public bool IsLoadingScene => m_IsLoadingScene;

    public void LoadScene(string _sceneName)
    {
        if (m_IsLoadingScene)
        {
            GameDebug.LogWarning($"[GameSceneManager] Already loading a scene. Cannot load {_sceneName}");
            return;
        }

        // 호스트/서버 상태에서는 Netcode SceneManager로 로드해야 in-scene NetworkObject가 정상 스폰됨
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(_sceneName, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(_sceneName);
        }
    }

    public void LoadSceneAsync(string _sceneName)
    {
        if (m_IsLoadingScene)
        {
            GameDebug.LogWarning($"[GameSceneManager] Already loading a scene. Cannot load {_sceneName}");
            return;
        }

        StartCoroutine(CoLoadSceneAsync(_sceneName));
    }

    private IEnumerator CoLoadSceneAsync(string _sceneName)
    {
        m_IsLoadingScene = true;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(_sceneName);
        asyncLoad.allowSceneActivation = false;

        // 로딩 진행률 체크
        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        // 씬 활성화
        asyncLoad.allowSceneActivation = true;

        // 씬 로드 완료 대기
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        m_IsLoadingScene = false;
    }
}
