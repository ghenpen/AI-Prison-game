using System;
using System.Collections.Generic;

[Serializable]
public class Experience
{
    public float[] State;      
    public int Action;         
    public float Reward;       
    public float[] NextState;  
    public bool Done;          

    public Experience(float[] state, int action, float reward, float[] nextState, bool done)
    {
        State = (float[])state.Clone();
        Action = action;
        Reward = reward;
        NextState = (float[])nextState.Clone();
        Done = done;
    }
}

public class ReplayBuffer
{
    private Experience[] _buffer;
    private int _capacity;
    private int _head;    
    private int _size;    
    private Random _rng;

    public int Count => _size;
    public bool IsReady(int minSize) => _size >= minSize;

    public ReplayBuffer(int capacity, int seed = 42)
    {
        _capacity = capacity;
        _buffer = new Experience[capacity];
        _head = 0;
        _size = 0;
        _rng = new Random(seed);
    }

    public void Add(float[] state, int action, float reward, float[] nextState, bool done)
    {
        _buffer[_head] = new Experience(state, action, reward, nextState, done);
        _head = (_head + 1) % _capacity; 
        _size = Math.Min(_size + 1, _capacity);
    }

    public Experience[] SampleBatch(int batchSize)
    {
        if (batchSize > _size)
            batchSize = _size;

        int[] indices = new int[_size];
        for (int i = 0; i < _size; i++) indices[i] = i;

        for (int i = _size - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        var batch = new Experience[batchSize];
        for (int i = 0; i < batchSize; i++)
            batch[i] = _buffer[indices[i]];

        return batch;
    }

    public ReplayBufferData Serialize()
    {
        var experiences = new Experience[_size];
        for (int i = 0; i < _size; i++)
            experiences[i] = _buffer[i];

        return new ReplayBufferData
        {
            Experiences = experiences,
            Head = _head,
            Size = _size
        };
    }

    public void LoadFrom(ReplayBufferData data)
    {
        _size = Math.Min(data.Size, _capacity);
        _head = data.Head % _capacity;

        for (int i = 0; i < _size; i++)
            _buffer[i] = data.Experiences[i];
    }
}

[Serializable]
public class ReplayBufferData
{
    public Experience[] Experiences;
    public int Head;
    public int Size;
}