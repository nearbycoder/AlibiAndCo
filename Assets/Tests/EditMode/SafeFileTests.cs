using System.IO;
using System.Linq;
using AlibiCo.Logic;
using NUnit.Framework;

namespace AlibiCo.Tests
{
    /// <summary>
    /// The crash-safe save file (Logic/SafeFile): a write never leaves half a save behind, an
    /// unreadable save falls back to the backup, and nothing the player had is ever deleted.
    /// Works in a scratch folder inside the project's Temp/, never the real save.
    /// </summary>
    public class SafeFileTests
    {
        const string Good1 = "{\n  \"cases\": [ { \"id\": \"case1\", \"solved\": true } ],\n  \"seenIntro\": true\n}";
        const string Good2 = "{\n  \"cases\": [ { \"id\": \"case2\", \"solved\": true } ],\n  \"seenIntro\": true\n}";

        string dir, path;

        [SetUp]
        public void SetUp()
        {
            dir = Path.GetFullPath(Path.Combine("Temp", "alibi-safefile-tests"));
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "alibi_save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        string Read(out SafeFile.Source source) => SafeFile.Read(path, SafeFile.IsCompleteJsonObject, out source);
        string[] Files() => Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(f => f).ToArray();

        [Test]
        public void WriteThenReadKeepsThePreviousSaveAsBackup()
        {
            SafeFile.Write(path, Good1);
            Assert.AreEqual(Good1, Read(out var s1));
            Assert.AreEqual(SafeFile.Source.Main, s1);
            Assert.IsFalse(File.Exists(SafeFile.BackupPath(path)), "the first save has nothing to back up");

            SafeFile.Write(path, Good2);
            Assert.AreEqual(Good2, Read(out _));
            Assert.AreEqual(Good1, File.ReadAllText(SafeFile.BackupPath(path)));
            Assert.IsFalse(File.Exists(SafeFile.TempPath(path)), "the temporary file is renamed into place");
        }

        [Test]
        public void TruncatedSaveFallsBackToTheBackupAndIsKept()
        {
            SafeFile.Write(path, Good1);
            SafeFile.Write(path, Good2);
            File.WriteAllText(path, Good2.Substring(0, Good2.IndexOf('}') + 1));   // cut off right after an inner '}'

            Assert.AreEqual(Good1, Read(out var source));
            Assert.AreEqual(SafeFile.Source.Backup, source);
            Assert.IsFalse(File.Exists(path), "the unreadable save is moved aside");
            Assert.AreEqual(1, Files().Count(f => f.StartsWith("alibi_save.unreadable-")), string.Join(", ", Files()));

            // The next save must not copy anything broken over the good backup.
            SafeFile.Write(path, Good2);
            Assert.AreEqual(Good1, File.ReadAllText(SafeFile.BackupPath(path)));
            Assert.AreEqual(Good2, Read(out var again));
            Assert.AreEqual(SafeFile.Source.Main, again);
        }

        [Test]
        public void MissingSaveFallsBackToTheBackup()
        {
            File.WriteAllText(SafeFile.BackupPath(path), Good1);
            Assert.AreEqual(Good1, Read(out var source));
            Assert.AreEqual(SafeFile.Source.Backup, source);
        }

        [Test]
        public void BothUnreadableStartsBlankAndDeletesNothing()
        {
            File.WriteAllText(path, "");
            File.WriteAllText(SafeFile.BackupPath(path), "\0\0\0\0");
            Assert.IsNull(Read(out var source));
            Assert.AreEqual(SafeFile.Source.None, source);
            var files = Files();
            Assert.AreEqual(2, files.Length, string.Join(", ", files));
            Assert.IsTrue(files.All(f => f.Contains(".unreadable-")), string.Join(", ", files));
            Assert.IsTrue(files.Any(f => f.StartsWith("alibi_save.bak.unreadable-")), string.Join(", ", files));
        }

        [Test]
        public void StaleTemporaryFileFromACrashIsIgnored()
        {
            SafeFile.Write(path, Good1);
            File.WriteAllText(SafeFile.TempPath(path), "{\"cases\": [");
            Assert.AreEqual(Good1, Read(out var source));
            Assert.AreEqual(SafeFile.Source.Main, source);
            SafeFile.Write(path, Good2);
            Assert.AreEqual(Good2, Read(out _));
            Assert.IsFalse(File.Exists(SafeFile.TempPath(path)));
        }

        [Test]
        public void CompleteJsonCheck()
        {
            Assert.IsTrue(SafeFile.IsCompleteJsonObject(Good1));
            Assert.IsTrue(SafeFile.IsCompleteJsonObject("  {\"a\": \"} ] { [\", \"b\": \"say \\\"hi\\\"\"}\n"));
            Assert.IsFalse(SafeFile.IsCompleteJsonObject(null));
            Assert.IsFalse(SafeFile.IsCompleteJsonObject(""));
            Assert.IsFalse(SafeFile.IsCompleteJsonObject("   "));
            Assert.IsFalse(SafeFile.IsCompleteJsonObject("{\"a\": [1, 2}"));
            Assert.IsFalse(SafeFile.IsCompleteJsonObject("{\"a\": {}} }"));
            Assert.IsFalse(SafeFile.IsCompleteJsonObject("{\"a\": \"}"));
            Assert.IsFalse(SafeFile.IsCompleteJsonObject("{\"a\": 1}\0"));
            Assert.IsFalse(SafeFile.IsCompleteJsonObject("{\"a\": {\"b\": 1}"));
        }
    }
}
