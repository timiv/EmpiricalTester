﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Windows.Forms.DataVisualization.Charting;
using EmpiricalTester.DynamicGraph;
using EmpiricalTester.StaticGraph;

namespace EmpiricalTester.Measuring
{
    class GraphRunner
    {
        private class Measurement
        {
            public string DirName { get; set; }
            public List<Tuple<string, List<long>>> measurements { get; set; }

            public Measurement(string name)
            {
                this.DirName = name;
                measurements = new List<Tuple<string, List<long>>>();
            }
        }

        public void runFolder(string folder, string outputFolder, int repeatCount, int resolution, List<IDynamicGraph> dynamicGraphs)
        {
            if (!Directory.Exists(folder))
                throw new InvalidDataException("Input folder does not exist");

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            // List< (algName, (folderName, list<long))>
            var measurements = new List<Measurement>();
            var directories = Directory.GetDirectories(folder);

            foreach (var dir in directories)
            {
                Console.WriteLine(dir);
                if (dir != outputFolder)
                {
                    measurements.Add(new Measurement(dir.Substring(dir.LastIndexOf('\\')+1)));
                    foreach (var alg in dynamicGraphs)
                    {
                        var ms = new List<List<long>>();
                    
                        foreach (var file in Directory.EnumerateFiles(dir))
                        {
                            // average per resolution, 1 alg, 1 file
                            ms.Add(runFile(file, alg, resolution, repeatCount));
                        }
                        // get the average of all the files (same p value)
                        var avg = Transpose(ms).ConvertAll(item => ((long)item.Average()));
                        var curr = measurements.Find(item => item.DirName == dir.Substring(dir.LastIndexOf('\\')+1));
                        curr.measurements.Add(new Tuple<string, List<long>>(alg.GetType().ToString(), avg));
                    }                                        
                }
            }

            // writeToFile
            var lines = new List<string>();
            
            foreach(var item in measurements)
            {
                lines.Add(item.DirName);
                foreach (var alg in item.measurements)
                {                    
                    var s = alg.Item1.Substring(alg.Item1.LastIndexOf('.'));
                    foreach(var number in alg.Item2)
                    {
                        s += "\t" + number.ToString();
                    }
                    lines.Add(s);
                }
            }

            File.WriteAllLines(outputFolder + "\\" + DateTime.Now.ToString("yyyyMMdd-HH-mm-ss") + ".txt", lines);
        }


        private List<long> runFile(string file, IDynamicGraph alg, int resolution, int repeatCount)
        {
            InputFile graph = readFile(file);
            Stopwatch sw = new Stopwatch();
            List<List<long>> times = new List<List<long>>();
            List<List<long>> timesVector = new List<List<long>>();

            for (int i = 0; i < repeatCount; i++)
            {
                alg.ResetAll(graph.n);
                int skip = 0;
                times.Add(new List<long>());

                for (int n = 0; n < graph.n; n++)
                    alg.AddVertex();

                do
                {
                    sw.Start();
                    foreach(var edge in graph.edges.Skip(skip).Take(resolution))
                    {
                        alg.AddEdge(edge.from, edge.to);
                    }
                    sw.Stop();
                    times[i].Add(sw.ElapsedTicks);
                    sw.Reset();

                    skip += resolution;
                } while (skip < graph.edges.Count);                
            }

            var ret = Transpose(times).ConvertAll(item => ((long)item.Average()));

            return ret;
        }


