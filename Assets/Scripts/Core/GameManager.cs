using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game state manager. Persists across scenes via DontDestroyOnLoad.
/// Tracks game state, player progress, and coordinates high-level events.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadProgress();
    }

    // ─── Game State ───────────────────────────────────────────────────────────
    public enum GameState { MainMenu, Playing, Paused, GameOver, BossEncounter, AbilityUnlock }

    private GameState _currentState = GameState.MainMenu;
    public GameState CurrentState => _currentState;

    // Fired whenever state changes — UI and systems subscribe to this
    public event Action<GameState> OnGameStateChanged;

    public void SetGameState(GameState newState)
    {
        _currentState = newState;
        OnGameStateChanged?.Invoke(newState);

        // Pause / unpause time with state
        Time.timeScale = newState == GameState.Paused ? 0f : 1f;
    }

    // ─── Player Progress (persisted via PlayerPrefs) ──────────────────────────
    [Header("Progress")]
    public int CurrentWorld    { get; private set; } = 1;
    public int CurrentLevel    { get; private set; } = 1;
    public string LastCheckpointScene  { get; private set; } = "";
    public Vector2 LastCheckpointPos   { get; private set; } = Vector2.zero;

    // Bitmask: bit 0 = WallJump, bit 1 = Dash, bit 2 = DoubleJump
    private int _unlockedAbilityMask = 0;

    public bool HasAbility(int abilityBit) => (_unlockedAbilityMask & (1 << abilityBit)) != 0;

    public void UnlockAbility(int abilityBit)
    {
        _unlockedAbilityMask |= (1 << abilityBit);
        SaveProgress();
        Debug.Log($"[GameManager] Ability unlocked: bit {abilityBit}");
    }

    // ─── Checkpoint ───────────────────────────────────────────────────────────
    public void SetCheckpoint(string sceneName, Vector2 position)
    {
        LastCheckpointScene = sceneName;
        LastCheckpointPos   = position;
        SaveProgress();
        Debug.Log($"[GameManager] Checkpoint saved at {sceneName} {position}");
    }

    public void RespawnAtCheckpoint()
    {
        if (string.IsNullOrEmpty(LastCheckpointScene))
        {
            LoadMainMenu();
            return;
        }
        SetGameState(GameState.Playing);
        SceneLoader.Instance.LoadScene(LastCheckpointScene);
    }

    // ─── Scene Navigation ────────────────────────────────────────────────────
    public void StartGame()
    {
        _unlockedAbilityMask = 0;
        CurrentWorld = 1;
        CurrentLevel = 1;
        SetGameState(GameState.Playing);
        SceneLoader.Instance.LoadScene("World1/Level1_1");
    }

    public void LoadMainMenu()
    {
        SetGameState(GameState.MainMenu);
        Time.timeScale = 1f;
        SceneLoader.Instance.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─── Save / Load ──────────────────────────────────────────────────────────
    private const string KEY_WORLD       = "save_world";
    private const string KEY_LEVEL       = "save_level";
    private const string KEY_ABILITIES   = "save_abilities";
    private const string KEY_CHKPT_SCENE = "save_chkpt_scene";
    private const string KEY_CHKPT_X     = "save_chkpt_x";
    private const string KEY_CHKPT_Y     = "save_chkpt_y";

    public void SaveProgress()
    {
        PlayerPrefs.SetInt(KEY_WORLD,       CurrentWorld);
        PlayerPrefs.SetInt(KEY_LEVEL,       CurrentLevel);
        PlayerPrefs.SetInt(KEY_ABILITIES,   _unlockedAbilityMask);
        PlayerPrefs.SetString(KEY_CHKPT_SCENE, LastCheckpointScene);
        PlayerPrefs.SetFloat(KEY_CHKPT_X,   LastCheckpointPos.x);
        PlayerPrefs.SetFloat(KEY_CHKPT_Y,   LastCheckpointPos.y);
        PlayerPrefs.Save();
    }

    public void LoadProgress()
    {
        CurrentWorld           = PlayerPrefs.GetInt(KEY_WORLD,     1);
        CurrentLevel           = PlayerPrefs.GetInt(KEY_LEVEL,     1);
        _unlockedAbilityMask   = PlayerPrefs.GetInt(KEY_ABILITIES, 0);
        LastCheckpointScene    = PlayerPrefs.GetString(KEY_CHKPT_SCENE, "");
        float cx               = PlayerPrefs.GetFloat(KEY_CHKPT_X, 0f);
        float cy               = PlayerPrefs.GetFloat(KEY_CHKPT_Y, 0f);
        LastCheckpointPos      = new Vector2(cx, cy);
    }

    public void DeleteSave()
    {
        PlayerPrefs.DeleteAll();
        LoadProgress();
    }

    // ─── Pause Toggle (call from PauseMenu button) ────────────────────────────
    public void TogglePause()
    {
        if (_currentState == GameState.Playing)
            SetGameState(GameState.Paused);
        else if (_currentState == GameState.Paused)
            SetGameState(GameState.Playing);
    }
}