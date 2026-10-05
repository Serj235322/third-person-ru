using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace ThirdPerson
{
    // Local check only: an explicit Я followed by adverbs/particles and predicates.
    // An intervening name, other pronoun, object or punctuation ends this scope.
    // No gender rewrites are made: a wrong option must not change testimony facts.
    public sealed class AgreementTracker
    {
        private static readonly Dictionary<string, byte> Genders = Load();
        private static readonly HashSet<string> Modifiers = new HashSet<string>((
            "не ни же бы ли уже еще ещё тоже также только даже именно сам сама лично ранее прежде потом затем вчера сегодня завтра утром вечером ночью днем днём всегда никогда иногда часто редко обычно постоянно недавно давно сразу сначала наконец вновь снова действительно точно совершенно абсолютно очень немного сильно вполне совершенно практически почти случайно намеренно сознательно якобы вероятно возможно приблизительно там тут здесь дома однажды тогда теперь сейчас одновременно").Split(' '), StringComparer.Ordinal);
        private int state; // 0: no narrator subject; 1: expecting predicate; 2: predicate seen.
        public void Reset() { state = 0; }
        public bool Observe(string word, bool female, bool firstPersonVerb)
        {
            if (word == "я") { state = 1; return false; }
            if (state == 0) return false;
            byte genders;
            if (Genders.TryGetValue(word, out genders))
            {
                bool mismatch = (genders & (female ? 2 : 1)) == 0;
                state = 2; return mismatch;
            }
            if (Modifiers.Contains(word)) return false;
            if (state == 2 && (word == "и" || word == "но" || word == "или")) { state = 1; return false; }
            if (firstPersonVerb) { state = 2; return false; }
            state = 0; return false;
        }
        private static Dictionary<string, byte> Load()
        {
            var forms = new Dictionary<string, byte>(150000, StringComparer.Ordinal);
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ThirdPerson.Agreement.tsv"))
            {
                if (stream == null) throw new InvalidOperationException("Отсутствует словарь проверки рода.");
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null) {
                        if (line.Length == 0 || line[0] == '#') continue;
                        int tab = line.IndexOf('\t');
                        forms.Add(line.Substring(0, tab), byte.Parse(line.Substring(tab + 1), System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
            }
            return forms;
        }
    }
}