        public void runGraph(string[] fileNames, int repeateCount, bool writeToFile, bool makeGraphImage, List<StaticGraph.IStaticGraph> staticGraphs, List<IDynamicGraph> dynamicGraphs)
        {          
            for(int x = 0; x < fileNames.Length; x++)
            {
                string fileName = fileNames[x];
                InputFile graph = readFile(fileName);
                Stopwatch sw = new Stopwatch();
                List<Measurements> measurements = new List<Measurements>();

                Console.WriteLine("File " + (x+1) + "/ " + fileNames.Length);
                
                foreach (var algorithm in staticGraphs)
                {
                    measurements.Add(new Measurements(algorithm.GetType().ToString()));
                    Measurements current = measurements.Find(item => item.Name == algorithm.GetType().ToString());
                    
                    for (int i = 0; i < repeateCount; i++)
                    {
                        Console.WriteLine("Run: " + i + ", " + current.Name);
                        algorithm.ResetAll();
                        // add nodes
                        for (int y = 0; y < graph.n; y++)
                        {
                            algorithm.AddVertex();
                        }

                        current.timeElapsed.Add(new List<TimeSpan>());
                        foreach (Pair pair in graph.edges)
                        {
                            sw.Start();
                            algorithm.AddEdge(pair.from, pair.to);
                            algorithm.TopoSort();
                            sw.Stop();
                            current.timeElapsed[i].Add(sw.Elapsed);
                            sw.Reset();
                        }
                    }

                }

                foreach (IDynamicGraph algorithm in dynamicGraphs)
                {
                    measurements.Add(new Measurements(algorithm.GetType().ToString()));
                    Measurements current = measurements.Find(item => item.Name == algorithm.GetType().ToString());
                    
                    for (int i = 0; i < repeateCount; i++)
                    {
                        Console.WriteLine("Run: " + i + ", " + current.Name);
                        algorithm.ResetAll(graph.n);
                        // add nodes
                        for (int y = 0; y < graph.n; y++)
                        {
                            algorithm.AddVertex();
                        }

                        current.timeElapsed.Add(new List<TimeSpan>());
                        foreach (Pair pair in graph.edges)
                        {
                            sw.Start();
                            algorithm.AddEdge(pair.from, pair.to);
                            sw.Stop();
                            current.timeElapsed[i].Add(sw.Elapsed);
                            sw.Reset();
                        }
                    }
                }

                measurements.ForEach(item => item.updateStatistics());

                if(writeToFile)
                    writeMeasurements(fileName, measurements);
                if (makeGraphImage)
                    createGraph(fileName, measurements, graph.n, graph.edges.Count, repeateCount);
            }            
        }

        public void runStaticVsDynamic(string[] fileNames, int repeateCount, bool writeToFile, bool makeGraphImage, List<StaticGraph.IStaticGraph> staticGraphs, List<IDynamicGraph> dynamicGraphs)
        {
            for (int x = 0; x < fileNames.Length; x++)
            {
                string fileName = fileNames[x];
                InputFile graph = readFile(fileName);
                Stopwatch sw = new Stopwatch();
                List<Measurements> measurements = new List<Measurements>();

                Console.WriteLine("File " + (x + 1) + "/ " + fileNames.Length);

                foreach (StaticGraph.IStaticGraph algorithm in staticGraphs)
                {
                    measurements.Add(new Measurements(algorithm.GetType().ToString()));
                    Measurements current = measurements.Find(item => item.Name == algorithm.GetType().ToString());

                    for (int i = 0; i < repeateCount; i++)
                    {
                        Console.WriteLine("Run: " + i + ", " + current.Name);
                        algorithm.ResetAll();
                        // add nodes
                        for (int y = 0; y < graph.n; y++)
                        {
                            algorithm.AddVertex();
                        }

                        current.timeElapsed.Add(new List<TimeSpan>());
                        sw.Start();
                        foreach (Pair pair in graph.edges)
                        {                            
                            algorithm.AddEdge(pair.from, pair.to);
                                                    
                        }

                        algorithm.TopoSort();
                        sw.Stop();
                        current.timeElapsed[i].Add(sw.Elapsed);
                        sw.Reset();
                    }

                }

                foreach (IDynamicGraph algorithm in dynamicGraphs)
                {
                    measurements.Add(new Measurements(algorithm.GetType().ToString()));
                    Measurements current = measurements.Find(item => item.Name == algorithm.GetType().ToString());

                    for (int i = 0; i < repeateCount; i++)
                    {
                        Console.WriteLine("Run: " + i + ", " + current.Name);
                        algorithm.ResetAll(graph.n);
                        // add nodes
                        for (int y = 0; y < graph.n; y++)
                        {
                            algorithm.AddVertex();
                        }

                        current.timeElapsed.Add(new List<TimeSpan>());
                        sw.Start();
                        foreach (Pair pair in graph.edges)
                        {                            
                            algorithm.AddEdge(pair.from, pair.to);                            
                        }
                        sw.Stop();
                        current.timeElapsed[i].Add(sw.Elapsed);
                        sw.Reset();
                    }
                }


                measurements.ForEach(item => item.updateStatistics());

                if (writeToFile)
                    writeMeasurements(fileName, measurements);
                if (makeGraphImage)
                    createGraph(fileName, measurements, graph.n, graph.edges.Count, repeateCount);

            }
        }

