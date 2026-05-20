using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles all scene loading with a fade-to-black transition.
/// Persists across scenes. Attach to the same root GameObject as GameManager.
///
/// Setup: This script auto-creates its own Canvas + Image overlay at runtime.
/// No prefab setup needed — just attach this script and call SceneLoader.Instance.LoadScene("SceneName").
/// </summary>
public class SceneLoader : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static SceneLoader Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildFadeOverlay();
    }

    // ─── Settings ────────────────────────────────────────────────────────────
    [Header("Fade Settings")]
    [SerializeField] float _fadeDuration = 0.35f;
    [SerializeField] Color _fadeColor    = Color.black;

    // ─── Internal Overlay ────────────────────────────────────────────────────
    private Canvas    _canvas;
    private Image     _overlay;
    private bool      _isLoading = false;

    private void BuildFadeOverlay()
    {
        var go = new GameObject("FadeCanvas");
        go.transform.SetParent(transform);
        DontDestroyOnLoad(go);

        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode      = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder    = 999;  // always on top
        go.AddComponent<UnityEngine.UI.CanvasScaler>();
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var imgGo = new GameObject("Overlay");
        imgGo.transform.SetParent(_canvas.transform, false);

        _overlay = imgGo.AddComponent<Image>();
        _overlay.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, 0f);

        // Stretch to fill
        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ─── Public API ───────────────────────────────────────────────────────────
    /// <summary>Load a scene by name with fade transition.</summary>
    public void LoadScene(string sceneName)
    {
        if (_isLoading) return;
        StartCoroutine(FadeAndLoad(sceneName));
    }

    /// <summary>Reload the currently active scene (e.g. after death).</summary>
    public void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    // ─── Fade Coroutine ───────────────────────────────────────────────────────
    private IEnumerator FadeAndLoad(string sceneName)
    {
        _isLoading = true;

        // Fade OUT (transparent → black)
        yield return StartCoroutine(Fade(0f, 1f));

        // Load scene async
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone)
            yield return null;

        // Small hold so the scene's Awake/Start can run before reveal
        yield return new WaitForSecondsRealtime(0.05f);

        // Fade IN (black → transparent)
        yield return StartCoroutine(Fade(1f, 0f));

        _isLoading = false;
    }

    private IEnumerator Fade(float fromAlpha, float toAlpha)
    {
        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeDuration);
            _overlay.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }
        _overlay.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, toAlpha);
    }

    // ─── Convenience: fade in on scene start ────────────────────────────────
    /// <summary>
    /// Call this from any scene's Start() if you want a fade-in without loading.
    /// Useful for the Main Menu or first scene.
    /// </summary>
    public void FadeInCurrentScene()
    {
        if (_isLoading) return;
        StartCoroutine(Fade(1f, 0f));
    }
}