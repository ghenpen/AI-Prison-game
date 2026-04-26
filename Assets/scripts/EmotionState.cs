using System;
using System.Collections.Generic;

[Serializable]
public class EmotionState
{
    public float Suspicion { get; private set; } = 0f;
    public float Sympathy { get; private set; } = 0f;
    public float Fear { get; private set; } = 0f;
    public float Guilt { get; private set; } = 0f;
    public float Amusement { get; private set; } = 0f;
    public float Respect { get; private set; } = 0f;

    public float SympathyBias = 1f;
    public float FearBias = 1f;
    public float GuiltBias = 1f;
    public float AmusementBias = 1f;
    public float RespectBias = 1f;

    public void ApplyDelta(EmotionDelta delta)
    {
        Suspicion = Clamp(Suspicion + delta.Suspicion);
        Sympathy = Clamp(Sympathy + delta.Sympathy * SympathyBias);
        Fear = Clamp(Fear + delta.Fear * FearBias);
        Guilt = Clamp(Guilt + delta.Guilt * GuiltBias);
        Amusement = Clamp(Amusement + delta.Amusement * AmusementBias);
        Respect = Clamp(Respect + delta.Respect * RespectBias);
    }

    public void ApplyDecay()
    {
        Amusement = Clamp(Amusement - 3f);
        Sympathy = Clamp(Sympathy - 1f);
        Fear = Clamp(Fear - 3f);
        Guilt = Clamp(Guilt - 1f);
    }

    public void SetSuspicionBase(float value)
    {
        if (value > Suspicion)
            Suspicion = Clamp(value);
    }

    public void SetBiases(
        float sympathyBias = 1f,
        float guiltBias = 1f,
        float fearBias = 1f,
        float amusementBias = 1f,
        float respectBias = 1f)
    {
        SympathyBias = sympathyBias;
        GuiltBias = guiltBias;
        FearBias = fearBias;
        AmusementBias = amusementBias;
        RespectBias = respectBias;
    }

    public void Reset()
    {
        Suspicion = 30f;
        Sympathy = 0f;
        Fear = 0f;
        Guilt = 0f;
        Amusement = 0f;
        Respect = 0f;
        SympathyBias = 1f;
        FearBias = 1f;
        GuiltBias = 1f;
        AmusementBias = 1f;
        RespectBias = 1f;
    }

    public string GetDominantEmotion()
    {
        var emotions = new Dictionary<string, float>
        {
            { "Sympathy",  Sympathy  },
            { "Fear",      Fear      },
            { "Guilt",     Guilt     },
            { "Amusement", Amusement },
            { "Respect",   Respect   },
        };

        string dominant = "None";
        float max = 20f;
        foreach (var e in emotions)
            if (e.Value > max) { max = e.Value; dominant = e.Key; }

        return dominant;
    }

    public string ToPromptString()
    {
        return $"Suspicion={Suspicion:F0}, Sympathy={Sympathy:F0}, Fear={Fear:F0}, " +
               $"Guilt={Guilt:F0}, Amusement={Amusement:F0}, Respect={Respect:F0}";
    }

    private float Clamp(float val) => Math.Max(0f, Math.Min(100f, val));
}

[Serializable]
public class EmotionDelta
{
    public float Suspicion = 0f;
    public float Sympathy = 0f;
    public float Fear = 0f;
    public float Guilt = 0f;
    public float Amusement = 0f;
    public float Respect = 0f;
}