        public void RunDirectoryHistogram(string baseDir, List<IDynamicGraph> algorithms, int startNumber, int endNumber)
        {
            if (!Directory.Exists(baseDir))
                throw new InvalidDataException("Input folder does not exist");

            var directories = Directory.GetDirectories(baseDir).ToList();
            directories = directories.OrderByDescending(path => int.Parse(path.Substring(path.LastIndexOf("-")))).ToList();

            var measurements = new List<Tuple<string, Dictionary<int, int>>>();// <name, dict<tick,count>>
            var sw = new Stopwatch();

            foreach (var alg in algorithms)
            {
                string algName = alg.GetType().ToString().Substring(alg.GetType().ToString().LastIndexOf(".") + 1);                
                var dict = new Dictionary<int, int>();

                foreach (var dir in directories)
                {
                    Console.WriteLine($"{algName}: {dir}");
                    foreach (var file in Directory.EnumerateFiles(dir))
                    {
                        var graph = readFile(file);
                        alg.ResetAll(graph.n, graph.m);

                        for (int i = 0; i < graph.n; i++)
                            alg.AddVertex();

                        foreach (var edge in graph.edges.GetRange(0, startNumber-1))
                        {
                            alg.AddEdge(edge.from, edge.to);
                        }

                        foreach (var edge in graph.edges.GetRange(startNumber, endNumber - startNumber))
                        {
                            sw.Reset();
                            sw.Start();
                            alg.AddEdge(edge.from, edge.to);
                            sw.Stop();
                            if (!dict.ContainsKey((int) sw.ElapsedTicks))
                                dict.Add((int) sw.ElapsedTicks, 1);
                            else
                                dict[(int) sw.ElapsedTicks]++;
                        }                        
                    }
                }
                
                //add to measurements
                measurements.Add(new Tuple<string, Dictionary<int, int>>(algName, dict));
            }

            // writeFile
            string outFile = baseDir + $"measurementsHisto{DateTime.Now.Hour}-{DateTime.Now.Minute}-{DateTime.Now.Second}.txt";
            var lines = new List<string>();
            foreach (var alg in measurements)
            {
                var ordered = alg.Item2.OrderBy(x => x.Key)
                    .ToList()
                    .ConvertAll(x => new Tuple<int, int>(x.Key, x.Value));

                lines.Add($"{alg.Item1}\t{ordered.ConvertAll(x => Convert.ToString(x.Item1)).Aggregate((a, b) => a + "\t" + b)}");
                lines.Add($"{alg.Item1}\t{ordered.ConvertAll(x => Convert.ToString(x.Item2)).Aggregate((a, b) => a + "\t" + b)}");                
            }

            File.WriteAllLines(outFile, lines);           
        }


        public void RunDirectoryPK(string baseDir, List<IDynamicGraph> dynamics, int segSize)
        {
            if (!Directory.Exists(baseDir))
                throw new InvalidDataException("Input folder does not exist");

            
            var sw = new Stopwatch();
            var measurements = new List<Tuple<string, List<List<long>>>>();


            foreach (var alg in dynamics)
            {
                var allValues = new List<List<long>>();

                foreach (var file in Directory.EnumerateFiles(baseDir))
                {
                    Console.WriteLine(alg.GetType().ToString().Substring(alg.GetType().ToString().LastIndexOf(".") + 1) + file);
                    var values = new List<long>();
                    var graph = readFile(file);
                    alg.ResetAll(graph.n, graph.m);

                    var segments = new List<List<Pair>>();
                    int index = 0;
                    while (index + segSize <= graph.edges.Count)
                    {
                        segments.Add(new List<Pair>(graph.edges.GetRange(index, segSize)));
                        index += segSize;
                    }
                    if(index < graph.edges.Count)
                        segments.Add(new List<Pair>(graph.edges.GetRange(index, graph.edges.Count - index)));


                    for (int i = 0; i < graph.n; i++)
                        alg.AddVertex();

                    sw.Reset();

                    //force gc
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    foreach (var segment in segments)
                    {
                        sw.Start();
                        foreach (var edge in segment)
                        {
                            alg.AddEdge(edge.from, edge.to);
                        }

                        //var top = alg.Topology();
                        sw.Stop();
                        values.Add(sw.ElapsedTicks);
                    }

                    allValues.Add(values);
                }


                //return new MinAvgMax(values.Min(), (long)values.Average(), values.Max());
                string algName = alg.GetType().ToString().Substring(alg.GetType().ToString().LastIndexOf(".") + 1);
                measurements.Add(new Tuple<string, List<List<long>>>(algName, allValues));
                
            }

            
            var output = new List<List<string>>();
            for (int i = 0; i < measurements.Count; i++)
            {
                var curr = new List<string>();
                curr.Add(measurements[i].Item1);
                for (int segm = 0; segm < measurements[i].Item2[0].Count; segm++)
                {
                    var tot = 0.0;
                    for (int filenr = 0; filenr < measurements[i].Item2.Count; filenr++)
                    {
                        tot += measurements[i].Item2[filenr][segm];
                    }
                    curr.Add((tot / (long)measurements[i].Item2[0].Count).ToString());
                }
                output.Add(curr);
            }



            var filename = baseDir + $"measurements{DateTime.Now.Hour}-{DateTime.Now.Minute}-{DateTime.Now.Second}.txt";

       
            var lines = new List<string>();
            for (int i = 0; i < output.Count; i++)
            {
                lines.Add(output[i].Aggregate((a, b) => a + "\t" + b));
            }

            File.WriteAllLines(filename, lines);


        }


