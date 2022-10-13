using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TraceMatching.Model
{
    internal class TraceModel
    {
        /// <summary>
        /// If this is set to true, then the algorithm will stop at the next step, without trying to further complete
        /// a solution.  Use this to interrupt it if you get bored waiting for it to finish.
        /// </summary>
        internal static bool HaltAfterNextStep = false;

        /// <summary>
        /// The external interface to the match-finder alogrithm.
        /// </summary>
        /// <param name="targets">A list of X,Y pairs, being the coordinates of the targets</param>
        /// <param name="traces">A list of X,Y pairs, being the coordinates of the traces</param>
        /// <param name="reporter">A callback method used to report the result (as an array of matches, and a string desciption, and a bool saying whether
        /// the algorithm has finished yet -false means this is an interim result).  The match array has a number of elements equal to the number of
        /// targets; the ith element contains the trace number that is matched target #i
        /// </param>
        internal static void Solve(List<Tuple<int, int>> targets, List<Tuple<int, int>> traces, Action<int[], string, bool> reporter)
        {
            // Construct the array of distances from targets to traces
            Problem p = new Problem(targets, traces);

            // Start with 'best total trace-to-target distance' to 'none found yet' (int.Maxalue means that there is
            // no solution so far - so any distance, no matter how large, is better than it.
            Hypothesis bestSolutionFound = new Hypothesis(p.numTargets, p.numTraces, int.MaxValue); 

            // This is where we actually kick off the recursive function call that will look for the best match.
            Solve(p, new Hypothesis(p.numTargets,p.numTraces, 0), bestSolutionFound, reporter);

            // The logging functionality opens a file; we close it here.
            if (Writer != null)
            {
                Writer.Close();
                Writer = null;
            }

            // The solver puts the solution into bestSolutionFound - we build a string describing it ...
            string resultString = bestSolutionFound.ToString();

            if (HaltAfterNextStep)
                resultString += " [*** INTERRUPTED BEFORE COMPLETION ***]";

            // ... and then we invoke the reporter callback, to tell the ui about the solution
            reporter(bestSolutionFound.matches, resultString, true);
        }

        /// <summary>
        /// The file we write the debug log to
        /// </summary>
        private static StreamWriter Writer = null;

        /// <summary>
        /// Write a hypothesis to the log file
        /// </summary>
        private static void LogSolution(Hypothesis h)
        {
            if (Writer == null)
            {
                string outFilePath = Path.Combine(FileSystem.Current.CacheDirectory, "tracematchlog.txt");
                FileStream stream = new FileStream(outFilePath, FileMode.Create);
                Writer = new StreamWriter(stream);
            }

            string resultString = h.ToString();
            resultString += "\n";
            Writer.Write(resultString);
        }

        /// <summary>Run the solver algorithm, which will deliver its result by copying the best solution (the one with the
        /// lowest total distance) to the hypothesis bestSoFar
        /// </summary>
        /// <param name="problem">the problem definition</param>
        /// <param name="currentHypothesis">the hypothesis that we're going to start with at this step of the algorithm</param>
        /// <param name="bestSoFar">the best solution found so far (if it has totalDistance==int.MaxValue
        /// that means no solution has been found yet</param>
        /// <param name="reporter">A callback method used to report the result</param>
        internal static void Solve(Problem problem, Hypothesis currentHypothesis, Hypothesis bestSoFar, Action<int[], string, bool> reporter)
        {
            currentHypothesis.stepCount++;

            // If the solver has been told to stop its work, then just return without doing anything;
            if (HaltAfterNextStep)
                return;

            // If the number of steps is divible by 100,000 then give an interim update on the solution
            if (currentHypothesis.stepCount % 100000 == 0)
            {
                // At any particular time, the solver will have its current best hypothesis in BestSolutionFound, and the
                // corresponding total trace-to-target distance in BestTotalDistance, and the number of steps taken in SolveSteps
                // - we build a string describing it ...
                string resultString = bestSoFar.ToString();
                // ... and then we invoke the reporter callback, to tell the ui about the solution.  The 'false' means that
                // it's an interim solution.
                reporter(bestSoFar.matches, resultString, false);
            }

            if (currentHypothesis.totalDistance > bestSoFar.totalDistance)
            {
                // We already found a better solution that this one - we can abandon this branch of the search because it can't
                // produce a better answer than the one we already found before.
                return;
            }

            // If we've found a match for all the targets, this is a solution that we should keep, until/unless we find a better one;
            // we copy it into bestSoFar.
            int nextToMatch=currentHypothesis.numberMatched;
            if (nextToMatch == problem.numTargets)
            {
                bestSoFar.CopyFrom(currentHypothesis); // any solutions worse that this one will be abandoned part way through

                // log the solution to the debug file
                LogSolution(bestSoFar);

                // now return; this branch of the search is finished.
                return;
            }

            // First, find the (not yet matched) trace that's closest to target #toMatch
            // (because we'll begin the search by trying matching that one)
            int bestDistance = Int32.MaxValue;
            int bestTrace = -1;
            for (int j = 0; j < problem.numTraces; j++)
            {
                if (currentHypothesis.TraceAlreadyHypothesised(j)) // ignore traces that have already been matched in the current hypothesis
                    continue;

                if (problem.distance[nextToMatch, j] < bestDistance)
                {
                    bestDistance = problem.distance[nextToMatch, j];
                    bestTrace = j;
                }
            }
            // We search for solutions that start by matching target #toMatch with trace #bestTrace (the one having the lowest distance)
            // Note that there is an edge-case where there is no possible trace, because they've all been hypothesised already, yet we have some
            // targets left over (i.e. the algorithm was supplied with too few traces).
            // In that case we'll report int.MaxValue as the best distance, as a hint that the algorithm can't give an optimal answer,
            // because of the way it works (it works through the targets starting with the first and matching them one after the other;
            // that means that if it runs out of traces it will not experiment with un-matching one of the lower numbered targets)
            currentHypothesis.matches[nextToMatch] = bestTrace;
            currentHypothesis.numberMatched = nextToMatch + 1;
            int currentHypothesisDistance = currentHypothesis.totalDistance;
            currentHypothesis.totalDistance = (bestTrace == -1 ? int.MaxValue : currentHypothesisDistance + bestDistance);
            Solve(problem, currentHypothesis, bestSoFar, reporter); // having matched trace #bestTrace with target #toMatch, we now try to extend the current hypothesis

            // Now put the current hypothesis back to how it was before the recursive call to the solver:
            currentHypothesis.numberMatched = nextToMatch;
            currentHypothesis.totalDistance = currentHypothesisDistance;

            // Next we search for solutions that start by matching target #toMatch with all the other traces except the one with the lowest distance
            for (int j = 0; j < problem.numTraces; j++)
            {
                if (currentHypothesis.TraceAlreadyHypothesised(j)) // ignore traces that have already been matched in the current hypothesis
                    continue;

                if (j == bestTrace) // because we generated solutions for the best trace already, we skip this one now
                    continue;

                // Now search for solutions that start by matching target #toMatch with trace #j
                currentHypothesis.matches[nextToMatch] = j;
                currentHypothesis.numberMatched = nextToMatch + 1;
                currentHypothesisDistance = currentHypothesis.totalDistance;
                currentHypothesis.totalDistance = currentHypothesisDistance + problem.distance[nextToMatch,j];
                Solve(problem, currentHypothesis, bestSoFar, reporter); // having matched trace #j with target #toMatch, we now try to extend the current hypothesis

                // Now put the current hypothesis back to how it was before the recursive call to the solver:
                currentHypothesis.numberMatched = nextToMatch;
                currentHypothesis.totalDistance = currentHypothesisDistance;
            }
        }
    }
    /// <summary>
    /// Represents the problem - the array of distances from targets to traces and (for convenience) the number
    /// of targets and the number of traces.
    /// </summary>
    internal class Problem
    {
        /// <summary>
        /// Build a problem definition, based on the provided coordinates for targets and traces
        /// </summary>
        /// <param name="targetCoordinates">Each tuple provides the coordinates of a target - Item1 is X, Item2 is Y</param>
        /// <param name="traceCoordinates">Each tuple provides the coordinates of a trace - Item1 is X, Item2 is Y</param>
        internal Problem(List<Tuple<int, int>> targetCoordinates, List<Tuple<int, int>> traceCoordinates)
        {
            this.numTargets = targetCoordinates.Count;
            this.numTraces = traceCoordinates.Count;

            this.distance = new int[this.numTargets, this.numTraces];

            for (int i = 0; i < this.numTargets; i++)
            {
                for (int j = 0; j < this.numTraces; j++)
                {
                    int dx = targetCoordinates[i].Item1 - traceCoordinates[j].Item1;
                    int dy = targetCoordinates[i].Item2 - traceCoordinates[j].Item2;
                    distance[i, j] = (int)Math.Sqrt(dx * dx + dy * dy);
                }
            }
        }

        /// <summary>
        /// Number of targets in the current problem
        /// </summary>
        internal int numTargets;

        /// <summary>
        /// Number of traces in the current problem
        /// </summary>
        internal int numTraces;

        /// <summary>
        /// A 2 dimensional array, distance[i,j] will be filled in with the distance from target #1 to trace #j
        /// </summary>
        internal int[,] distance;
    }

    /// <summary>
    /// Represents a hypothesis, consisting of a (perhaps) incomplete mapping of traces to targets.  The member variable
    /// 'numberMatched' keeps track of how many targets have been been matched; for each of those targets, the member variable
    /// 'matches[i]' is the trace number of the trace matched to that target.
    /// The class also keeps track of the total distance (i.e. for those targets that have been matched, the total of their
    /// target-to-trace distance).  If the total distance is int.MaxValue that means the hypothesis is not valid.
    /// The class also records the number of steps that the solver has taken, to arrive at this hypothesis.
    /// The class is expected to have exactly two instances - one used to represent the best solution we have found,
    /// and other to represent the hypothesis we're currently working on.
    /// </summary>
    internal class Hypothesis
    {
        /// <summary>
        /// Create an empty hypothesis, of a size to hold the specified number of traces
        /// </summary>
        /// <param name="_numTargets">number of targets the hypothesis has to match eventually</param>
        /// <param name="_numTraces">number of traces the hypothesis has to match eventually</param>
        /// <param name="_totalDistance">Use 0 to mean 'a partial solution to be expanded on'; use int.MaxValue
        /// to mean 'an invalid solution that we're trying to improve on</param>
        internal Hypothesis(int _numTargets, int _numTraces, int _totalDistance)
        {
            this.matches = new int[_numTargets];
            this.numTargets = _numTargets;
            this.numTraces = _numTraces;
            this.numberMatched = 0;
            this.totalDistance = _totalDistance;
            this.stepCount = 0;
        }

        /// <summary>
        /// Overwrites self by making a deep copy of the values from another hypothesis.
        /// We use this method to maintain the 'best solution found so far' object (we have to make a deep copy because the solver
        /// is continuously making changes to the current hypothesis, and we need to take a snapshot of it that will
        /// not get changed, except when we find a better solution).
        /// </summary>
        /// <param name="that"></param>
        internal void CopyFrom(Hypothesis that)
        {
            this.matches = new int[that.matches.Length];
            for (int i = 0; i < that.matches.Length; i++)
            {
                this.matches[i] = that.matches[i];
            }

            this.numTargets = that.numTargets;
            this.numTraces = that.numTraces;

            this.numberMatched = that.numberMatched;
            this.totalDistance = that.totalDistance;
            this.stepCount = that.stepCount;
        }

        /// The method looks at the traces that have been matched so far in this hypothesis.  The method checks whether
        /// trace #trace has already been matched to some target, in the hypothesis.
        /// </summary>
        /// <param name="trace">the index number of the trace to be checked</param>
        /// <returns>true if trace has already been matched in the hypothesis</returns>
        internal bool TraceAlreadyHypothesised(int trace)
        {
            for (int i = 0; i < this.numberMatched; i++)
            {
                if (this.matches[i] == trace)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Turn a solution into a user-readble string
        /// </summary>
        public override string ToString()
        {
            bool aTargetIsUnmatched = false;
            string resultString = "";
            for (int i = 0; i < this.numTargets; i++)
            {
                if (i != 0)
                    resultString += ", ";

                if (this.matches[i] == -1)
                {
                    resultString += "[UNMATCHED " + i.ToString() + "]";
                    aTargetIsUnmatched = true;
                }
                else
                    resultString += "[" + this.matches[i].ToString() + "-" + i.ToString() + "]";
            }
            resultString += " => " + this.totalDistance.ToString();

            string unmatchedTraces = "";
            for (int j = 0; j < this.numTraces; j++)
            {
                if (!this.matches.Contains(j))
                {
                    if (!string.IsNullOrEmpty(unmatchedTraces))
                        unmatchedTraces += ", ";
                    unmatchedTraces += j.ToString();
                }
            }
            if (!string.IsNullOrEmpty(unmatchedTraces))
                resultString += "  (TRACES NOT MATCHED: " + unmatchedTraces + ")";

            resultString += " after " + this.stepCount.ToString() + " steps";

            // The algorithm works by examining targets in order, starting with target #0 and then working through higher numbered targets.
            // That means that if it has to choose to leave a target unmatched, it's always going to leave the highest-numbered targets unmatched
            // while matching all the lower numbered ones.  It won't consider solutions that leave one or more of the lower-numbered
            // targets unmatched, even if those would be better in terms of total distance.
            if (aTargetIsUnmatched)
                resultString += " [*** WARNING: SOLUTION IS NOT OPTIMAL ***]";

            return resultString;
        }

        /// <summary>
        /// The ith element of this contains the trace number that is hypothesised to match target #i.  Note that it may
        /// be a partial solution - the member variable 'numberMatched' keeps track of which elements of the array are actually
        /// correctly filled in (e.g. if numberMatched==4, then elements [0-3] contain valid trace-to-target mappings, and the
        /// other array elements contain values that aren't meaningful).
        /// </summary>
        internal int[] matches;

        /// <summary>
        /// Number of targets to be matched (of them, numberMatched have been matched in the current hypothesis)
        /// </summary>
        internal int numTargets;

        /// <summary>
        /// Number of traces
        /// </summary>
        internal int numTraces;

        /// <summary>
        /// The current hypothesis; the ith element of this contains the trace number that is hypothesised to match target #i.  Note that it may
        /// be a partial solution - the parameter 'toMatch' of the Solve method keeps track of which elements of the array are actually correctly
        /// filled in (e.g. if toMatch==4, then elements [0-3] contain valid trace-to-target mappings, and the other array elements contain
        /// values that aren't meaningful).
        /// </summary>
        internal int numberMatched;

        /// <summary>The aggregate distance (total trace-to-target distance) in this hypothesis.  Note that it behaves differently 
        /// for the current solution and the best solution so far.  For the current solution, it will be initialised to 0,
        /// meaning 'I've matched no targets to traces' so the total distance is zero.  For the best solution so far, it will
        /// be initialised to int.MaxValue, meaning 'this isn't a valid solution; any valid solution will be better than this one'.
        /// </summary>
        internal int totalDistance;

        /// <summary>
        /// Counts how many times the Solve() function has been recurively called so far on the way
        /// to finding a solution.  The algorithm doesn't do anything with this information; it just uses ut for
        /// logging, and for deciding when to report intermediate results.
        /// If you wanted to, you could use it to implement 'give up after X steps' logic.
        /// </summary>
        internal int stepCount;
    }
}
