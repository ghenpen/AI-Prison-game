using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class GuardConsoleRunner : MonoBehaviour
{
    private GameManager _game;

    [Header("Test Input")]
    public string testMessage = "";

    void Start()
    {
        _game = new GameManager();

        _game.OnGuardResponse += msg => Log("GUARD", msg);
        _game.OnIntentDetected += i => Log("INTENT", i.ToString());
        _game.OnEmotionsUpdated += emo => Log("EMOTIONS", FormatEmotions(emo));
        _game.OnStateChanged += state => Log("STATE", state.ToString());
        _game.OnDQNAction += action => Log("DQN ACT", action.ToString());
        _game.OnDQNReward += reward => Log("DQN REW", $"{reward:+0.00;-0.00}");
        _game.OnGameWon += () => LogResult(true);
        _game.OnGameLost += () => LogResult(false);

        Debug.Log(Divider());
        Debug.Log($"  GUARD SIMULATION  |  ε={_game.GetEpsilon():F3}  steps={_game.GetTotalSteps()}");
        Debug.Log(Divider());
    }

    void Update()
    {
        if (Keyboard.current.enterKey.wasPressedThisFrame && !string.IsNullOrWhiteSpace(testMessage))
        {
            Debug.Log($"\n  PRISONER  >  {testMessage}");
            _ = SendInput(testMessage);
        }

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            Debug.Log(Divider());
            Debug.Log($"  DQN STATS  |  Steps={_game.GetTotalSteps()}  ε={_game.GetEpsilon():F3}  Loss={_game.GetLastLoss():F4}  Resistance={_game.GetResistance()}");
            Debug.Log(Divider());
        }

        if (Keyboard.current.deleteKey.wasPressedThisFrame)
        {
            string path = System.IO.Path.Combine(
                Application.persistentDataPath, "dqn_guard.json");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
                Debug.Log("  DQN  |  Save deleted — starting fresh next session");
            }
            else
            {
                Debug.Log("  DQN  |  No save file found");
            }
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            _game.ResetGame();
            Debug.Log(Divider());
            Debug.Log("  GAME RESET");
            Debug.Log(Divider());
        }
    }

    public async Task SendInput(string playerInput)
    {
        try
        {
            await _game.ProcessPlayerInput(playerInput);
        }
        catch (Exception e)
        {
            Debug.LogError($"  ERROR  >  {e.Message}\n{e.StackTrace}");
        }
    }

    private void Log(string tag, string message)
    {
        int pad = 10;
        string paddedTag = tag.PadLeft(pad);
        Debug.Log($"  {paddedTag}  |  {message}");
    }

    private void LogResult(bool won)
    {
        Debug.Log(Divider());
        Debug.Log(won
            ? "  RESULT  |  Prisoner convinced the guard. You escaped."
            : "  RESULT  |  Guard called for backup. You failed.");
        Debug.Log($"  DQN  |  Steps={_game.GetTotalSteps()}  ε={_game.GetEpsilon():F3}");
        Debug.Log(Divider());
    }

    private string FormatEmotions(EmotionState emo)
    {
        return $"Suspicion {emo.Suspicion:F0}  " +
               $"Fear {emo.Fear:F0}  " +
               $"Sympathy {emo.Sympathy:F0}  " +
               $"Guilt {emo.Guilt:F0}  " +
               $"Amusement {emo.Amusement:F0}  " +
               $"Respect {emo.Respect:F0}";
    }

    private string Divider() => "  " + new string('-', 60);
}