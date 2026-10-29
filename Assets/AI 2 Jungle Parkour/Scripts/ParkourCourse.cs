using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private readonly List<JungleCoin> coins = new List<JungleCoin>();
    private RespawnTransition transition;
    private ParkourCheckpoint checkpoint;
    private float feetOffset;
    private int collected;
    private float startTime;
    private float finishTime = -1f;
    private string toast;
    private float toastUntil;

    private GUIStyle bigStyle;
    private GUIStyle smallStyle;
    private GUIStyle centerStyle;
    private GUIStyle hintStyle;

    private void Awake()
    {
        Instance = this;
        var controller = FindFirstObjectByType<ThirdPersonController>();
        Player = controller != null ? controller.GetComponent<CharacterController>() : null;
        transition = GetComponent<RespawnTransition>();
    }

    private void Start()
    {
        coins.AddRange(FindObjectsByType<JungleCoin>(FindObjectsSortMode.None));
        if (Player == null)
        {
            Debug.LogError("ParkourCourse needs a player with a ThirdPersonController.");
            enabled = false;
            return;
        }

        // The controller's pivot isn't at the feet, so remember the gap for respawning
        feetOffset = Player.transform.position.y - Player.bounds.min.y;
        Restart(0f);
    }

    private void Update()
    {
        if (transition != null && transition.IsRunning) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart(RestartGlideTime);

        if (checkpoint != null && Player.enabled && Player.bounds.min.y < checkpoint.KillY) Respawn(float.PositiveInfinity);
    }

    public void CollectCoin(JungleCoin coin)
    {
        collected++;
        AudioPool.Play(SoundSynth.Coin(), 0.5f, Random.Range(0.97f, 1.03f));
        if (collected == coins.Count) ShowToast("All coins found!", 3f);
    }

    public void ReachCheckpoint(ParkourCheckpoint reached)
    {
        if (reached == checkpoint) return;
        SetCheckpoint(reached);
        AudioPool.Play(SoundSynth.Checkpoint(), 0.5f);
        ShowToast("Checkpoint", 1.5f);
    }

    public void Finish()
    {
        if (finishTime >= 0f) return;
        finishTime = Time.time - startTime;
        AudioPool.Play(SoundSynth.Fanfare(), 0.6f);
    }

    /// <summary>Resets coins, timer and checkpoints and sends the player back to the start.
    /// A glide time of zero places her there instantly, without the respawn effect.</summary>
    private void Restart(float maxGlideTime)
    {
        foreach (JungleCoin coin in coins) coin.gameObject.SetActive(true);
        foreach (TreasureGoal goal in FindObjectsByType<TreasureGoal>(FindObjectsSortMode.None)) goal.ResetGoal();
        collected = 0;
        finishTime = -1f;
        startTime = Time.time;
        SetCheckpoint(startCheckpoint);
        Respawn(maxGlideTime);
    }

    private void SetCheckpoint(ParkourCheckpoint next)
    {
        if (checkpoint != null) checkpoint.SetLit(false);
        checkpoint = next;
        if (checkpoint != null) checkpoint.SetLit(true);
    }

    private void Respawn(float maxGlideTime)
    {
        if (checkpoint == null) return;
        Vector3 position = checkpoint.transform.position + Vector3.up * (feetOffset + 0.05f);
        Quaternion rotation = checkpoint.transform.rotation;
        if (transition != null && maxGlideTime > 0f)
        {
            transition.Play(Player, position, rotation, maxGlideTime);
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
        toast = message;
        toastUntil = Time.time + seconds;
    }

    private void OnGUI()
    {
        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, richText = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
            centerStyle = new GUIStyle(bigStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 40 };
            hintStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
        }

        float elapsed = finishTime >= 0f ? finishTime : Time.time - startTime;
        ShadowLabel(new Rect(24, 18, 400, 40), $"Coins  {collected} / {coins.Count}", bigStyle, new Color(1f, 0.85f, 0.35f));
        ShadowLabel(new Rect(24, 58, 400, 30), FormatTime(elapsed), smallStyle, Color.white);

        if (finishTime >= 0f)
        {
            string result = $"Treasure found!\n{collected} / {coins.Count} coins in {FormatTime(finishTime)}\n<size=22>Press R to run it again</size>";
            ShadowLabel(new Rect(0, Screen.height * 0.3f, Screen.width, 160), result, centerStyle, new Color(1f, 0.85f, 0.35f));
        }
        else if (Time.time < toastUntil)
        {
            ShadowLabel(new Rect(0, Screen.height * 0.22f, Screen.width, 60), toast, centerStyle, Color.white);
        }

        if (Time.time - startTime < hintDuration && finishTime < 0f)
        {
            ShadowLabel(new Rect(0, Screen.height - 60, Screen.width, 30),
                "WASD move  ·  Caps Lock run  ·  Shift sprint  ·  Space jump  ·  R restart", hintStyle, Color.white);
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
}
