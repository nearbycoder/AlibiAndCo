using System;
using System.Collections.Generic;

namespace AlibiCo.Logic
{
    /// <summary>All-pairs shortest walking times (Floyd–Warshall) plus route reconstruction.</summary>
    public sealed class TownMap
    {
        public readonly TownData Data;
        readonly Dictionary<string, int> index = new Dictionary<string, int>();
        readonly int[,] dist;
        readonly int[,] next;
        const int Inf = 1 << 20;

        public TownMap(TownData data)
        {
            Data = data;
            int n = data.Locations.Count;
            for (int i = 0; i < n; i++) index[data.Locations[i].Id] = i;
            dist = new int[n, n];
            next = new int[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    dist[i, j] = i == j ? 0 : Inf;
                    next[i, j] = i == j ? i : -1;
                }
            foreach (var s in data.Streets)
            {
                if (!index.TryGetValue(s.A, out int a) || !index.TryGetValue(s.B, out int b))
                    throw new ArgumentException($"street {s.A}-{s.B} names an unknown place");
                if (s.Minutes < dist[a, b])
                {
                    dist[a, b] = dist[b, a] = s.Minutes;
                    next[a, b] = b;
                    next[b, a] = a;
                }
            }
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        if (dist[i, k] + dist[k, j] < dist[i, j])
                        {
                            dist[i, j] = dist[i, k] + dist[k, j];
                            next[i, j] = next[i, k];
                        }
        }

        public bool Has(string id) => id != null && index.ContainsKey(id);

        /// <summary>Walking minutes between two places (0 for the same place).</summary>
        public int Minutes(string a, string b)
        {
            if (a == b) return 0;
            int d = dist[index[a], index[b]];
            if (d >= Inf) throw new InvalidOperationException($"no route {a} -> {b}");
            return d;
        }

        /// <summary>The sequence of places along the shortest route, inclusive.</summary>
        public List<string> Route(string a, string b)
        {
            var r = new List<string> { a };
            if (a == b) return r;
            int i = index[a], j = index[b];
            if (next[i, j] < 0) return r;
            while (i != j)
            {
                i = next[i, j];
                r.Add(Data.Locations[i].Id);
            }
            return r;
        }
    }
}
