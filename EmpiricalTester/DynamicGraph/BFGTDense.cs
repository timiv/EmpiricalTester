﻿using System;
using System.Collections.Generic;
using System.Linq;
using static System.Math;

namespace EmpiricalTester.DynamicGraph
{
    class BFGTDense : IDynamicGraph
    {
        private List<BFGTDenseNode> nodes;
        private int n;

        public bool CycleBackup { get; set; }

        public BFGTDense()
        {
            n = 0;
            nodes = new List<BFGTDenseNode>();

            CycleBackup = false; // Set to true for graph generation (when cycles are added) 
        }

        public void AddVertex()
        {
            nodes.Add(new BFGTDenseNode(n++));
        }

        public bool AddEdge(int iv, int iw)
        {
            var v = nodes[iv];
            var w = nodes[iw];

            if (v.Level >= w.Level)
            {
                var A = new List<Edge>();
                
                // TODO make this more targetted
                var Recovery = CycleBackup ? new List<BFGTDenseNode>() : null;
                if (CycleBackup)
                {
                    foreach (var node in nodes)
                    {
                        Recovery.Add(new BFGTDenseNode(node));
                    }
                }
                
                //Recovery.Add(new BFGTDenseNode(v));
                //Recovery.Add(new BFGTDenseNode(w));

                A.Add(new Edge(v, w));

                while (A.Count > 0)
                {
                    var x = A[0].X;
                    var y = A[0].Y;
                    A.RemoveAt(0);

                    if (y == v)
                    {
                        if (CycleBackup)
                        {
                            foreach (var node in Recovery)
                            {
                                nodes[node.Index].Level = node.Level;
                                nodes[node.Index].InDegree = node.InDegree;
                                nodes[node.Index].J = node.J;

                                nodes[node.Index].Outgoing = node.Outgoing;
                                nodes[node.Index].KOut = node.KOut;
                                nodes[node.Index].Bound = node.Bound;
                                nodes[node.Index].Count = node.Count;
                            }
                        }
                        
                        return false; // cycle
                    }
                        
                    if (x.Level >= y.Level)
                    {
                        y.Level = x.Level + 1;
                    }
                    else
                    {
                        int j = (int)Floor(Log(Min(y.Level - x.Level, y.InDegree), 2));
                        x.J = j;

                        if (x.Count.ContainsKey(j) && x.Count[j].ContainsKey(y.Index))
                            x.Count[j][y.Index]++;
                        else
                        {
                            if(!x.Count.ContainsKey(j))
                                x.Count.Add(j, new Dictionary<int, int>());
                            x.Count[j].Add(y.Index, 1);
                        }
                        
                        if (x.Count[j][y.Index] == (3*Pow(2, j)))
                        {
                            x.Count[j][y.Index] = 0;

                            int bound = x.Bound.ContainsKey(j) && x.Bound[j].ContainsKey(y.Index) ? x.Bound[j][y.Index] : 1;
                            y.Level = Max(y.Level, bound + (int)Pow(2, j));
                            if(!x.Bound.ContainsKey(j))
                                x.Bound.Add(j, new Dictionary<int, int>());
                            if(!x.Bound[j].ContainsKey(y.Index))
                                x.Bound[j].Add(y.Index, y.Level);
                            else
                                x.Bound[j][y.Index] = y.Level;
                        }
                    }

                    if (!y.Outgoing.IsEmpty)
                    {
                        var curr = y.Outgoing.FindMin();
                        
                        while (curr.Key <= y.Level)
                        {
                            y.Outgoing.DeleteMin();                           
                            A.Add(new Edge(y, curr.Value));
                            //Recovery.Add(new BFGTDenseNode(curr.Value));
                            if (y.Outgoing.IsEmpty)
                                break;
                            curr = y.Outgoing.FindMin();
                        }
                    }
                    x.Outgoing.Add(new KVP(y.Level, y));
                }
            }

            v.AddOutGoing(w);

            return true;
        }

        public void RemoveEdge(int v, int w)
        {
            throw new NotImplementedException();
        }

        public List<int> Topology()
        {
            return nodes.OrderBy(n => n.Level).ToList().ConvertAll(i => i.Index);
        }

        public void ResetAll(int newN)
        {
            nodes.Clear();
            n = 0;
        }

        public void ResetAll(int newN, int newM)
        {
            ResetAll(newN);
        }
    }

    internal class Edge
    {
        public BFGTDenseNode X { get; set; }
        public BFGTDenseNode Y { get; set; }

        public Edge(BFGTDenseNode x, BFGTDenseNode y)
        {
            X = x;
            Y = y;
        }
    }

    internal class KVP : IComparable
    {
        public int Key { get; set; }
        public BFGTDenseNode Value { get; set; }

        public KVP(int key, BFGTDenseNode value)
        {
            Key = key;
            Value = value;
        }

        public int CompareTo(object obj)
        {
            return Key.CompareTo(((KVP)obj).Key);
        }
    }

    
}
