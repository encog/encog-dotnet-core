﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Encog.Util;
using Encog.ML.Bayesian.Table;

namespace Encog.ML.Bayesian.Query.Enumeration
{
    /// <summary>
    /// An enumeration query allows probabilistic queries on a Bayesian network.
    /// Enumeration works by calculating every combination of hidden nodes and using
    /// total probability. This results in an accurate deterministic probability.
    /// However, enumeration can be slow for large Bayesian networks. For a quick
    /// estimate of probability the sampling query can be used.
    /// </summary>
    public class EnumerationQuery : BasicQuery
    {
        /// <summary>
        /// The events that we will enumerate over.
        /// </summary>
        private IList<EventState> enumerationEvents = new List<EventState>();

        /// <summary>
        /// The calculated probability.
        /// </summary>
        private double probability;

        /// <summary>
        /// Construct the enumeration query.
        /// </summary>
        /// <param name="theNetwork">The Bayesian network to query.</param>
        public EnumerationQuery(BayesianNetwork theNetwork)
            : base(theNetwork)
        {
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public EnumerationQuery()
        {

        }

        /// <summary>
        /// Reset the enumeration events. Always reset the hidden events. Optionally
        /// reset the evidence and outcome.
        /// </summary>
        /// <param name="includeEvidence">True if the evidence is to be reset.</param>
        /// <param name="includeOutcome">True if the outcome is to be reset.</param>
        public void ResetEnumeration(bool includeEvidence, bool includeOutcome)
        {
            this.enumerationEvents.Clear();

            foreach (EventState state in this.Events.Values)
            {
                if (state.CurrentEventType == EventType.Hidden)
                {
                    this.enumerationEvents.Add(state);
                    state.Value = 0;
                }
                else if (includeEvidence
                      && state.CurrentEventType == EventType.Evidence)
                {
                    this.enumerationEvents.Add(state);
                    state.Value = 0;
                }
                else if (includeOutcome
                      && state.CurrentEventType == EventType.Outcome)
                {
                    this.enumerationEvents.Add(state);
                    state.Value = 0;
                }
                else
                {
                    state.Value = state.CompareValue;
                }
            }
        }

        /// <summary>
        /// Roll the enumeration events forward by one.
        /// </summary>
        /// <returns>False if there are no more values to roll into, which means we're
        /// done.</returns>
        public bool Forward()
        {
            int currentIndex = 0;
            bool done = false;
            bool eof = false;

            if (this.enumerationEvents.Count == 0)
            {
                done = true;
                eof = true;
            }

            while (!done)
            {

                EventState state = this.enumerationEvents[currentIndex];
                int v = (int)state.Value;
                v++;
                if (v >= state.Event.Choices.Count)
                {
                    state.Value = 0;
                }
                else
                {
                    state.Value = v;
                    done = true;
                    break;
                }

                currentIndex++;

                if (currentIndex >= this.enumerationEvents.Count)
                {
                    done = true;
                    eof = true;
                }
            }

            return !eof;
        }
        
        /// <summary>
        /// Obtain the arguments for an event.
        /// </summary>
        /// <param name="theEvent">The event.</param>
        /// <returns>The arguments.</returns>
        private int[] ObtainArgs(BayesianEvent theEvent)
        {
            int[] result = new int[theEvent.Parents.Count];

            int index = 0;
            foreach (BayesianEvent parentEvent in theEvent.Parents)
            {
                EventState state = this.GetEventState(parentEvent);
                result[index++] = state.Value;

            }
            return result;
        }

        /// <summary>
        /// Calculate the probability for a state.
        /// </summary>
        /// <param name="state">The state to calculate.</param>
        /// <returns>The probability.</returns>
        private double CalculateProbability(EventState state)
        {

            int[] args = ObtainArgs(state.Event);

            foreach (TableLine line in state.Event.Table.Lines)
            {
                if (line.CompareArgs(args))
                {
                    if (Math.Abs(line.Result - state.Value) < EncogFramework.DefaultDoubleEqual)
                    {
                        return line.Probability;
                    }
                }
            }

            throw new BayesianError("Could not determine the probability for "
                    + state.ToString());
        }
     
        /// <summary>
        /// Perform a single enumeration. 
        /// </summary>
        /// <returns>The result.</returns>
        private double PerformEnumeration()
        {
            double result = 0;

            do
            {
                bool first = true;
                double prob = 0;
                foreach (EventState state in this.Events.Values)
                {
                    if (first)
                    {
                        prob = CalculateProbability(state);
                        first = false;
                    }
                    else
                    {
                        prob *= CalculateProbability(state);
                    }
                }
                result += prob;
            } while (Forward());
            return result;
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            LocateEventTypes();
            ResetEnumeration(false, false);
            double numerator = PerformEnumeration();
            ResetEnumeration(false, true);
            double denominator = PerformEnumeration();
            this.probability = numerator / denominator;
        }

        /// <inheritdoc/>
        public override double Probability
        {
            get
            {
                return probability;
            }
        }

        /// <inheritdoc/>
        public String ToString()
        {
            StringBuilder result = new StringBuilder();
            result.Append("[SamplingQuery: ");
            result.Append(Problem);
            result.Append("=");
            result.Append(Format.FormatPercent(Probability));
            result.Append("]");
            return result.ToString();
        }

        /// <summary>
        /// Roll the enumeration events forward by one.
        /// </summary>
        /// <param name="enumerationEvents">The events to roll.</param>
        /// <param name="args">The arguments to roll.</param>
        /// <returns>False if there are no more values to roll into, which means we're
        ///         done.</returns>
        public static bool Roll(IList<BayesianEvent> enumerationEvents, int[] args)
        {
            int currentIndex = 0;
            bool done = false;
            bool eof = false;

            if (enumerationEvents.Count == 0)
            {
                done = true;
                eof = true;
            }

            while (!done)
            {
                BayesianEvent e = enumerationEvents[currentIndex];
                int v = (int)args[currentIndex];
                v++;
                if (v >= e.Choices.Count)
                {
                    args[currentIndex] = 0;
                }
                else
                {
                    args[currentIndex] = v;
                    done = true;
                    break;
                }

                currentIndex++;

                if (currentIndex >= args.Length)
                {
                    done = true;
                    eof = true;
                }
            }

            return !eof;
        }

        /// <summary>
        /// A clone of this object.
        /// </summary>
        /// <returns>A clone of this object.</returns>
        public override IBayesianQuery Clone()
        {
            return new EnumerationQuery(this.Network);
        }
    }
}
