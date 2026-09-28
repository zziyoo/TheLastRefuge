namespace LastRefuge.Core
{
    public interface IRandomSystem
    {
        string GameSeed { get; }
        void Initialize(string seed);
        void Initialize(int seed);
        int NextInt(int min, int max);
        int NextInt(int max);
        float NextFloat();
        float NextFloat(float min, float max);
        bool NextBool(float probability = 0.5f);
        T NextElement<T>(T[] array);
        T NextElement<T>(System.Collections.Generic.List<T> list);
        int NextWeightedIndex(float[] weights);
        System.Random GetSubRandom(string context);
        int NextInt(string context, int min, int max);
        float NextFloat(string context);
        bool NextBool(string context, float probability = 0.5f);
        T NextElement<T>(string context, T[] array);
    }
}