using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace ThirdPerson
{
    public sealed class VerbRule
    {
        public string Target;
        public bool Plural, Ambiguous;
    }
    public static class Lexicon
    {
        public static readonly Dictionary<string, VerbRule> Entries = Load();
        private static Dictionary<string, VerbRule> Load()
        {
            var entries = new Dictionary<string, VerbRule>(70000, StringComparer.Ordinal);
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ThirdPerson.Verbs.tsv"))
            {
                if (stream == null) throw new InvalidOperationException("В сборке отсутствует встроенный словарь глаголов.");
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0 || line[0] == '#') continue;
                        string[] parts = line.Split('\t');
                        VerbRule old;
                        if (entries.TryGetValue(parts[0], out old)) { old.Ambiguous = true; old.Target += " / " + parts[2]; }
                        else entries.Add(parts[0], new VerbRule { Plural = parts[1] == "P", Target = parts[2], Ambiguous = parts[3] == "1" });
                    }
                }
            }
            return entries;
        }
    }
}
