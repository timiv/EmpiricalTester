﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace EmpiricalTester.GraphGeneration
{
    class GraphGenerator
    {
        private string filename;
        private List<Tuple<int, int>> edges;
        private int n;
        private int e;
        private bool staticCheck;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="n"></param>
        /// <param name="p"></param>
        /// <param name="staticCheck">If true always runs static check, otherwise only for the completed graph</param>
        /// <param name="staticGraphs"></param>
        /// <param name="dynamicGraphs"></param>
        /// <returns></returns>
        public string generateGraph(int n, double p, bool staticCheck, List<StaticGraph.IStaticGraph> staticGraphs, List<DynamicGraph.IDynamicGraph> dynamicGraphs)
        {
            this.staticCheck = staticCheck;
            DateTime datenow = DateTime.Now;
            filename = datenow.ToString("yyyyMMdd-HH-MM") + string.Format("({0}, {1})", n.ToString(), p.ToString());

            edges = new List<Tuple<int, int>>();
            this.n = n;

            Random random = new Random(DateTime.Now.Millisecond);

            for (int i = 0; i < n; i++)
            {
                foreach(StaticGraph.IStaticGraph graph in staticGraphs)
                {
                    graph.addVertex();
                }

                foreach(DynamicGraph.IDynamicGraph graph in dynamicGraphs)
                {
                    graph.addVertex();
                }
            }

            bool[] staticResults = new bool[staticGraphs.Count];
            bool[] dynamicResults = new bool[dynamicGraphs.Count];

            for(int i = 0; i < n; i++)
            {
                Console.WriteLine("Progress " + (double)i/(double)n*100 +"%");
                if(random.Next(0,100)/100.0 < p)
                {
                    e++; //increase edge count;
                    
                    int v = random.Next(0, n - 1);
                    int w = random.Next(0, n - 1);

                    // edge cannot already exist in the graph
                    while(null != edges.Find(item => item.Item1 == v && item.Item2 == w))
                    {
                        v = random.Next(0, n - 1);
                        w = random.Next(0, n - 1);
                    }

                    for(int x = 0; x < staticGraphs.Count; x++)
                    {
                        staticGraphs[x].addEdge(v, w);
                        
                        if(staticCheck)
                        {
                            // if null there is a cycle
                            if (null == staticGraphs[x].topoSort())
                                staticResults[x] = false; // cycle detected
                            else
                                staticResults[x] = true; // all is good
                        }
                    }

                    for(int x = 0; x < dynamicGraphs.Count; x++)
                    {
                        // dynamic graph returns false if cycle is detected
                        dynamicResults[x] = dynamicGraphs[x].addEdge(v, w);
                            
                    }

                    // if static check is false only dynamic results matter
                    bool allAgree = staticCheck ? 
                        (staticResults.All(item => item == true) && dynamicResults.All(item => item == true))
                        || (staticResults.All(item => item == false) && dynamicResults.All(item => item == false))
                        : 
                        dynamicResults.All(item => item == true) || dynamicResults.All(item => item == false);

                    // if all are false i.e. all failed since there was a cycle
                    bool cycle = staticCheck ?
                        staticResults.All(item => item == false) && dynamicResults.All(item => item == false)
                        :
                        dynamicResults.All(item => item == false);

                    // if all do not agree
                    if ( ! allAgree )
                    {
                        edges.Add(new Tuple<int, int>(v, w)); // add the error edge to the list
                        writeFile("Crash-NotAgree");
                        throw new Exception("The algorithms do not agree");
                    }
                    
                    // cycle, remove this edge
                    if(cycle)
                    {
                        foreach(StaticGraph.IStaticGraph graph in staticGraphs)
                        {
                            graph.removeEdge(v, w);
                        }
                    }
                    else
                    {
                        edges.Add(new Tuple<int, int>(v, w));
                    }
                }
            }

            // if static check is false, run a static check at the end
            if(!staticCheck)
            {
                Console.WriteLine("Performing final static check");

                for (int x = 0; x < staticGraphs.Count; x++)
                {
                    // if null there is a cycle
                    if (null == staticGraphs[x].topoSort())
                        staticResults[x] = false; // cycle detected
                    else
                        staticResults[x] = true; // all is good                 
                }

                // if not all agree throw error
                if(!(staticResults.All(item => item == true) || staticResults.All(item => item == false)))
                {
                    throw new Exception("Final static check failed");
                }

            }


            //compare topologies
            bool compare = topologyCompare(n, staticGraphs, dynamicGraphs);
            
            if(!compare)
            {
                writeFile("Crash-TopoOrder");
                throw new Exception("Topological order comparison failed");
            }

            return filename;
        }

        private void writeFile(string comment)
        {
            bool exists = Directory.Exists(Path.Combine(Environment.CurrentDirectory, @"Output"));

            if (!exists)
                Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, @"Output"));

            edges.Insert(0, new Tuple<int, int>(n, e)); // insert the vertex and edge counters in the start of the file
            File.WriteAllLines(Path.Combine(Environment.CurrentDirectory, @"Output\" + filename + comment + ".txt")
                , edges.ConvertAll(item => item.Item1.ToString() + " " + item.Item2.ToString()).ToArray());
        }

        // Compares the edges topological order
        private bool topologyCompare(int n, List<StaticGraph.IStaticGraph> staticGraphs, List<DynamicGraph.IDynamicGraph> dynamicGraphs)
        {
            // Find connected pairs to compare
            ConnectivityGraph connectivity = new ConnectivityGraph();
            
            for (int i = 0; i < n; i++)
            {
                connectivity.addVertex();
            }

            foreach(Tuple<int, int> edge in edges)
            {
                connectivity.addEdge(edge.Item1, edge.Item2);
            }

            // matrix of node connectivity
            List<Tuple<int, List<int>>> matrix = connectivity.generateConnectivityMatrix();

            // list of all algorithm topologies
            List<List<int>> topologies = new List<List<int>>();

            foreach(StaticGraph.IStaticGraph algorithm in staticGraphs)
            {
                topologies.Add(algorithm.topoSort().ToList<int>());
            }

            foreach (DynamicGraph.IDynamicGraph algorithm in dynamicGraphs)
            {
                topologies.Add(algorithm.topology());
            }
            

            List<Tuple<int, List<List<bool>>>> resultMatrix = new List<Tuple<int, List<List<bool>>>>();
            // create result matrix, each row is a index + list of comparison results index >= nodeInPath
            // inner most list represents the comparison for each topology
            for (int iFromNode = 0; iFromNode < matrix.Count; iFromNode++)
            {
                resultMatrix.Add(new Tuple<int, List<List<bool>>>(iFromNode, new List<List<bool>>()));

                for (int iToNode = 0; iToNode < matrix[iFromNode].Item2.Count; iToNode++)
                {
                    resultMatrix[iFromNode].Item2.Add(new List<bool>());
                }
            }
            
            // Fill in the resultMatrix
            for(int iFromNode = 0; iFromNode < matrix.Count; iFromNode++)
            {
                for(int iToNode = 0; iToNode < matrix[iFromNode].Item2.Count; iToNode++)
                {
                    for(int iTopology = 0; iTopology < topologies.Count; iTopology++)
                    {
                        resultMatrix[iFromNode].Item2[iToNode].Add(
                            topologies[iTopology].FindIndex(item => item == matrix[iFromNode].Item1) 
                            >= topologies[iTopology].FindIndex(item => item == matrix[iFromNode].Item2[iToNode]));
                    }
                }
            }


            // fold result matrix into a bool stating if all agree
            // negate the result since Any returns false if all were the same and distinct only returns 1 value which was skipped over
            bool allAgree = !resultMatrix.ConvertAll<bool>(i => i.Item2.ConvertAll<bool>(item => item.Distinct().Skip(1).Any()).Distinct().Skip(1).Any()).Distinct().Skip(1).Any();

            if(!allAgree)
            {
                writeFile("TopoCompFail");
                throw new Exception("Topological comparison failed");
            }


            return true;
        }

    }
}