        public void RunDirectoryPerGraph(string baseDir, List<IStaticGraph> statics, List<IDynamicGraph> dynamics)
        {
            if (!Directory.Exists(baseDir))
                throw new InvalidDataException("Input folder does not exist");
            
            var directories = Directory.GetDirectories(baseDir).ToList();
            directories = directories.OrderByDescending(path => int.Parse(path.Substring(path.LastIndexOf("-")))).ToList();
            
            var measurements = new List<Tuple<string, List<long>>>();


            foreach (var alg in statics)
            {
                string algName = alg.GetType().ToString().Substring(alg.GetType().ToString().LastIndexOf("."));

                var aMin = new Tuple<string, List<long>>($"{algName}-Min", new List<long>());
                var aAvg = new Tuple<string, List<long>>($"{algName}-Avg", new List<long>());
                var aMax = new Tuple<string, List<long>>($"{algName}-Max", new List<long>());
                measurements.Add(aMin);
                measurements.Add(aAvg);
                measurements.Add(aMax);

                foreach (var dir in directories)
                {
                    Console.WriteLine($"{algName}\n{dir}");
                    var minavgmax = RunFolderStaticGraph(dir, alg);
                    aMin.Item2.Add(minavgmax.Min);
                    aAvg.Item2.Add(minavgmax.Avg);
                    aMax.Item2.Add(minavgmax.Max);
                }
            }

            foreach (var alg in dynamics)
            {
                string algName = alg.GetType().ToString().Substring(alg.GetType().ToString().LastIndexOf(".")+1);

                var aMin = new Tuple<string, List<long>>($"{algName}-Min", new List<long>());
                var aAvg = new Tuple<string, List<long>>($"{algName}-Avg", new List<long>());
                var aMax = new Tuple<string, List<long>>($"{algName}-Max", new List<long>());
                measurements.Add(aMin);
                measurements.Add(aAvg);
                measurements.Add(aMax);

                foreach (var dir in directories)
                {
                    Console.WriteLine($"{algName}\n{dir}");
                    var minavgmax = RunFolderDynamicGraph(dir, alg);
                    aMin.Item2.Add(minavgmax.Min);
                    aAvg.Item2.Add(minavgmax.Avg);
                    aMax.Item2.Add(minavgmax.Max);
                }
            }

            WriteFolderMeasurements(baseDir + $"measurements{DateTime.Now.Hour}-{DateTime.Now.Minute}-{DateTime.Now.Second}.txt", measurements);

        }

        private void WriteFolderMeasurements(string filename, List<Tuple<string, List<long>>> ms)
        {
            var lines = new List<string>();
            foreach (var m in ms)
            {
                lines.Add($"{m.Item1}\t{m.Item2.ConvertAll(Convert.ToString).Aggregate((a,b) => a + "\t" + b)}");
            }

            File.WriteAllLines(filename, lines);
        }

        private MinAvgMax RunFolderStaticGraph(string folder, IStaticGraph alg)
        {
            var values = new List<long>();
            var sw = new Stopwatch();

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                alg.ResetAll();
                var graph = readFile(file);
                
                for(int i = 0; i < graph.n; i++)
                    alg.AddVertex();

                sw.Reset();
                sw.Start();
                foreach (var edge in graph.edges)
                {
                    alg.AddEdge(edge.from, edge.to);
                }

                var top = alg.TopoSort();
                sw.Stop();
                values.Add(sw.ElapsedTicks);
            }
            
            return new MinAvgMax(values.Min(), (long)values.Average(), values.Max());
        }

        private MinAvgMax RunFolderDynamicGraph(string folder, IDynamicGraph alg)
        {
            var values = new List<long>();
            var sw = new Stopwatch();

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var graph = readFile(file);
                alg.ResetAll(graph.n, graph.m);

                for(int i = 0; i < graph.n; i++)
                    alg.AddVertex();

                sw.Reset();

                //force gc               
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                sw.Start();
                foreach (var edge in graph.edges)
                {
                    alg.AddEdge(edge.from, edge.to);                    
                }

                var top = alg.Topology();
                sw.Stop();
                values.Add(sw.ElapsedTicks);
            }

