using System;

// nn with ReLU activations, MSE loss
// basically : the nn gets the emotion state as input, and outputs a vector of action values for the DQN to use as Q-values
// ex: input: [Sympathy, Fear, Guilt, Amusement, Respect] -> output: [Q_Convince, Q_Alarm, Q_Waver, Q_Suspicious, Q_Amused, Q_Curious] 

// 8-64-64-8   input layer of 8 emotion state + intent +cur state, two hidden layers of 64 neurons, and output layer of 8 (action values)
//
public class NeuralNetwork
{
    private int[] _layers;        
    private float[][] _neurons;   
    private float[][] _biases;    
    private float[][][] _weights; 

    private Random _rng = new Random();

    public NeuralNetwork(params int[] layers)
    {
        _layers = layers;
        InitNeurons();
        InitWeights();
    }

    public NeuralNetwork(NeuralNetwork source)
    {
        _layers = (int[])source._layers.Clone();
        InitNeurons();

        _weights = new float[source._weights.Length][][];
        for (int i = 0; i < source._weights.Length; i++)
        {
            _weights[i] = new float[source._weights[i].Length][];
            for (int j = 0; j < source._weights[i].Length; j++)
            {
                _weights[i][j] = (float[])source._weights[i][j].Clone();
            }
        }

        _biases = new float[source._biases.Length][];
        for (int i = 0; i < source._biases.Length; i++)
            _biases[i] = (float[])source._biases[i].Clone();
    }

    private void InitNeurons()
    {
        _neurons = new float[_layers.Length][];
        for (int i = 0; i < _layers.Length; i++)
            _neurons[i] = new float[_layers[i]];
    }

    private void InitWeights()
    {
        _biases = new float[_layers.Length][];
        _weights = new float[_layers.Length - 1][][];

        for (int i = 0; i < _layers.Length; i++)
        {
            _biases[i] = new float[_layers[i]];
        }

        for (int i = 0; i < _layers.Length - 1; i++)
        {
            int toSize = _layers[i + 1];
            int fromSize = _layers[i];

            _weights[i] = new float[toSize][];

            float std = (float)Math.Sqrt(2.0 / fromSize);

            for (int j = 0; j < toSize; j++)
            {
                _weights[i][j] = new float[fromSize];
                for (int k = 0; k < fromSize; k++)
                    _weights[i][j][k] = RandomGaussian() * std; //HE initialization nici prea mari nici prea mici 
            }
        }
    }
    // calc pond sum for every neuron from prev layer , + bias , then apply ReLU for hidden layers and identity for output layer
    public float[] Forward(float[] inputs)
    {
        for (int i = 0; i < inputs.Length; i++)
            _neurons[0][i] = inputs[i];

        for (int layer = 1; layer < _layers.Length; layer++)
        {
            bool isOutput = (layer == _layers.Length - 1);

            for (int j = 0; j < _layers[layer]; j++)
            {
                float sum = _biases[layer][j];

                for (int k = 0; k < _layers[layer - 1]; k++)
                    sum += _neurons[layer - 1][k] * _weights[layer - 1][j][k];

                _neurons[layer][j] = isOutput ? sum : ReLU(sum);
            }
        }

        return (float[])_neurons[_neurons.Length - 1].Clone();
    }

    public float TrainBatch(float[][] inputs, float[][] targets, float learningRate)
    {
        int batchSize = inputs.Length;
        int outputLayer = _layers.Length - 1;

        float[][] gradBiases = new float[_layers.Length][];
        for (int i = 0; i < _layers.Length; i++)
            gradBiases[i] = new float[_layers[i]];

        float[][][] gradWeights = new float[_layers.Length - 1][][];
        for (int i = 0; i < _layers.Length - 1; i++)
        {
            gradWeights[i] = new float[_layers[i + 1]][];
            for (int j = 0; j < _layers[i + 1]; j++)
                gradWeights[i][j] = new float[_layers[i]];
        }

        float totalLoss = 0f;

        // Pentru fiecare sample: forward + backward
        for (int s = 0; s < batchSize; s++)
        {
            Forward(inputs[s]);

            float[][] deltas = new float[_layers.Length][];
            for (int i = 0; i < _layers.Length; i++)
                deltas[i] = new float[_layers[i]];

            for (int j = 0; j < _layers[outputLayer]; j++)
            {
                float error = _neurons[outputLayer][j] - targets[s][j];
                totalLoss += (error * error) / _layers[outputLayer];
                deltas[outputLayer][j] = error;
            }

            for (int layer = outputLayer - 1; layer >= 1; layer--)
            {
                for (int j = 0; j < _layers[layer]; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < _layers[layer + 1]; k++)
                        sum += deltas[layer + 1][k] * _weights[layer][k][j];

                    deltas[layer][j] = sum * (_neurons[layer][j] > 0 ? 1f : 0f);
                }
            }

            for (int layer = 0; layer < _layers.Length - 1; layer++)
            {
                for (int j = 0; j < _layers[layer + 1]; j++)
                {
                    gradBiases[layer + 1][j] += deltas[layer + 1][j];
                    for (int k = 0; k < _layers[layer]; k++)
                        gradWeights[layer][j][k] += deltas[layer + 1][j] * _neurons[layer][k];
                }
            }
        }

