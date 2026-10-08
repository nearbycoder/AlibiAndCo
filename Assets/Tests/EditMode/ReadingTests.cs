using AlibiCo.Logic;
using NUnit.Framework;

namespace AlibiCo.Tests
{
    /// <summary>
    /// How long a memo stays on the desk while others wait behind it (Logic.Reading): its reading time
    /// from when it starts typing, never less than the old 2.2 s beat once it's typed, and capped.
    /// </summary>
    public class ReadingTests
    {
        [Test]
        public void ALongMemoIsGivenItsReadingTime()
        {
            // 240 characters, Connie's longest: about 14 s, where the old rule gave 3.4 s of typing and 2.2 s.
            Assert.AreEqual(240 / 17f, Reading.TimeToRead(240), 1e-4);
            Assert.IsFalse(Reading.Read(240, 5.6f, 2.2f), "the old rule's moment is too soon");
            Assert.IsFalse(Reading.Read(240, 14.0f, 10.6f));
            Assert.IsTrue(Reading.Read(240, 14.2f, 10.8f));
        }

        [Test]
        public void AShortMemoStillGetsTheBeatAfterTyping()
        {
            // 30 characters read in under 2 s, but a typed memo always gets 2.2 s.
            Assert.Less(Reading.TimeToRead(30), 2f);
            Assert.IsFalse(Reading.Read(30, 2.5f, 2.0f));
            Assert.IsTrue(Reading.Read(30, 2.7f, 2.2f));
        }

        [Test]
        public void NoMemoHoldsTheQueueForever()
        {
            Assert.AreEqual(Reading.MaxOnDesk, Reading.TimeToRead(5000), 1e-4);
            Assert.IsTrue(Reading.Read(5000, Reading.MaxOnDesk, Reading.MaxOnDesk));
            Assert.AreEqual(0f, Reading.TimeToRead(-3), 1e-6);
        }
    }
}