            return new MinAvgMax(values.Min(), (long)values.Average(), values.Max());
        }

        

        // @Rawling, stackoverflow
        private static List<List<T>> Transpose<T>(List<List<T>> lists)
        {
            var longest = lists.Any() ? lists.Max(l => l.Count) : 0;
            List<List<T>> outer = new List<List<T>>(longest);
            for (int i = 0; i < longest; i++)
                outer.Add(new List<T>(lists.Count));
            for (int j = 0; j < lists.Count; j++)
                for (int i = 0; i < longest; i++)
                    outer[i].Add(lists[j].Count > i ? lists[j][i] : default(T));
            return outer;
        }

        private void createGraph(string fileName, List<Measurements> measurements, int n, int m, int repeateCount)
        {
            Chart chart = new Chart();
            chart.ChartAreas.Add("area");
            chart.ChartAreas["area"].AxisX.Name = "Nr of edge";
            chart.ChartAreas["area"].AxisY.Name = "Ticks";
            chart.ChartAreas["area"].AxisY.Maximum = 15000; 
            // controls the resolution of the output file            
            chart.Width = 1920;
            chart.Height = 1080;

            chart.Titles.Add(string.Format("{0} nodes, {1} edges. Average of {2} runs", n, m, repeateCount));
            
            chart.Palette = ChartColorPalette.SeaGreen;

            
            foreach (var algorithm in measurements)
            {
                string algorithmName = algorithm.Name.Substring(algorithm.Name.LastIndexOf('.') + 1);

                chart.Series.Add(algorithmName);
                chart.Series[algorithmName].ChartType = SeriesChartType.Line;
                chart.Legends.Add(algorithmName);

                foreach (long tick in algorithm.averages)
                {
                    chart.Series[algorithmName].Points.AddY(tick);
                }                              
            }
            
            chart.SaveImage(fileName + ".png", ChartImageFormat.Png);
        }

        private void writeMeasurements(string fileName, List<Measurements> measurements)
        {
            fileName = fileName + ".measurements";

            List<string> lines = new List<string>();
            
            foreach(var algorithm in measurements)
            {   
                lines.Add(algorithm.Name + ";" + algorithm.averages.ConvertAll<string>(item => item.ToString()).Aggregate((a, b) => a + ";" + b));
            }

            File.WriteAllLines(fileName, lines);
        }

        private InputFile readFile(string fileName)
        {
            string[] text = File.ReadAllLines(fileName);
            InputFile retValue = new InputFile();
            retValue.n = int.Parse(text[0].Split().First());
            retValue.m = int.Parse(text[0].Split().Skip(1).First());


            // split input and add to the graph list
            for (int i = 1; i < text.Length; i++)
            {
                var s = text[i].Split();
                retValue.edges.Add(new Pair(int.Parse(s.First()), int.Parse(s.Skip(1).First())));
            }

            return retValue;
        }

        // data object to keep elapsed times for each algorithm
        private class Measurements
        {
            public string Name { get; set; }
            public List<List<TimeSpan>> timeElapsed { get; set; }
            public List<long> averages { get; set; } // long is the datatype of tick

            private double averageTick { get; set; }
            private double maxTick { get; set; }
            private double minTick { get; set; }

            public Measurements(string name)
            {
                Name = name;
                timeElapsed = new List<List<TimeSpan>>();
                averages = new List<long>();
            }

            public void updateStatistics()
            {
                for(int edge = 0; edge < timeElapsed[0].Count; edge++)
                {
                    long sum = 0;
                    for(int runNumber = 0; runNumber < timeElapsed.Count; runNumber++)
                    {
                        sum += timeElapsed[runNumber][edge].Ticks;
                    }

                    averages.Add(sum / timeElapsed.Count);
                }

                averageTick = averages.Average();
                maxTick = averages.Max();
                minTick = averages.Min();
            }
        }

        // data object for file contents
        private class InputFile
        {
            public int n { get; set; } // #nodes
            public int m { get; set; } // #edges

            public List<Pair> edges { get; set; }

            public InputFile()
            {
                edges = new List<Pair>();
            }
        }

        private struct Pair
        {
            public int from { get; set; }
            public int to { get; set; }

            public Pair(int from, int to)
            {
                this.from = from;
                this.to = to;
            }
        }

        private struct MinAvgMax
        {
            public long Min { get; set; }
            public long Avg { get; set; }
            public long Max { get; set; }

            public MinAvgMax(long min, long avg, long max)
            {
                Min = min;
                Avg = avg;
                Max = max;
            }
        }
    }
}
