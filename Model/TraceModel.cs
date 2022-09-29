
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace TraceMatching.Model
{
    internal class TraceModel
    {

        internal static int[] HypothesisedMatches = null; // ith element of this array contains the trace number that is hypothesised to match target #i.
        internal static int NUM_TARGETS=0;
        internal static int NUM_TRACES = 0;

        internal static Tuple<int, int>[] TargetCoordinates=null;
        internal static Tuple<int, int>[] TraceCoordinates = null;
        internal static int[,] Distance = null;

        internal static int BestTotalDistanceFoundSoFar;
        internal static int[] BestSolutionFound;

        internal static void Solve(Action<string> reporter)
        {
            NUM_TARGETS = 5;
            NUM_TRACES = 5;

            HypothesisedMatches = new int[NUM_TARGETS];
            BestSolutionFound = new int[NUM_TARGETS];
            
            TargetCoordinates = new Tuple<int, int>[NUM_TARGETS];
            TraceCoordinates = new Tuple<int, int>[NUM_TRACES];

            TargetCoordinates[4]=new Tuple<int, int>(0, 0);
            TargetCoordinates[3] = new Tuple<int, int>(1, 1);
            TargetCoordinates[2] = new Tuple<int, int>(2, 2);
            TargetCoordinates[1] = new Tuple<int, int>(3, 3);
            TargetCoordinates[0] = new Tuple<int, int>(4, 4);

            TraceCoordinates[0] = new Tuple<int, int>(3, 3);
            TraceCoordinates[1] = new Tuple<int, int>(4, 4);
            TraceCoordinates[2] = new Tuple<int, int>(5, 5);
            TraceCoordinates[3] = new Tuple<int, int>(6, 6);
            TraceCoordinates[4] = new Tuple<int, int>(7, 7);

            Distance = new int[NUM_TARGETS, NUM_TRACES];

            for (int i = 0; i < NUM_TARGETS; i++)
            {
                for (int j = 0; j < NUM_TRACES; j++)
                {
                    int dx = TargetCoordinates[i].Item1 - TraceCoordinates[j].Item1;
                    int dy = TargetCoordinates[i].Item2 - TraceCoordinates[j].Item2;
                    Distance[i, j] = dx*dx+dy*dy;
                }
            }

            BestTotalDistanceFoundSoFar = int.MaxValue;

            Solve(0,0);

            if (Writer != null)
            {
                Writer.Close();
                Writer = null;
            }

            string resultString = SolutionToString(BestSolutionFound, BestTotalDistanceFoundSoFar);

            reporter(resultString);

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="toMatch">the target number that we will start solving from</param>
        private static void Solve(int toMatch, int totalDistance)
        {

            if (totalDistance > BestTotalDistanceFoundSoFar)
            {
                // We already found a better solution that this one - we can abandon this branch of the search because it can't
                // produce a better answer than the one we already found.
                return;
            }

            // If we've found a match for all the targets, this is a solution that we should keep, until/unless we find a better one
            if (toMatch== NUM_TARGETS)
            {
                BestTotalDistanceFoundSoFar = totalDistance; // any solutions worse that this one will be abandoned part way through
                for (int i = 0; i < NUM_TARGETS; i++)
                    BestSolutionFound[i] = HypothesisedMatches[i];

                LogSolution(totalDistance);
                return;
            }

            // First, find the trace that's closest to target #toMatch
            // (because we'll begin the search by trying matching that)
            int bestDistance = Int32.MaxValue;
            int bestTrace = -1;
            for (int j = 0; j < NUM_TRACES; j++)
            {
                if (TraceAlreadyHypothesised(j, toMatch))
                    continue;

                if (Distance[toMatch,j]<bestDistance)
                {
                    bestDistance = Distance[toMatch,j];
                    bestTrace = j;
                }
            }
            // Now search for solutions that start by matching target #toMatch with trace #bestTrace (the one having the lowest distance)
            HypothesisedMatches[toMatch] = bestTrace;
            Solve(toMatch + 1, totalDistance + bestDistance);

            // Now we search for solutions that start by matching target #toMatch with all the other traces except the one with the lowest distance
            for (int j=0; j < NUM_TRACES; j++)
            {
                if (TraceAlreadyHypothesised(j, toMatch))
                    continue;

                if (j == bestTrace) // because we generated solutions for the best trace
                    continue;

                HypothesisedMatches[toMatch] = j;

                Solve(toMatch + 1, totalDistance + Distance[toMatch,j]);
            }
        }

        private static bool TraceAlreadyHypothesised(int trace, int soFar)
        {
            for(int i=0; i<soFar;i++)
            {
                if (HypothesisedMatches[i] == trace)
                    return true;
            }
            return false;
        }

        private static StreamWriter Writer = null;
        private static void LogSolution(int totalDistance)
        {
            if(Writer==null)
            {
                string outFilePath = Path.Combine(FileSystem.Current.CacheDirectory, "tracematchlog.txt");
                FileStream stream = new FileStream(outFilePath, FileMode.Create);
                Writer = new StreamWriter(stream);
            }

            string resultString = SolutionToString(HypothesisedMatches, totalDistance);
            resultString += "\n";
            Writer.Write(resultString);
        }

        private static string SolutionToString(int[] s, int d)
        {
            string resultString = "";
            for (int i = 0; i < NUM_TARGETS; i++)
            {
                if (i != 0)
                    resultString += ",";
                resultString += "[" + s[i].ToString() + "-" + i.ToString() + "]";
            }
            resultString += " => " + d.ToString();

            string unmatchedTraces = "";
            for (int j = 0; j < NUM_TRACES; j++)
            {
                if (!s.Contains(j))
                {
                    if (!string.IsNullOrEmpty(unmatchedTraces))
                        unmatchedTraces += ", ";
                    unmatchedTraces += j.ToString();
                }
            }
            if (!string.IsNullOrEmpty(unmatchedTraces))
                resultString += "  (TRACES NOT MATCHED: " + unmatchedTraces + ")";

            return resultString;
        }

        
    }

    
}
