
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TraceMatching.Model
{
    internal class TraceModel
    {
        /// <summary>
        /// The current hypothesis; the ith element of this contains the trace number that is hypothesised to match target #i.  Note that it may
        /// be a partial solution - the parameter 'toMatch' of the Solve method keeps track of which elements of the array are actually correctly
        /// filled in (e.g. if toMatch==4, then elements [0-3] contain valid trace-to-target mappings, and the other array elements contain
        /// values that aren't meaningful).
        /// </summary>
        internal static int[] HypothesisedMatches = null;

        /// <summary>
        /// Number of targets in the current problem
        /// </summary>
        internal static int NUM_TARGETS=0;

        /// <summary>
        /// Number of traces in the current problem
        /// </summary>
        internal static int NUM_TRACES = 0;

        /// <summary>
        /// Tuples of X and Y values, being the coodinates of the targets
        /// </summary>
        internal static Tuple<int, int>[] TargetCoordinates=null;

        /// <summary>
        /// Tuples of X and Y values, being the coodinates of the targets
        /// </summary>
        internal static Tuple<int, int>[] TraceCoordinates = null;

        /// <summary>
        /// A 2 dimensional array, Distance[i,j] will be filled in with the distance from target #1 to trace #j
        /// </summary>
        internal static int[,] Distance = null;

        /// <summary>
        /// The lowest aggregate distance (total trace-to-target distance) that the solver has found.  It will be set
        /// to int.MaxValue as long as no solution has been found yet.
        /// </summary>
        internal static int BestTotalDistance;

        /// <summary>
        /// The best solution that the solver has found so far (best = having the lowest total trace-to-target distance)
        /// </summary>
        internal static int[] BestSolutionFound;

        /// <summary>
        /// Counts how many times the Solve(int toMatch, int totalDistance) function has been recurively called so far on the way
        /// to finding a solution.  The algorithm doesn't do anything with this infomation, it's just for information (though
        /// if you wanted to, you could use it to implement 'give up after X steps' logic).
        /// </summary>
        internal static int SolveSteps = 0;

        /// <summary>
        /// The external interface to the match-finder alogrithm.
        /// </summary>
        /// <param name="targets">A list of X,Y pairs, being the coordinates of the targets</param>
        /// <param name="traces">A list of X,Y pairs, being the coordinates of the traces</param>
        /// <param name="reporter">A callback method used to report the result (as an array of matches, and a string desciption).  The match array
        /// has a number of elements equal to the number of targets; the ith element contains the trace number that is matched target #i
        /// </param>
        internal static void Solve(List<Tuple<int,int>> targets, List<Tuple<int,int>> traces, Action<int[], string> reporter)
        {
            /*
            NUM_TARGETS = 5;
            NUM_TRACES = 5;

            TargetCoordinates = new Tuple<int, int>[NUM_TARGETS];
            TraceCoordinates = new Tuple<int, int>[NUM_TRACES];

            TargetCoordinates[4] = new Tuple<int, int>(0, 0);
            TargetCoordinates[3] = new Tuple<int, int>(1, 1);
            TargetCoordinates[2] = new Tuple<int, int>(2, 2);
            TargetCoordinates[1] = new Tuple<int, int>(3, 3);
            TargetCoordinates[0] = new Tuple<int, int>(4, 4);

            TraceCoordinates[0] = new Tuple<int, int>(3, 3);
            TraceCoordinates[1] = new Tuple<int, int>(4, 4);
            TraceCoordinates[2] = new Tuple<int, int>(5, 5);
            TraceCoordinates[3] = new Tuple<int, int>(6, 6);
            TraceCoordinates[4] = new Tuple<int, int>(7, 7);
            */
            /*
            NUM_TARGETS = 10;
            NUM_TRACES = 10;

            TargetCoordinates = new Tuple<int, int>[NUM_TARGETS];
            TraceCoordinates = new Tuple<int, int>[NUM_TRACES];

            for (int i = 0; i < 10; i++)
            {
                int x1 = System.Random.Shared.Next(0, 99);
                int y1 = System.Random.Shared.Next(0, 99);

                int x2 = x1+ System.Random.Shared.Next(-50, +50);
                int y2 = y1 + System.Random.Shared.Next(-50, +50);

                TargetCoordinates[i] = new Tuple<int, int>(x1, y1);
                TraceCoordinates[i] = new Tuple<int, int>(x2, y2);
            }
            */

            NUM_TARGETS = targets.Count;
            NUM_TRACES = traces.Count;

            TargetCoordinates = new Tuple<int, int>[NUM_TARGETS];
            TraceCoordinates = new Tuple<int, int>[NUM_TRACES];

            for(int i=0; i<NUM_TARGETS;i++)
                TargetCoordinates[i] = targets[i];

            for (int j = 0; j < NUM_TRACES; j++)
                TraceCoordinates[j] = traces[j];

            HypothesisedMatches = new int[NUM_TARGETS];
            BestSolutionFound = new int[NUM_TARGETS];

            Distance = new int[NUM_TARGETS, NUM_TRACES];

            for (int i = 0; i < NUM_TARGETS; i++)
            {
                for (int j = 0; j < NUM_TRACES; j++)
                {
                    int dx = TargetCoordinates[i].Item1 - TraceCoordinates[j].Item1;
                    int dy = TargetCoordinates[i].Item2 - TraceCoordinates[j].Item2;
                    Distance[i, j] = (int) Math.Sqrt(dx*dx+dy*dy);
                }
            }

            // This is where we actually kick off the recursive function call that will look for the best match.
            // First, initialise the hypothesis to 'nothing is matched'
            // Set the step counter back to zero
            SolveSteps = 0;
            // Start with 'best total trace-to-target distance' to 'none found yet'
            BestTotalDistance = int.MaxValue;
            // Call the recursive solver algorithm
            Solve(0,0);

            // The logging functionality opens a file; we close it here.
            if (Writer != null)
            {
                Writer.Close();
                Writer = null;
            }

            // The solver puts the solution into BestSolutionFound, and the corresponding total trace-to-target distance
            // into BestTotalDistance, and the number of steps taken into SolveSteps - we build a string describing it ...
            string resultString = SolutionToString(BestSolutionFound, BestTotalDistance,SolveSteps);

            // ... and then we invoke the reporter callback, to tell the ui about the solution
            reporter(BestSolutionFound, resultString);

        }
        /// <summary>
        /// Run the solver algorithm, which will deliver its result by writing the best solution (the one with the lowest total
        /// distance) to the array BestSolutionFound, with its corresponding total distance going into BestTotalDistance)
        /// </summary>
        /// <param name="toMatch">the target number that we will start solving from</param>
        /// <param name="totalDistance">the sum of trace-to-target distances for the targets already matched in this solution hypothesis</param>
        private static void Solve(int toMatch, int totalDistance)
        {
            SolveSteps++;

            if (totalDistance > BestTotalDistance)
            {
                // We already found a better solution that this one - we can abandon this branch of the search because it can't
                // produce a better answer than the one we already found before.
                return;
            }

            // If we've found a match for all the targets, this is a solution that we should keep, until/unless we find a better one;
            // we copy it into the BestSolutionFound array, and update BestTotalDistance to reflect this new solution's total
            // trace-to-target distance.
            if (toMatch== NUM_TARGETS)
            {
                BestTotalDistance = totalDistance; // any solutions worse that this one will be abandoned part way through
                for (int i = 0; i < NUM_TARGETS; i++)
                    BestSolutionFound[i] = HypothesisedMatches[i];

                // log the solution to the debug file
                LogSolution(totalDistance, SolveSteps);

                // now return; this branch of the search is finished.
                return;
            }

            // First, find the (not yet matched) trace that's closest to target #toMatch
            // (because we'll begin the search by trying matching that one)
            int bestDistance = Int32.MaxValue;
            int bestTrace = -1;
            for (int j = 0; j < NUM_TRACES; j++)
            {
                if (TraceAlreadyHypothesised(j, toMatch)) // ignore traces that have already been matched in the current hypothesis
                    continue;

                if (Distance[toMatch,j]<bestDistance)
                {
                    bestDistance = Distance[toMatch,j];
                    bestTrace = j;
                }
            }
            // We search for solutions that start by matching target #toMatch with trace #bestTrace (the one having the lowest distance)
            // Note that there is an edge-case where there is no possible trace, because they've all been hypothesised already, yet we have some
            // targets left over (i.e. the algorithm was supplied with too few traces).
            // In that case we'll report int.MaxValue as the best distance, as a hint that the algorithm can't give an optimal answer,
            // because of the way it works (it works through the targets starting with the first and matching them one after the other;
            // that means that if it runs out of traces it will not experiment with un-matching one of the lower numbered targets)
            HypothesisedMatches[toMatch] = bestTrace;
            Solve(toMatch + 1, bestTrace == -1 ? int.MaxValue : totalDistance + bestDistance); // having matched trace #bestTrace with target #toMatch, we now try to extend the current hypothesis

            // Next we search for solutions that start by matching target #toMatch with all the other traces except the one with the lowest distance
            for (int j=0; j < NUM_TRACES; j++)
            {
                if (TraceAlreadyHypothesised(j, toMatch)) // ignore traces that have already been matched in the current hypothesis
                    continue;

                if (j == bestTrace) // because we generated solutions for the best trace already, we skip this one now
                    continue;

                // Now search for solutions that start by matching target #toMatch with trace #j
                HypothesisedMatches[toMatch] = j; 
                Solve(toMatch + 1, totalDistance + Distance[toMatch,j]); // having matched trace #j with target #toMatch, we now try to extend the current hypothesis
            }
        }

        /// <summary>
        /// The method looks at the traces that have been matched so far (the parameter tells us how far into the search we have got - i.e
        /// how many targets we have matched so far).  The method checks whether trace #trace has already been matched to some
        /// target, in the current hypothesis.
        /// </summary>
        /// <param name="trace">the index number of the trace to be checked</param>
        /// <param name="soFar">how many targets we have matched so far</param>
        /// <returns>true if trace has already been matched in the currently active hypothesis</returns>
        private static bool TraceAlreadyHypothesised(int trace, int soFar)
        {
            for(int i=0; i<soFar;i++)
            {
                if (HypothesisedMatches[i] == trace)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The file we write the debug log to
        /// </summary>
        private static StreamWriter Writer = null;

        /// <summary>
        /// Write the current solution to the log file
        /// </summary>
        /// <param name="totalDistance"></param>
        /// <param name="stepCount"></param>
        private static void LogSolution(int totalDistance, int stepCount)
        {
            if(Writer==null)
            {
                string outFilePath = Path.Combine(FileSystem.Current.CacheDirectory, "tracematchlog.txt");
                FileStream stream = new FileStream(outFilePath, FileMode.Create);
                Writer = new StreamWriter(stream);
            }

            string resultString = SolutionToString(HypothesisedMatches, totalDistance, stepCount);
            
            resultString += "\n";
            Writer.Write(resultString);
        }

        /// <summary>
        /// Turn a solution into a user-readble string
        /// </summary>
        /// <param name="s">the solution</param>
        /// <param name="d">the solution's total trace-to-target distance</param>
        /// <param name="c">the number of steps taken</param>
        /// <returns></returns>
        private static string SolutionToString(int[] s, int d, int c)
        {
            bool aTargetIsUnmatched = false;
            string resultString = "";
            for (int i = 0; i < NUM_TARGETS; i++)
            {
                if (i != 0)
                    resultString += ", ";

                if (s[i] == -1)
                {
                    resultString += "[UNMATCHED " + i.ToString() + "]";
                    aTargetIsUnmatched = true;
                }
                else
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

            resultString += " after " + c.ToString() + " steps";

            // Thw algorithm works by workiong targets in order, starting with target #0 and then working through higher numbered targets.
            // That means that if it has to choose to leave a target unmatched, it's always going to leave the highest-numbered targets unmatched
            // while matching all the lower numbered ones.  It won't consider solutions that leave one or more of the lower-numbered
            // targets unmatched, even if those would be better in terms of total distance,
            if (aTargetIsUnmatched)
                resultString += " *** WARNING: SOLUTION IS NOT OPTIMAL ***";

            return resultString;
        }
    }
}
