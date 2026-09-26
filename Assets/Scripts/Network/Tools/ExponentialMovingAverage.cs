using System;

public struct ExponentialMovingAverage
{
    private readonly double alpha;
    private bool initialized;

    public double Value;
    public double Variance;//方差
    public double StandardDeviation;//标准差

    public ExponentialMovingAverage(int n)
    {
        alpha = 2.0 / (n + 1);

        initialized = false;

        Value = 0;
        Variance = 0;
        StandardDeviation = 0;
    }
    public void Add(double newValue)
    {
        if (initialized)
        {
            double delta = newValue - Value;
            Value += alpha * delta;
            Variance = (1 - alpha) * (Variance + alpha * delta * delta);
            StandardDeviation = Math.Sqrt(Variance);
        }
        else
        {
            Value = newValue;
            initialized = true;
        }
    }

    public void Reset()
    {
        initialized = false;

        Value = 0;
        Variance = 0;
        StandardDeviation = 0;
    }
}
