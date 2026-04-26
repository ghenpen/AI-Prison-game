using System;

public enum GuardState
{
    Neutral,
    Suspicious,
    Curious,
    Wavering,
    Convinced,
    Alert,
    Amused
}

public class GuardFSM
{
    public GuardState CurrentState { get; private set; } = GuardState.Neutral;
    private EmotionState _emotions;
    private int _resistanceLevel = 0; 

    public int TurnsInCurrentState { get; private set; } = 0;

    public GuardFSM(EmotionState emotions)
    {
        _emotions = emotions;
    }

    public void SetResistanceLevel(int level)
    {
        _resistanceLevel = Math.Clamp(level, 0, 2);
    }

    public int GetResistanceLevel() => _resistanceLevel;

    public GuardState Evaluate()
    {
        GuardState newState = ComputeState();

        if (newState != CurrentState)
        {
            TurnsInCurrentState = 0;
            CurrentState = newState;
        }
        else
        {
            TurnsInCurrentState++;
        }

        return CurrentState;
    }

    private GuardState ComputeState()
    {
        float convincedSuspicion = _resistanceLevel switch
        {
            2 => 20f,  
            1 => 30f,  
            _ => 40f   
        };

        float convincedEmotion = _resistanceLevel switch
        {
            2 => 60f,  
            1 => 45f,  
            _ => 35f   
        };

        float suspiciousThresh = _resistanceLevel switch
        {
            2 => 45f,  
            1 => 55f,  
            _ => 60f   
        };

        float waveringSymp = _resistanceLevel switch
        {
            2 => 40f,
            1 => 30f,
            _ => 25f
        };

        float waveringGuilt = _resistanceLevel switch
        {
            2 => 30f,
            1 => 20f,
            _ => 15f
        };

        if (_emotions.Fear >= 70f)
            return GuardState.Alert;

        if (_emotions.Suspicion <= convincedSuspicion &&
           (_emotions.Respect >= convincedEmotion ||
            _emotions.Sympathy >= convincedEmotion ||
            _emotions.Guilt >= convincedEmotion - 5f))
            return GuardState.Convinced;

        if (_emotions.Sympathy >= waveringSymp && _emotions.Guilt >= waveringGuilt)
            return GuardState.Wavering;

        if (_emotions.Amusement >= 25f && _emotions.Suspicion < 55f)
            return GuardState.Amused;

        if (_emotions.Respect >= 20f || _emotions.Sympathy >= 18f)
            return GuardState.Curious;

        if (_emotions.Suspicion >= suspiciousThresh || _emotions.Fear >= 25f)
            return GuardState.Suspicious;

        return GuardState.Neutral;
    }

    public string ToPromptString()
    {
        return CurrentState switch
        {
            GuardState.Neutral => "you are calm and neutral, doing your job",
            GuardState.Suspicious => "you are suspicious and on guard, you don't believe what you're told",
            GuardState.Curious => "you are curious, listening more carefully",
            GuardState.Wavering => "you are hesitant, feeling like you might be making a mistake",
            GuardState.Convinced => "you are convinced, letting them go",
            GuardState.Alert => "you are on high alert, calling for backup",
            GuardState.Amused => "you are amused, your guard has lowered a little",
            _ => "you are neutral"
        };
    }

    public string ResistanceLevelToString() => _resistanceLevel switch
    {
        2 => "high",
        1 => "medium",
        _ => "low"
    };

    public bool IsGameOver() => CurrentState == GuardState.Alert;
    public bool IsGameWon() => CurrentState == GuardState.Convinced;

    public void Reset()
    {
        CurrentState = GuardState.Neutral;
        TurnsInCurrentState = 0;
        _resistanceLevel = 0;
    }
}