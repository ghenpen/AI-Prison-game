using System.Collections.Generic;

public enum PlayerIntent
{
    Bribe,      
    Threaten,   
    Reason,     
    Appeal,     
    Distract,   
    Humor,      
    Guilt,     
    Unknown    
}

public class IntentDecisionTree
{
    private static readonly Dictionary<PlayerIntent, EmotionDelta> _baseDeltas = new()
    {
        [PlayerIntent.Bribe] = new EmotionDelta { Suspicion = +15f, Respect = -5f },
        [PlayerIntent.Threaten] = new EmotionDelta { Fear = +25f, Suspicion = +20f, Sympathy = -10f },
        [PlayerIntent.Reason] = new EmotionDelta { Respect = +20f, Suspicion = -15f },
        [PlayerIntent.Appeal] = new EmotionDelta { Sympathy = +30f, Guilt = +15f, Suspicion = -10f },
        [PlayerIntent.Distract] = new EmotionDelta { Suspicion = +10f, Amusement = +5f },
        [PlayerIntent.Humor] = new EmotionDelta { Amusement = +30f, Suspicion = -15f, Fear = -5f },
        [PlayerIntent.Guilt] = new EmotionDelta { Guilt = +30f, Sympathy = +15f, Suspicion = -10f },
        [PlayerIntent.Unknown] = new EmotionDelta { Suspicion = +5f },
    };

    public EmotionDelta GetBaseDelta(PlayerIntent intent)
        => _baseDeltas.TryGetValue(intent, out var delta)
           ? delta
           : _baseDeltas[PlayerIntent.Unknown];

    public string IntentToPromptString(PlayerIntent intent) => intent switch
    {
        PlayerIntent.Bribe => "the player is trying to bribe you",
        PlayerIntent.Threaten => "the player is threatening you",
        PlayerIntent.Reason => "the player is using logical arguments",
        PlayerIntent.Appeal => "the player is making an emotional appeal",
        PlayerIntent.Distract => "the player is trying to distract you",
        PlayerIntent.Humor => "the player is trying to make you laugh",
        PlayerIntent.Guilt => "the player is trying to make you feel guilty",
        _ => "the player says something unclear"
    };
}