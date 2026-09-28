using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastRefuge.Core
{
    public class RandomSystem : IRandomSystem
    {
        private System.Random mainRandom;
        private Dictionary<string, System.Random> subRandoms = new Dictionary<string, System.Random>();
        private string gameSeed;
        
        public string GameSeed => gameSeed;
        
        public void Initialize(string seed)
        {
            gameSeed = seed;
            int seedHash = seed.GetHashCode();
            mainRandom = new System.Random(seedHash);
            subRandoms.Clear();
        }
        
        public void Initialize(int seed)
        {
            gameSeed = seed.ToString();
            mainRandom = new System.Random(seed);
            subRandoms.Clear();
        }
        
        public int NextInt(int min, int max)
        {
            return mainRandom.Next(min, max);
        }
        
        public int NextInt(int max)
        {
            return mainRandom.Next(max);
        }
        
        public float NextFloat()
        {
            return (float)mainRandom.NextDouble();
        }
        
        public float NextFloat(float min, float max)
        {
            return min + (float)mainRandom.NextDouble() * (max - min);
        }
        
        public bool NextBool(float probability = 0.5f)
        {
            return NextFloat() < probability;
        }
        
        public T NextElement<T>(T[] array)
        {
            if (array == null || array.Length == 0) return default;
            return array[NextInt(array.Length)];
        }
        
        public T NextElement<T>(List<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[NextInt(list.Count)];
        }
        
        public int NextWeightedIndex(float[] weights)
        {
            if (weights == null || weights.Length == 0) return -1;
            
            float total = 0f;
            foreach (float w in weights) total += w;
            
            float roll = NextFloat() * total;
            float accum = 0f;
            
            for (int i = 0; i < weights.Length; i++)
            {
                accum += weights[i];
                if (roll <= accum) return i;
            }
            
            return weights.Length - 1;
        }
        
        public System.Random GetSubRandom(string context)
        {
            if (!subRandoms.ContainsKey(context))
            {
                int subSeed = (gameSeed + context).GetHashCode();
                subRandoms[context] = new System.Random(subSeed);
            }
            return subRandoms[context];
        }
        
        public int NextInt(string context, int min, int max)
        {
            return GetSubRandom(context).Next(min, max);
        }
        
        public float NextFloat(string context)
        {
            return (float)GetSubRandom(context).NextDouble();
        }
        
        public bool NextBool(string context, float probability = 0.5f)
        {
            return NextFloat(context) < probability;
        }
        
        public T NextElement<T>(string context, T[] array)
        {
            if (array == null || array.Length == 0) return default;
            return array[GetSubRandom(context).Next(array.Length)];
        }
        
        public void SetState(string context, int state)
        {
        }
    }
}