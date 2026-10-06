using System;
using System.IO;

namespace AlibiCo.Logic
{
    /// <summary>
    /// Crash-safe text files (the save). A write goes to a temporary file, is flushed to disk and
    /// then renamed over the old one, so a crash leaves either the old save or the new one, never
    /// half of one. The previous save is kept as a backup. A file that can't be read is moved
    /// aside, never overwritten, and the backup is used instead.
    /// </summary>
    public static class SafeFile
    {
        public enum Source { None, Main, Backup }

        public static string BackupPath(string path) => path + ".bak";
        public static string TempPath(string path) => path + ".tmp";

        public static void Write(string path, string text)
        {
            var tmp = TempPath(path);
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var w = new StreamWriter(fs))
            {
                w.Write(text);
                w.Flush();
                fs.Flush(true);
            }
            if (File.Exists(path))
            {
                File.Copy(path, BackupPath(path), true);
                File.Replace(tmp, path, null);
            }
            else File.Move(tmp, path);
        }

        /// <summary>
        /// Read the file, or its backup if the file is missing or <paramref name="valid"/> rejects it.
        /// Rejected files are renamed to "name.unreadable-yyyyMMdd-HHmmss.ext" so nothing is lost.
        /// </summary>
        public static string Read(string path, Func<string, bool> valid, out Source source, Action<string> log = null)
        {
            source = Source.None;
            var main = TryRead(path, valid, log);
            if (main != null) { source = Source.Main; return main; }
            var bak = TryRead(BackupPath(path), valid, log);
            if (bak != null)
            {
                source = Source.Backup;
                log?.Invoke("loaded the backup save " + BackupPath(path));
                return bak;
            }
            return null;
        }

        /// <summary>
        /// True if the text is one complete JSON object: it starts with '{' and its braces and
        /// brackets (outside strings) close exactly at the last character. A save cut short by a
        /// crash fails this even when the cut happens to fall right after an inner '}'.
        /// </summary>
        public static bool IsCompleteJsonObject(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int start = 0, end = text.Length - 1;
            while (start <= end && char.IsWhiteSpace(text[start])) start++;
            while (end >= start && char.IsWhiteSpace(text[end])) end--;
            if (start > end || text[start] != '{' || text[end] != '}') return false;
            int depth = 0;
            bool inString = false;
            for (int i = start; i <= end; i++)
            {
                char ch = text[i];
                if (inString)
                {
                    if (ch == '\\') i++;
                    else if (ch == '"') inString = false;
                    continue;
                }
                if (ch == '"') inString = true;
                else if (ch == '{' || ch == '[') depth++;
                else if (ch == '}' || ch == ']')
                {
                    depth--;
                    if (depth < 0 || (depth == 0 && i != end)) return false;
                }
                else if (ch == '\0') return false;
            }
            return depth == 0 && !inString;
        }

        static string TryRead(string file, Func<string, bool> valid, Action<string> log)
        {
            if (!File.Exists(file)) return null;
            string text = null;
            bool ok;
            try
            {
                text = File.ReadAllText(file);
                ok = valid(text);
            }
            catch (Exception e)
            {
                log?.Invoke("couldn't read " + file + ": " + e.Message);
                ok = false;
            }
            if (ok) return text;
            MoveAside(file, log);
            return null;
        }

        static void MoveAside(string file, Action<string> log)
        {
            try
            {
                var dir = Path.GetDirectoryName(file) ?? "";
                var stem = Path.GetFileName(file).Replace(".json.bak", ".bak.json");
                var ext = Path.GetExtension(stem);
                var name = Path.GetFileNameWithoutExtension(stem) + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var dest = Path.Combine(dir, name + ext);
                for (int i = 2; File.Exists(dest); i++) dest = Path.Combine(dir, name + "-" + i + ext);
                File.Move(file, dest);
                log?.Invoke("moved an unreadable save aside to " + dest);
            }
            catch (Exception e) { log?.Invoke("couldn't move " + file + " aside: " + e.Message); }
        }
    }
}
