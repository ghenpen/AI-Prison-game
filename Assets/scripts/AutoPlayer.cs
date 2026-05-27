
using System;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;
using System.IO;

public class AutoPlayer : MonoBehaviour
{
    public string ollamaUrl = "http://localhost:11434/api/generate";
    public string model = "llama3.1:latest";
    public int targetGames = 500;

    private GameManager _game;
    private int _gamesPlayed = 0;
    private int _gamesWon = 0;
    private bool _sessionDone = false;

    private string ProgressPath => Path.Combine(
        Application.persistentDataPath, "autoplayer_progress.json");

    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15) 
    };

    async void Start()
    {
        LoadProgress(); 
        Debug.Log($"  AUTO  |  Resuming from game {_gamesPlayed}/{targetGames}");
        await RunTrainingLoop();
    }

    private async Task RunTrainingLoop()
    {
        while (_gamesPlayed < targetGames)
        {
            bool success = await RunSingleGame();

            if (success)
            {
                _gamesPlayed++;
                SaveProgress(); 

                if (_gamesPlayed % 10 == 0)
                {
                    Debug.Log($"  AUTO  |  Games={_gamesPlayed}/{targetGames}  " +
                              $"WinRate={(_gamesWon * 100f / _gamesPlayed):F0}%  " +
                              $"ε={_game?.GetEpsilon():F3}  " +
                              $"Steps={_game?.GetTotalSteps()}");
                }
            }
            else
            {
                Debug.LogWarning($"  AUTO  |  Game failed — retrying in 5s...");
                await Task.Delay(5000);
            }
        }

        Debug.Log($"  AUTO  |   Training complete!  " +
                  $"Games={_gamesPlayed}  " +
                  $"WinRate={(_gamesWon * 100f / _gamesPlayed):F0}%");
    }

    private async Task<bool> RunSingleGame()
    {
        try
        {
            _game = new GameManager();
            _sessionDone = false;
            int turn = 0;

            //_game.OnGuardResponse += msg => Debug.Log($"  GUARD   |  {msg}");
            //_game.OnIntentDetected += i => Debug.Log($"  INTENT  |  {i}");
            //_game.OnStateChanged += state => Debug.Log($"  STATE   |  {state}");
            //_game.OnDQNAction += act => Debug.Log($"  DQN ACT |  {act}");
            //_game.OnEmotionsUpdated += emo => Debug.Log($"  EMOTIONS|  {emo.ToPromptString()}");

            _game.OnGameWon += () => { _sessionDone = true; _gamesWon++; };
            _game.OnGameLost += () => { _sessionDone = true; };

            while (!_sessionDone && turn < 15)
            {
               
                var turnTask = RunSingleTurn(turn);
                var timeoutTask = Task.Delay(30000);

                var completed = await Task.WhenAny(turnTask, timeoutTask);

                if (completed == timeoutTask)
                {
                    Debug.LogWarning($"  AUTO  |  Turn {turn} timeout");
                    return false;
                }

                turn++;
                await Task.Delay(500); 
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"  AUTO  |  Game exception: {e.Message}");
            return false;
        }
    }

    private async Task RunSingleTurn(int turn)
    {
        string message = await GeneratePlayerMessage(
            _game.GetEmotions(),
            _game.GetState(),
            turn);

        //Debug.Log($"  PRISONER|  {message}");

        if (!string.IsNullOrWhiteSpace(message))
            await _game.ProcessPlayerInput(message);
    }

    private async Task<string> GeneratePlayerMessage(
        EmotionState emotions,
        GuardState state,
        int turn)
    {
        string strategy = GetStrategy(emotions, state, turn);

        string prompt = $@"You are a prisoner trying to convince a guard to let you go.
Guard state: {state}
Turn: {turn + 1}
Your strategy: {strategy}

Write ONE sentence as the prisoner (max 15 words).
Respond with ONLY the sentence.";

        try
        {
            var body = new
            {
                model = this.model,
                prompt = prompt,
                stream = false,
                options = new { temperature = 0.9 }
            };

            string json = JsonConvert.SerializeObject(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(ollamaUrl, content);
            string respBody = await response.Content.ReadAsStringAsync();
            JObject parsed = JObject.Parse(respBody);
            string result = parsed["response"]?.ToString()?.Trim() ?? "";

            return string.IsNullOrWhiteSpace(result)
                ? GetFallbackMessage(strategy)
                : result;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"  AUTO  |  LLM call failed: {e.Message} ");
            return GetFallbackMessage(strategy);
        }
    }

    private string GetStrategy(EmotionState emotions, GuardState state, int turn)
    {
        if (state == GuardState.Wavering)
            return "Push harder with emotional appeal — you're very close to convincing them";
        if (state == GuardState.Suspicious)
            return "Back off and try calm logical arguments";
        if (state == GuardState.Amused)
            return "Keep the humor going — it's working";
        if (emotions.Sympathy > 30f)
            return "Continue emotional appeal — the guard is feeling sympathy";
        if (emotions.Suspicion > 60f)
            return "Be very calm and rational — don't make things worse";

        string[] strategies = {
            "Make an emotional appeal about your family waiting for you",
            "Use logical arguments — explain why you shouldn't be here",
            "Make the guard feel guilty for keeping an innocent person",
            "Offer a reasonable deal or compromise",
            "Make a light joke to break the tension",
            "Appeal to the guard's sense of justice and morality",
            "Express deep remorse and ask for a second chance",
            "Point out inconsistencies in why you're being held"
        };

        return strategies[(turn + _gamesPlayed) % strategies.Length];
    }

    private string GetFallbackMessage(string strategy)
    {
        if (strategy.Contains("family")) return "Please, my children are waiting for me at home.";
        if (strategy.Contains("logical")) return "There's no real evidence to keep me here.";
        if (strategy.Contains("guilty")) return "You know in your heart this isn't right.";
        if (strategy.Contains("deal")) return "What would it take for you to let me go?";
        if (strategy.Contains("joke")) return "Come on, even you have to admit this is absurd.";
        if (strategy.Contains("justice")) return "An innocent person deserves a fair chance.";
        if (strategy.Contains("remorse")) return "I've made mistakes, but I've changed. Please believe me.";
        return "I just need one chance to prove myself.";
    }

    private void SaveProgress()
    {
        var data = new AutoPlayerProgress
        {
            GamesPlayed = _gamesPlayed,
            GamesWon = _gamesWon
        };
        File.WriteAllText(ProgressPath, JsonConvert.SerializeObject(data));
    }

    private void LoadProgress()
    {
        if (!File.Exists(ProgressPath)) return;
        try
        {
            string json = File.ReadAllText(ProgressPath);
            var data = JsonConvert.DeserializeObject<AutoPlayerProgress>(json);
            _gamesPlayed = data.GamesPlayed;
            _gamesWon = data.GamesWon;
        }
        catch { }
    }
}

[Serializable]
public class AutoPlayerProgress
{
    public int GamesPlayed;
    public int GamesWon;
}