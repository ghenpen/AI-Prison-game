using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
//emotion current -action choosen(greedyduh) 


// 2 dqn, identic copy _main si _target , un c
// main every step - target at 100 steps........
public enum DQNAction
{
    ResistLow = 0, 
    ResistMedium = 1, 
    ResistHigh = 2, 
    CounterAppeal = 3, 
    CounterThreat = 4, 
    CounterBribe = 5, 
    CounterHumor = 6, 
    IgnoreAndWait = 7  
}

public class DQNAgent
{
    private const int STATE_SIZE = 6 + 8 + 1 + 7 + 3;  // 25
    private const int ACTION_SIZE = 8; 
    private const int HIDDEN_SIZE = 64;
    private const int BUFFER_SIZE = 10000;
    private const int BATCH_SIZE = 64;
    private const int MIN_BUFFER = 500;
    private const int TARGET_UPDATE = 100;
    private const float GAMMA = 0.95f;
    private const float LR = 0.01f;
    private const float EPSILON_START = 1.0f;
    private const float EPSILON_END = 0.05f;
    private const float EPSILON_DECAY = 0.999f;
    private const string SAVE_PATH = "dqn_guard.json";

    private NeuralNetwork _mainNet;
    private NeuralNetwork _targetNet;
    private ReplayBuffer _buffer;

    private float _epsilon;
    private int _totalSteps;
    private float _lastLoss;

    private float[] _currentState;
    private int _currentAction;

    private System.Random _rng = new System.Random();

    public float Epsilon => _epsilon;
    public float LastLoss => _lastLoss;
    public int TotalSteps => _totalSteps;
    public DQNAction CurrentAction => (DQNAction)_currentAction;

    public DQNAgent()
    {
        _mainNet = new NeuralNetwork(STATE_SIZE, HIDDEN_SIZE, HIDDEN_SIZE, ACTION_SIZE);
        _targetNet = new NeuralNetwork(_mainNet);
        _buffer = new ReplayBuffer(BUFFER_SIZE);
        _epsilon = EPSILON_START;
        TryLoad();
    }

    public void BeginEpisode(EmotionState emotions, PlayerIntent lastIntent, int turnsInState, GuardFSM fsm)
    {
        _currentState = BuildStateVector(emotions, lastIntent, turnsInState, fsm);
        _currentAction = SelectAction(_currentState);
    }

    public DQNAction Step(
        EmotionState nextEmotions,
        PlayerIntent lastIntent,
        int turnsInState,
        GuardFSM fsm,
        float reward,
        bool done)
    {
        _totalSteps++;

        float[] nextState = BuildStateVector(nextEmotions, lastIntent, turnsInState, fsm);
        _buffer.Add(_currentState, _currentAction, reward, nextState, done);

        if (_buffer.IsReady(MIN_BUFFER))
        {
            TrainStep();

            if (_totalSteps % TARGET_UPDATE == 0)
            {
                _targetNet.CopyWeightsFrom(_mainNet);
                Debug.Log($"  DQN  |  Target sync @ step {_totalSteps}  loss={_lastLoss:F4}");
            }

            _epsilon = Math.Max(EPSILON_END, _epsilon * EPSILON_DECAY);
        }

        _currentState = nextState;
        _currentAction = done ? 0 : SelectAction(nextState);

        return (DQNAction)_currentAction;
    }

    // rewardu schimba!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    public float CalculateReward(GuardState state, bool gameOver, bool gameWon)
    {
        if (gameOver && !gameWon) return +1.0f;  
        if (gameOver && gameWon) return -1.0f;  
        if (state == GuardState.Wavering) return -0.1f;  
        if (state == GuardState.Convinced) return -1.0f;  
        if (state == GuardState.Suspicious) return +0.2f; 
        return +0.1f; 
    }

