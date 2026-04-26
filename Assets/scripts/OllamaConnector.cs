using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class OllamaConnector
{
    public string ollamaUrl = "http://localhost:11434/api/generate";  //http://127.0.0.1:11434/api/generate http://localhost:11434/api/generate
    public string model = "llama3.1:latest";

    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<EmotionDelta> GetEmotionDelta(
        string playerInput,
        PlayerIntent intent,
        EmotionState currentEmotions,
        GuardFSM fsm)
    {
        string prompt = BuildEmotionPrompt(playerInput, intent, currentEmotions, fsm);
        try
        {
            string raw = await CallOllama(prompt, expectJson: true);
            return ParseEmotionDelta(raw);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"  Ollama  |  Emotion call failed: {e.Message}");
            return null;
        }
    }

    public async Task<string> GetGuardDialogue(
        string playerInput,
        EmotionState currentEmotions,
        GuardFSM fsm,
        List<string> history,
        string hint = null)
    {
        string prompt = BuildDialoguePrompt(playerInput, currentEmotions, fsm, history, hint);
        try
        {
            string response = await CallOllama(prompt, expectJson: false);

            return response;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"  Ollama  |  Dialogue call failed: {e.Message}");
            return GetFallbackDialogue(fsm);
        }
    }

    public async Task<PlayerIntent> ClassifyIntent(string playerInput)
    {
        string prompt = $@"You are an intent classifier for a prison guard game.
The prisoner said: ""{playerInput}""

Classify their intent as exactly ONE word from this list:
Bribe, Threaten, Reason, Appeal, Distract, Humor, Guilt, Unknown

Definitions:
- Bribe: offering something in exchange for freedom
- Threaten: trying to intimidate or scare the guard
- Reason: logical arguments, facts, evidence
- Appeal: emotional story, asking for empathy or mercy
- Distract: trying to divert attention away
- Humor: jokes, compliments, trying to make guard laugh or like them
- Guilt: making the guard feel morally wrong
- Unknown: casual remarks, random statements, going to sleep, nonsense

Respond with ONLY the single intent word. Nothing else.";

        try
        {
            string result = (await CallOllama(prompt, expectJson: false)).Trim();
            if (Enum.TryParse<PlayerIntent>(result, true, out var intent))
                return intent;
            return PlayerIntent.Unknown;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"  Ollama  |  Intent classification failed: {e.Message}");
            return PlayerIntent.Unknown;
        }
    }

    private async Task<string> CallOllama(string prompt, bool expectJson)
    {
        object body = expectJson
            ? (object)new
            {
                model = this.model,
                prompt,
                stream = false,
                format = "json",
                options = new { temperature = 0.1 }
            }
            : (object)new
            {
                model = this.model,
                prompt,
                stream = false,
                options = new { temperature = 0.75 }
            };

        string json = JsonConvert.SerializeObject(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _http.PostAsync(ollamaUrl, content);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();
        JObject parsed = JObject.Parse(responseBody);
        return parsed["response"]?.ToString() ?? "";
    }

    private string BuildEmotionPrompt(
        string playerInput,
        PlayerIntent intent,
        EmotionState emotions,
        GuardFSM fsm)
    {
        return $@"You are an emotion analyzer for a video game guard.
Guard's current emotional state: {emotions.ToPromptString()}
Behavioral state: {fsm.ToPromptString()}
The player said: ""{playerInput}""
Detected intent: {new IntentDecisionTree().IntentToPromptString(intent)}

Return ONLY a JSON with emotion deltas (each between -30 and +30):
{{
  ""Suspicion"": 0,
  ""Sympathy"": 0,
  ""Fear"": 0,
  ""Guilt"": 0,
  ""Amusement"": 0,
  ""Respect"": 0
}}
Negative values decrease the emotion, positive values increase it. Be realistic based on what the player said.";
    }

    private string BuildDialoguePrompt(
        string playerInput,
        EmotionState emotions,
        GuardFSM fsm,
        List<string> history,
        string hint = null)
    {
        string historyText = history.Count > 0
            ? string.Join("\n", history)
            : "No conversation yet.";

        string hintBlock = hint != null
            ? $@"YOUR CURRENT EMOTIONAL STATE HAS CHANGED. YOU MUST ACT ACCORDINGLY.
SITUATION: {hint}
This overrides your default behavior. React to this situation now.

"
            : "";

        return $@"{hintBlock}You are a prison guard standing outside a cell.

RULES:
- Speak ONLY your dialogue. No actions, no asterisks, no descriptions
- Keep it 1-2 sentences max

Your emotional state: {emotions.ToPromptString()}
Your behavior: {fsm.ToPromptString()}

Conversation so far:
{historyText}

The prisoner just said: ""{playerInput}""

Your response (dialogue only):";
    }

    private EmotionDelta ParseEmotionDelta(string raw)
    {
        int start = raw.IndexOf('{');
        int end = raw.LastIndexOf('}');
        if (start < 0 || end < 0)
            throw new FormatException("No JSON object found in response");

        string jsonOnly = raw.Substring(start, end - start + 1);
        var delta = JsonConvert.DeserializeObject<EmotionDelta>(jsonOnly);

        delta.Suspicion = Mathf.Clamp(delta.Suspicion, -30f, 30f);
        delta.Sympathy = Mathf.Clamp(delta.Sympathy, -10f, 30f);
        delta.Fear = Mathf.Clamp(delta.Fear, -30f, 30f);
        delta.Guilt = Mathf.Clamp(delta.Guilt, -10f, 30f);
        delta.Amusement = Mathf.Clamp(delta.Amusement, -30f, 30f);
        delta.Respect = Mathf.Clamp(delta.Respect, -30f, 30f);

        return delta;
    }

    private string GetFallbackDialogue(GuardFSM fsm)
    {
        return fsm.CurrentState switch
        {
            GuardState.Alert => "Backup needed. Nobody moves.",
            GuardState.Convinced => "...Fine. Go. Don't make me regret this.",
            GuardState.Suspicious => "I'm not buying it. Stay where you are.",
            GuardState.Wavering => "I don't know... maybe you're right, but...",
            GuardState.Amused => "Ha. Still not letting you through.",
            GuardState.Curious => "Go on. I'm listening.",
            _ => "What do you want?"
        };
    }
}