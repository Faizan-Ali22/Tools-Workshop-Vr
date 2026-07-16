// ─────────────────────────────────────────────────────────────────────────────
// PTTI Trade Training SDK — Tool Usage Tracker
// Records per-tool usage events for instructor review and student scoring.
// Capped at a configurable maximum to prevent unbounded memory growth on Quest.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PTTI.TradeTrainingSDK
{
    /// <summary>
    /// Lightweight usage recorder attached to every trade tool.
    /// Stores timestamped records of grabs, activations, correct/incorrect
    /// actions, and durations. Serialisable to JSON for export.
    /// </summary>
    public class ToolUsageTracker : MonoBehaviour
    {
        // ── Serialisable Record Struct ──
        [Serializable]
        public struct UsageRecord
        {
            public string toolName;
            public string actionType;   // "Grab", "Activate", "CorrectUse", "IncorrectUse", etc.
            public bool   wasCorrect;
            public float  timestamp;    // Time.time when the event occurred
            public float  duration;     // Seconds (0 for instant events)
        }

        [Tooltip("Max stored records. Oldest are dropped when full (FIFO).")]
        [SerializeField] private int maxRecords = 200;

        // ── Internal Storage ──
        private readonly List<UsageRecord> records = new List<UsageRecord>();

        // Cached counters — avoids iterating the list for common queries.
        private int   correctCount;
        private int   incorrectCount;
        private float totalGripTime;

        // ── Public Read-Only API ──
        public IReadOnlyList<UsageRecord> Records       => records;
        public int   CorrectCount   => correctCount;
        public int   IncorrectCount => incorrectCount;
        public float TotalGripTime  => totalGripTime;

        /// <summary>Accuracy as a 0–100 percentage. Returns 0 if no events recorded.</summary>
        public float AccuracyPercent
        {
            get
            {
                int total = correctCount + incorrectCount;
                return total > 0 ? (correctCount * 100f) / total : 0f;
            }
        }

        // ── Recording ──

        /// <summary>
        /// Record a tool usage event.
        /// </summary>
        /// <param name="toolName">Display name of the tool.</param>
        /// <param name="actionType">Event type string (e.g. "Grab", "CorrectUse").</param>
        /// <param name="wasCorrect">Whether the action was performed correctly.</param>
        /// <param name="duration">Duration in seconds (0 for instantaneous events).</param>
        public void RecordEvent(string toolName, string actionType, bool wasCorrect, float duration = 0f)
        {
            // Drop the oldest record when at capacity
            if (records.Count >= maxRecords)
                records.RemoveAt(0);

            records.Add(new UsageRecord
            {
                toolName   = toolName,
                actionType = actionType,
                wasCorrect = wasCorrect,
                timestamp  = Time.time,
                duration   = duration
            });

            // Update cached counters
            if (actionType == "CorrectUse")   correctCount++;
            if (actionType == "IncorrectUse") incorrectCount++;
            if (duration > 0f && actionType == "Grab") totalGripTime += duration;
        }

        /// <summary>Clear all records and reset counters.</summary>
        public void ClearRecords()
        {
            records.Clear();
            correctCount   = 0;
            incorrectCount = 0;
            totalGripTime  = 0f;
        }

        // ── Export ──

        /// <summary>Serialise all records to a JSON string for instructor export.</summary>
        public string ToJson()
        {
            var wrapper = new RecordListWrapper { records = new List<UsageRecord>(records) };
            return JsonUtility.ToJson(wrapper, true);
        }

        [Serializable]
        private class RecordListWrapper
        {
            public List<UsageRecord> records;
        }
    }
}