    public void ApplyActionToFSM(
        DQNAction action,
        GuardFSM fsm,
        EmotionState emotions,
        PlayerIntent intent,
        IntentDecisionTree dt)
    {
        switch (action)
        {
            case DQNAction.ResistLow:
                fsm.SetResistanceLevel(0);
                emotions.SetSuspicionBase(25f);
                emotions.SetBiases(1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
                break;

            case DQNAction.ResistMedium:
                fsm.SetResistanceLevel(1);
                emotions.SetSuspicionBase(40f);
                emotions.SetBiases(0.8f, 0.8f, 1.0f, 0.9f, 1.0f);
                break;

            case DQNAction.ResistHigh:
                fsm.SetResistanceLevel(2);
                emotions.SetSuspicionBase(60f);
                emotions.SetBiases(0.5f, 0.5f, 1.2f, 0.6f, 1.0f);
                break;

            case DQNAction.CounterAppeal:
                emotions.ApplyDelta(new EmotionDelta
                { Suspicion = +12f, Sympathy = -8f, Guilt = -8f });
                fsm.SetResistanceLevel(1);
                break;

            case DQNAction.CounterThreat:
                emotions.ApplyDelta(new EmotionDelta
                { Fear = -10f, Suspicion = +10f });
                emotions.SetBiases(fearBias: 0.3f);
                break;

            case DQNAction.CounterBribe:
                emotions.ApplyDelta(new EmotionDelta
                { Suspicion = +20f, Respect = -10f });
                break;

            case DQNAction.CounterHumor:
                emotions.ApplyDelta(new EmotionDelta
                { Amusement = -15f, Suspicion = +5f });
                emotions.SetBiases(amusementBias: 0.2f);
                break;

            case DQNAction.IgnoreAndWait:
                emotions.ApplyDelta(new EmotionDelta { Suspicion = +5f });
                break;
        }
    }

    public void Save()
    {
        var saveData = new DQNSaveData
        {
            Network = _mainNet.Serialize(),
            Buffer = _buffer.Serialize(),
            Epsilon = _epsilon,
            TotalSteps = _totalSteps
        };

        string json = JsonConvert.SerializeObject(saveData);
        string path = Path.Combine(Application.persistentDataPath, SAVE_PATH);
        File.WriteAllText(path, json);
        Debug.Log($"  DQN  |  Saved  steps={_totalSteps}  ε={_epsilon:F3}");
    }

    public void TryLoad()
    {
        string path = Path.Combine(Application.persistentDataPath, SAVE_PATH);
        if (!File.Exists(path))
        {
            Debug.Log("  DQN  |  No save found");
            return;
        }
        try
        {
            string json = File.ReadAllText(path);
            var saveData = JsonConvert.DeserializeObject<DQNSaveData>(json);
            _mainNet.LoadFrom(saveData.Network);
            _targetNet.CopyWeightsFrom(_mainNet);
            _buffer.LoadFrom(saveData.Buffer);
            _epsilon = saveData.Epsilon;
            _totalSteps = saveData.TotalSteps;
            Debug.Log($"  DQN  |  Loaded  steps={_totalSteps}  ε={_epsilon:F3}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"  DQN  |  Load failed: {e.Message}");
        }
    }

    private int SelectAction(float[] state)
    {
        if (_rng.NextDouble() < _epsilon)
            return _rng.Next(ACTION_SIZE);

        float[] qValues = _mainNet.Forward(state);
        return ArgMax(qValues);
    }

    private void TrainStep()
    {
        Experience[] batch = _buffer.SampleBatch(BATCH_SIZE);

        float[][] inputs = new float[batch.Length][];
        float[][] targets = new float[batch.Length][];

        for (int i = 0; i < batch.Length; i++)
        {
            var exp = batch[i];
            float[] qValues = _mainNet.Forward(exp.State);

            float targetQ;
            if (exp.Done)
            {
                targetQ = exp.Reward;
            }
            else
            {
                float[] nextQMain = _mainNet.Forward(exp.NextState);
                int bestAction = ArgMax(nextQMain);
                float[] nextQTarget = _targetNet.Forward(exp.NextState);
                targetQ = exp.Reward + GAMMA * nextQTarget[bestAction];
            }

            targets[i] = (float[])qValues.Clone();
            targets[i][exp.Action] = targetQ;
            inputs[i] = exp.State;
        }

        _lastLoss = _mainNet.TrainBatch(inputs, targets, LR);
    }

    private float[] BuildStateVector(
    EmotionState e,
    PlayerIntent intent,
    int turnsInState,
    GuardFSM fsm)
    {
        var state = new float[STATE_SIZE];

        state[0] = e.Suspicion / 100f;
        state[1] = e.Sympathy / 100f;
        state[2] = e.Fear / 100f;
        state[3] = e.Guilt / 100f;
        state[4] = e.Amusement / 100f;
        state[5] = e.Respect / 100f;

        // [6..13] 
        state[6 + (int)intent] = 1f;

        // [14] turns 
        state[14] = Math.Min(turnsInState, 10) / 10f;

        // [15..21] FSM state 
        state[15 + (int)fsm.CurrentState] = 1f;

        // [22..24] resistance level 
        state[22 + fsm.GetResistanceLevel()] = 1f;

        return state;
    }

    private int ArgMax(float[] values)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++)
            if (values[i] > values[best]) best = i;
        return best;
    }
}

[Serializable]
public class DQNSaveData
{
    public NetworkData Network;
    public ReplayBufferData Buffer;
    public float Epsilon;
    public int TotalSteps;
}