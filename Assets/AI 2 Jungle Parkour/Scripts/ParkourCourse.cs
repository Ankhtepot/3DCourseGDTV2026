using System.Collections.Generic;
using AI_2_Jungle_Parkour.Scripts;
using DefaultNamespace;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

/// <summary>
/// Runs the jungle parkour course: counts coins, keeps the active checkpoint, respawns the player after a fall,
/// times the run and draws the HUD. R restarts the course from the first checkpoint.
/// </summary>
public class ParkourCourse : MonoBehaviour
{
    // Restarting from far along the course glides the camera faster than a respawn, so it never takes long
    private const float RestartGlideTime = 3f;

    public static ParkourCourse Instance { get; private set; }
    public static CharacterController Player { get; private set; }

    [SerializeField] private ParkourCheckpoint startCheckpoint;
    [Tooltip("Seconds the controls hint stays on screen.")]
    [SerializeField] private float hintDuration = 12f;

    private readonly List<JungleCoin> _coins = new();
    private RespawnTransition _transition;
    private ParkourCheckpoint _checkpoint;
    private float _feetOffset;
    private int _collected;
    private float _startTime;
    private float _finishTime = -1f;
    private string _toast;
    private float _toastUntil;

    private GUIStyle _bigStyle;
    private GUIStyle _smallStyle;
    private GUIStyle _centerStyle;
    private GUIStyle _hintStyle;
    private int _courseGoals;

    private void Awake()
    {
        Instance = this;
        ThirdPersonController controller = FindAnyObjectByType<ThirdPersonController>();
        Player = controller != null ? controller.GetComponent<CharacterController>() : null;
        _transition = GetComponent<RespawnTransition>();
    }

    private void OnEnable()
    {
        EventManager.OnPlayerReachedCourseObjective += CourseObjectiveReached;
        _courseGoals = FindObjectsByType<CourseObjective>().Length;
        Debug.Log($"Course objectives to complete: {_courseGoals}");
    }

    private void Start()
    {
        _coins.AddRange(FindObjectsByType<JungleCoin>());
        if (Player == null)
        {
            Debug.LogError("ParkourCourse needs a player with a ThirdPersonController.");
            enabled = false;
            return;
        }

        // The controller's pivot isn't at the feet, so remember the gap for respawning
        _feetOffset = Player.transform.position.y - Player.bounds.min.y;
        Restart(0f);
    }

    private void Update()
    {
        if (_transition != null && _transition.IsRunning) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart(RestartGlideTime);

        if (_checkpoint != null && Player.enabled && Player.bounds.min.y < _checkpoint.KillY) Respawn(float.PositiveInfinity);
    }

    public void CollectCoin(JungleCoin coin)
    {
        _collected++;
        AudioPool.Play(SoundSynth.Coin(), 0.5f, Random.Range(0.97f, 1.03f));
        if (_collected == _coins.Count) ShowToast("All coins found!", 3f);
    }

    public void ReachCheckpoint(ParkourCheckpoint reached)
    {
        if (reached == _checkpoint) return;
        SetCheckpoint(reached);
        AudioPool.Play(SoundSynth.Checkpoint(), 0.5f);
        ShowToast("Checkpoint", 1.5f);
    }

    public void Finish()
    {
        if (_finishTime >= 0f) return;
        _finishTime = Time.time - _startTime;
        AudioPool.Play(SoundSynth.Fanfare(), 0.6f);
    }

    /// <summary>Resets coins, timer and checkpoints and sends the player back to the start.
    /// A glide time of zero places her there instantly, without the respawn effect.</summary>
    private void Restart(float maxGlideTime)
    {
        foreach (JungleCoin coin in _coins) coin.gameObject.SetActive(true);
        foreach (TreasureChest goal in FindObjectsByType<TreasureChest>()) goal.ResetGoal();
        _collected = 0;
        _finishTime = -1f;
        _startTime = Time.time;
        SetCheckpoint(startCheckpoint);
        Respawn(maxGlideTime);
    }

    private void SetCheckpoint(ParkourCheckpoint next)
    {
        if (_checkpoint != null) _checkpoint.SetLit(false);
        _checkpoint = next;
        if (_checkpoint != null) _checkpoint.SetLit(true);
    }

    private void Respawn(float maxGlideTime)
    {
        if (_checkpoint == null) return;
        Vector3 position = _checkpoint.transform.position + Vector3.up * (_feetOffset + 0.05f);
        Quaternion rotation = _checkpoint.transform.rotation;
        if (_transition != null && maxGlideTime > 0f)
        {
            _transition.Play(Player, position, rotation, maxGlideTime);
            return;
        }

        // A CharacterController overrides teleports while it's enabled
        Player.enabled = false;
        Player.transform.SetPositionAndRotation(position, rotation);
        Player.enabled = true;
        Player.GetComponent<ThirdPersonController>().ResetMotion();
    }

    private void ShowToast(string message, float seconds)
    {
        _toast = message;
        _toastUntil = Time.time + seconds;
    }

    private void OnGUI()
    {
        if (_bigStyle == null)
        {
            _bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, richText = true };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
            _centerStyle = new GUIStyle(_bigStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 40 };
            _hintStyle = new GUIStyle(_smallStyle) { alignment = TextAnchor.MiddleCenter };
        }

        float elapsed = _finishTime >= 0f ? _finishTime : Time.time - _startTime;
        ShadowLabel(new Rect(24, 18, 400, 40), $"Coins  {_collected} / {_coins.Count}", _bigStyle, new Color(1f, 0.85f, 0.35f));
        ShadowLabel(new Rect(24, 58, 400, 30), FormatTime(elapsed), _smallStyle, Color.white);

        if (_finishTime >= 0f)
        {
            string result = $"Treasure found!\n{_collected} / {_coins.Count} coins in {FormatTime(_finishTime)}\n<size=22>Press R to run it again</size>";
            ShadowLabel(new Rect(0, Screen.height * 0.3f, Screen.width, 160), result, _centerStyle, new Color(1f, 0.85f, 0.35f));
        }
        else if (Time.time < _toastUntil)
        {
            ShadowLabel(new Rect(0, Screen.height * 0.22f, Screen.width, 60), _toast, _centerStyle, Color.white);
        }

        if (Time.time - _startTime < hintDuration && _finishTime < 0f)
        {
            ShadowLabel(new Rect(0, Screen.height - 60, Screen.width, 30),
                "WASD move  ·  Caps Lock run  ·  Shift sprint  ·  Space jump  ·  R restart", _hintStyle, Color.white);
        }
    }

    private static void ShadowLabel(Rect rect, string text, GUIStyle style, Color color)
    {
        style.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, style);
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }

    private static string FormatTime(float seconds) => $"{(int)(seconds / 60f)}:{seconds % 60f:00.0}";
    
    private void CourseObjectiveReached()
    {
        _courseGoals -= 1;
        if (_courseGoals <= 0)
        {
            Finish();
        }
    }

    private void OnDisable()
    {
        EventManager.OnPlayerReachedCourseObjective -= CourseObjectiveReached;
    }
}