        float invBatch = 1f / batchSize;

        const float MAX_GRAD_NORM = 1.0f;
        float sumSquares = 0f;

        for (int layer = 0; layer < _layers.Length - 1; layer++)
        {
            for (int j = 0; j < _layers[layer + 1]; j++)
            {
                float gb = gradBiases[layer + 1][j] * invBatch;
                sumSquares += gb * gb;

                for (int k = 0; k < _layers[layer]; k++)
                {
                    float gw = gradWeights[layer][j][k] * invBatch;
                    sumSquares += gw * gw;
                }
            }
        }

        float gradNorm = (float)Math.Sqrt(sumSquares);
        float scale = gradNorm > MAX_GRAD_NORM ? MAX_GRAD_NORM / gradNorm : 1f;
        float effectiveLR = learningRate * scale;

        for (int layer = 0; layer < _layers.Length - 1; layer++)
        {
            for (int j = 0; j < _layers[layer + 1]; j++)
            {
                _biases[layer + 1][j] -= effectiveLR * gradBiases[layer + 1][j] * invBatch;
                for (int k = 0; k < _layers[layer]; k++)
                    _weights[layer][j][k] -= effectiveLR * gradWeights[layer][j][k] * invBatch;
            }
        }

        return totalLoss / batchSize;
    }

    // backprop     i know how much error for output layer so adjust ponds 
    public float Train(float[] inputs, float[] targets, float learningRate)
    {
        Forward(inputs);

        float[][] deltas = new float[_layers.Length][];
        for (int i = 0; i < _layers.Length; i++)
            deltas[i] = new float[_layers[i]];

        float loss = 0f;
        int outputLayer = _layers.Length - 1;
        for (int j = 0; j < _layers[outputLayer]; j++)
        {
            float error = _neurons[outputLayer][j] - targets[j];
            loss += error * error;
            deltas[outputLayer][j] = error; 
        }
        loss /= _layers[outputLayer]; //MSE 

        for (int layer = outputLayer - 1; layer >= 1; layer--)
        {
            for (int j = 0; j < _layers[layer]; j++)
            {
                float sum = 0f;
                for (int k = 0; k < _layers[layer + 1]; k++)
                    sum += deltas[layer + 1][k] * _weights[layer][k][j];  // how much did it contribute

                deltas[layer][j] = sum * (_neurons[layer][j] > 0 ? 1f : 0f);
            }
        }

        for (int layer = 0; layer < _layers.Length - 1; layer++)
        {
            for (int j = 0; j < _layers[layer + 1]; j++)
            {
                _biases[layer + 1][j] -= learningRate * deltas[layer + 1][j];

                for (int k = 0; k < _layers[layer]; k++)
                    _weights[layer][j][k] -= learningRate * deltas[layer + 1][j] * _neurons[layer][k]; // update ponds
            }
        }

        return loss;
    }

    public void CopyWeightsFrom(NeuralNetwork source)
    {
        for (int i = 0; i < _weights.Length; i++)
            for (int j = 0; j < _weights[i].Length; j++)
                Array.Copy(source._weights[i][j], _weights[i][j], _weights[i][j].Length);

        for (int i = 0; i < _biases.Length; i++)
            Array.Copy(source._biases[i], _biases[i], _biases[i].Length);
    }

    public NetworkData Serialize()
    {
        var data = new NetworkData
        {
            Layers = (int[])_layers.Clone(),
            Biases = new float[_biases.Length][],
            Weights = new float[_weights.Length][][]
        };

        for (int i = 0; i < _biases.Length; i++)
            data.Biases[i] = (float[])_biases[i].Clone();

        for (int i = 0; i < _weights.Length; i++)
        {
            data.Weights[i] = new float[_weights[i].Length][];
            for (int j = 0; j < _weights[i].Length; j++)
                data.Weights[i][j] = (float[])_weights[i][j].Clone();
        }

        return data;
    }

    public void LoadFrom(NetworkData data)
    {
        for (int i = 0; i < _biases.Length; i++)
            Array.Copy(data.Biases[i], _biases[i], _biases[i].Length);

        for (int i = 0; i < _weights.Length; i++)
            for (int j = 0; j < _weights[i].Length; j++)
                Array.Copy(data.Weights[i][j], _weights[i][j], _weights[i][j].Length);
    }

    private float ReLU(float x) => x > 0 ? x : 0;

    private float RandomGaussian()
    {
        double u1 = 1.0 - _rng.NextDouble();
        double u2 = 1.0 - _rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
    }
}

[Serializable]
public class NetworkData
{
    public int[] Layers;
    public float[][] Biases;
    public float[][][] Weights;
}