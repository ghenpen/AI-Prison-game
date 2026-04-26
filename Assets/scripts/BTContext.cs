using System.Collections.Generic;

public enum GuardPersonality { Neutral, Strict, Empathetic, Corrupt, Paranoid }

public class BTContext
{
    public EmotionState Emotions;
    public GuardFSM FSM;
    public PlayerIntent LastIntent;
    public GuardPersonality Personality;

    public GuardState ResultState = GuardState.Neutral;
    public string OverrideDialogueHint = null; 

    private Dictionary<PlayerIntent, int> _intentHistory = new();
    private Dictionary<string, int> _counters = new();

    public void RecordIntent(PlayerIntent intent)
    {
        if (!_intentHistory.ContainsKey(intent)) _intentHistory[intent] = 0;
        _intentHistory[intent]++;
    }

    public int GetIntentCount(PlayerIntent intent)
        => _intentHistory.TryGetValue(intent, out int c) ? c : 0;

    public int GetCounter(string key)
        => _counters.TryGetValue(key, out int c) ? c : 0;

    public void IncrementCounter(string key)
    {
        if (!_counters.ContainsKey(key)) _counters[key] = 0;
        _counters[key]++;
    }

    public void ResetForNewSession()
    {
        _intentHistory.Clear();
        _counters.Clear();
    }
}