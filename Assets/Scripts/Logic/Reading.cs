using System;

namespace AlibiCo.Logic
{
    /// <summary>
    /// How long a memo stays on the desk while others wait behind it: long enough to read at an
    /// ordinary pace, counted from the moment it starts typing, and never less than a short beat once
    /// it's fully typed. A memo with nothing behind it stays until the next one arrives.
    /// </summary>
    public static class Reading
    {
        /// <summary>About 185 words a minute.</summary>
        public const float CharsPerSecond = 17f;
        /// <summary>The beat a fully typed memo always gets (the whole wait, before round 10).</summary>
        public const float MinAfterTyped = 2.2f;
        /// <summary>However long a memo is, the next one isn't kept waiting longer than this.</summary>
        public const float MaxOnDesk = 16f;

        /// <summary>Seconds a memo of this many characters is given on the desk, from when it starts typing.</summary>
        public static float TimeToRead(int chars) => Math.Min(MaxOnDesk, Math.Max(0, chars) / CharsPerSecond);

        /// <summary>
        /// May the next memo replace this one? <paramref name="shown"/> is the time since it started
        /// typing, <paramref name="sinceTyped"/> the time since its last character appeared.
        /// </summary>
        public static bool Read(int chars, float shown, float sinceTyped) =>
            sinceTyped >= MinAfterTyped && shown >= TimeToRead(chars);
    }
}
