using System;
using System.Collections.Generic;
using System.Text;

namespace ThirdPerson
{
    public sealed class Options
    {
        public bool Female;
        public bool ConvertPlural;
        public bool ProtectQuotes = true;
    }

    public sealed class Mark
    {
        public int SourceStart, SourceLength, OutputStart, OutputLength;
        public string Before, After, Reason;
        public string[] Suggestions = new string[0];
        public bool GenderMismatch;
    }

    public sealed class Conversion
    {
        public string Text;
        public int ChangeCount, IssueCount, ProtectedCount, GenderIssueCount;
        public int ContextCount, ReviewedCount;
        public readonly List<Mark> Changes = new List<Mark>();
        public readonly List<Mark> Issues = new List<Mark>();
        internal readonly List<Mark> GenderIssues = new List<Mark>();
    }

    // A deliberately finite rule engine. No suffix-based rewriting or inferred facts.
    public static class Engine
    {
        public const int MaximumCharacters = 5000000;
        public const int MaximumMarks = 2000;
        private static readonly HashSet<string> NPrepositions = Words(
            "без близ в во вместо внутри возле вокруг впереди для до за из к ко кроме между мимо на над надо напротив около от ото перед передо под подо после при про против с со у через о об обо по позади поверх помимо насчет насчёт относительно");
        private static readonly HashSet<string> Possessives = Words(
            "моя мое моё мои моего моей моему моим моими моих моем моём мою мой моею");
        private static readonly HashSet<string> Our = Words(
            "наш наша наше наши нашего нашей нашему нашим нашими наших нашем нашу нашею");
        private static readonly HashSet<string> Ambiguous = Words("мой мою моем моём еду дам плачу узнаю");
        private static readonly HashSet<string> FirstPlural = Words("мы нас нам нами");
        private static readonly HashSet<string> SpeechWords = Words("сказал сказала ответил ответила спросил спросила написал написала добавил добавила сообщил сообщила пояснил пояснила говорю скажу пишу отвечаю спрашиваю сообщаю поясняю");
        private static readonly HashSet<string> Past = Words(
            "был была видел видела приехал приехала пришел пришёл пришла ушел ушёл ушла взял взяла дал дала сказал сказала подумал подумала услышал услышала знал знала сделал сделала находился находилась работал работала жил жила получил получила понял поняла мог могла стал стала сел села встал встала имел имела хотел хотела объяснил объяснила заявил заявила сообщил сообщила указал указала просил просила вспомнил вспомнила заметил заметила считал считала шел шёл шла поехал поехала написал написала забыл забыла разговаривал разговаривала");
        public static int VerbCount { get { return Lexicon.Entries.Count; } }
        private static HashSet<string> Words(string text) { return new HashSet<string>(text.Split(' '), StringComparer.Ordinal); }

        internal static string CaseLike(string original, string replacement)
        {
            bool allUpper = true;
            foreach (char c in original) if (char.IsLetter(c) && !char.IsUpper(c)) { allUpper = false; break; }
            if (original.Length > 1 && allUpper) return replacement.ToUpperInvariant();
            if (char.IsUpper(original[0])) return char.ToUpperInvariant(replacement[0]) + replacement.Substring(1);
            return replacement;
        }
        private static bool WordChar(char c) { return char.IsLetter(c) || c == '\u0301'; }
        private static string PeekWord(string text, int pos)
        {
            while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\t')) pos++;
            int start = pos;
            while (pos < text.Length && WordChar(text[pos])) pos++;
            return text.Substring(start, pos - start).ToLowerInvariant();
        }
        private static string PrepReplacement(string word, string next, Options options)
        {
            bool possessive = (Possessives.Contains(next) && !Ambiguous.Contains(next)) || (options.ConvertPlural && Our.Contains(next));
            if (next == "мне" || possessive)
            {
                if (word == "ко") return "к";
                if (word == "во") return "в";
                if (word == "обо" || word == "об") return "о";
            }
            if (next == "мной" || next == "мною" || possessive)
            {
                if (word == "со") return "с";
                if (word == "надо") return "над";
                if (word == "передо") return "перед";
                if (word == "подо") return "под";
            }
            if ((next == "меня" || possessive) && word == "ото") return "от";
            return null;
        }
        private static string Personal(string word, string previous, Options o)
        {
            bool n = NPrepositions.Contains(previous);
            if (word == "я") return o.Female ? "она" : "он";
            if (word == "меня") return o.Female ? (n ? "неё" : "её") : (n ? "него" : "его");
            if (word == "мне")
            {
                bool locative = previous == "о" || previous == "об" || previous == "обо" || previous == "в" || previous == "во" || previous == "на" || previous == "при";
                return o.Female ? (n ? "ней" : "ей") : (locative ? "нём" : (n ? "нему" : "ему"));
            }
            if (word == "мной" || word == "мною") return o.Female ? (n ? "ней" : "ей") : (n ? "ним" : "им");
            if (Possessives.Contains(word)) return o.Female ? "её" : "его";
            if (o.ConvertPlural)
            {
                if (word == "мы") return "они";
                if (word == "нас") return n ? "них" : "их";
                if (word == "нам") return n ? "ним" : "им";
                if (word == "нами") return n ? "ними" : "ими";
                if (Our.Contains(word)) return "их";
            }
            return null;
        }
        private static void MarkChange(Conversion r, int s, int sl, int o, string before, string after, string reason)
        {
            r.ChangeCount++;
            if (r.Changes.Count < MaximumMarks) r.Changes.Add(new Mark { SourceStart = s, SourceLength = sl, OutputStart = o, OutputLength = after.Length, Before = before, After = after, Reason = reason });
        }
        private static void MarkIssue(Conversion r, int s, int sl, int o, int ol, string before, string reason, bool genderMismatch = false, string[] suggestions = null)
        {
            r.IssueCount++;
            if (genderMismatch) r.GenderIssueCount++;
            List<Mark> bucket = genderMismatch ? r.GenderIssues : r.Issues;
            if (bucket.Count < MaximumMarks) {
                string[] variants = suggestions ?? new string[0];
                for (int k = 0; k < variants.Length; k++) variants[k] = CaseLike(before, variants[k]);
                bucket.Add(new Mark { SourceStart = s, SourceLength = sl, OutputStart = o, OutputLength = ol, Before = before, After = "Без изменения", Reason = reason, GenderMismatch = genderMismatch, Suggestions = variants });
            }
        }

        public static Conversion Convert(string text, Options options, Func<bool> cancelled, Action<int> progress)
        {
            if (text == null) throw new ArgumentNullException("text");
            if (text.Length > MaximumCharacters) throw new ArgumentException("Максимум — 5 000 000 символов за один проход. Разделите текст на части.");
            Conversion result = new Conversion();
            StringBuilder output = new StringBuilder(text.Length + Math.Min(text.Length / 8, 500000));
            Stack<char> closers = new Stack<char>();
            AgreementTracker agreement = new AgreementTracker();
            int quoteStart = -1, quoteOutput = -1, nextProgress = 0, subjectWindow = 0;
            bool lineStart = true, dashLine = false;
            string previous = "", earlier = "";
            for (int i = 0; i < text.Length; )
            {
                if (i >= nextProgress)
                {
                    if (cancelled != null && cancelled()) throw new OperationCanceledException();
                    if (progress != null) progress(text.Length == 0 ? 100 : (int)((long)i * 100 / text.Length));
                    nextProgress = i + 8192;
                }
                char c = text[i];
                if (c == '\r' || c == '\n')
                {
                    output.Append(c); i++; lineStart = true; dashLine = false; previous = earlier = ""; subjectWindow = 0; agreement.Reset(); continue;
                }
                if (lineStart && (c == ' ' || c == '\t')) { output.Append(c); i++; continue; }
                if (lineStart)
                {
                    lineStart = false;
                    if (options.ProtectQuotes && closers.Count == 0 && (c == '—' || c == '–' || c == '-') && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
                    {
                        dashLine = true; result.ProtectedCount++;
                        MarkIssue(result, i, 1, output.Length, 1, "Реплика / пункт списка", "Строка с начальным тире сохранена. Проверьте, является ли она прямой речью.");
                    }
                }
                if (dashLine) { output.Append(c); i++; continue; }
                if (options.ProtectQuotes)
                {
                    if (closers.Count > 0 && c == closers.Peek())
                    {
                        closers.Pop(); output.Append(c); i++;
                        if (closers.Count == 0) { result.ProtectedCount++; previous = earlier = ""; subjectWindow = 0; quoteStart = -1; }
                        continue;
                    }
                    char close = c == '«' ? '»' : c == '“' || c == '„' ? (c == '„' ? '“' : '”') : c == '‘' ? '’' : c == '"' ? '"' : '\0';
                    if (close != '\0')
                    {
                        agreement.Reset();
                        if (closers.Count == 0) { quoteStart = i; quoteOutput = output.Length; }
                        closers.Push(close); output.Append(c); i++; continue;
                    }
                    if (closers.Count > 0) { output.Append(c); i++; continue; }
                }
                if (!WordChar(c))
                {
                    if (options.ProtectQuotes && c == ':') {
                        string nextWord = PeekWord(text, i + 1);
                        if (nextWord.Length > 0 && (SpeechWords.Contains(previous) || nextWord == "я" || nextWord == "мы")) {
                            dashLine = true; result.ProtectedCount++;
                            MarkIssue(result, i, 1, output.Length, 1, "Речь после двоеточия", "Возможная прямая речь без кавычек: остаток строки сохранён. Проверьте вручную.");
                        }
                    }
                    output.Append(c); i++;
                    if (!char.IsWhiteSpace(c)) { previous = earlier = ""; subjectWindow = 0; agreement.Reset(); }
                    continue;
                }
                int start = i;
                while (i < text.Length && WordChar(text[i])) i++;
                string original = text.Substring(start, i - start), word = original.ToLowerInvariant();
                VerbRule lexical; Lexicon.Entries.TryGetValue(word, out lexical);
                int outStart = output.Length;
                if (agreement.Observe(word, options.Female, lexical != null && !lexical.Plural))
                {
                    MarkIssue(result, start, original.Length, outStart, original.Length, original,
                        "Несовпадение рода рядом с «я»: «" + original + "» не соответствует выбранному " + (options.Female ? "женскому" : "мужскому") + " роду. Проверьте выбор рассказчика и согласование. Исходная форма сохранена.", true);
                }
                string replacement = null, reason = "Местоимение";
                if (Ambiguous.Contains(word))
                {
                    string contextReason;
                    if (ContextRules.TryResolve(text, i, word, previous, options, out replacement, out contextReason) &&
                        !(word == "мою" && replacement == "моет" && previous == "не" && earlier != "я")) {
                        reason = contextReason; result.ContextCount++;
                    } else {
                    replacement = null;
                    string ambiguousReason = word == "еду" ? "Может быть глаголом («едет») или существительным («еду»). Выберите вручную." :
                        word == "дам" ? "Может быть глаголом («даст») или существительным («дам»). Выберите вручную." :
                        word == "плачу" ? "Разное ударение и значение: «платит» или «плачет». Выберите вручную." :
                        word == "узнаю" ? "Разное ударение и время: «узнаёт» или «узнает». Выберите вручную." :
                        "Может быть местоимением или глаголом. Выберите вручную: «" + (options.Female ? "её" : "его") + "», «моет» или «моют» по смыслу.";
                    MarkIssue(result, start, original.Length, outStart, original.Length, original, ambiguousReason, false, ContextRules.Suggestions(word, options, lexical));
                    }
                }
                else if (!options.ConvertPlural && (FirstPlural.Contains(word) || Our.Contains(word) || (lexical != null && lexical.Plural)))
                {
                    MarkIssue(result, start, original.Length, outStart, original.Length, original,
                        "Первое лицо множественного числа сохранено: необходимо определить состав группы. Опция «Мы → они» включает известные формы.", false, ContextRules.Suggestions(word, options, lexical));
                }
                else if (lexical != null && lexical.Ambiguous)
                {
                    if (ContextRules.TryLexical(text, i, original, word, previous, earlier, lexical, out replacement)) {
                        reason = "Омонимичная глагольная форма после явного «я / я не»"; result.ContextCount++;
                    } else {
                    MarkIssue(result, start, original.Length, outStart, original.Length, original,
                        "Несколько словарных разборов или нестандартная форма. Возможный глагольный вариант: «" + lexical.Target + "». Проверьте значение и часть речи.", false, ContextRules.Suggestions(word, options, lexical));
                    }
                }
                else
                {
                    replacement = PrepReplacement(word, PeekWord(text, i), options);
                    if (replacement == null) {
                        int nextEnd = i; string next = ContextRules.Next(text, ref nextEnd);
                        string choice, contextReason;
                        if (Ambiguous.Contains(next) && Possessives.Contains(next) && ContextRules.TryResolve(text, nextEnd, next, word, options, out choice, out contextReason) && (choice == "его" || choice == "её"))
                            replacement = PrepReplacement(word, "моей", options);
                    }
                    if (replacement != null) reason = "Предлог перед местоимением";
                    else replacement = Personal(word, previous, options);
                    if (replacement == null && lexical != null) { replacement = lexical.Target; reason = "Глагол из локального словаря"; }

                    if (replacement == null && !Past.Contains(word) && ((subjectWindow > 0 && LooksLikeFirstPerson(word)) || word.EndsWith("юсь", StringComparison.Ordinal) || word.EndsWith("усь", StringComparison.Ordinal)))
                        MarkIssue(result, start, original.Length, outStart, original.Length, original, "Возможная неизвестная форма первого лица. Окончания автоматически не меняются; проверьте вручную.");
                }
                if (replacement != null)
                {
                    replacement = CaseLike(original, replacement);
                    output.Append(replacement);
                    if (replacement != original) MarkChange(result, start, original.Length, outStart, original, replacement, reason);
                }
                else output.Append(original);
                if (word == "я") subjectWindow = 6;
                else if (lexical != null || Past.Contains(word)) subjectWindow = 0; else if (subjectWindow > 0) subjectWindow--;
                earlier = previous; previous = word;
            }
            if (quoteStart >= 0)
            {
                result.ProtectedCount++;
                MarkIssue(result, quoteStart, 1, quoteOutput, 1, "Незакрытая кавычка", "Остаток текста после незакрытой кавычки сохранён. Исправьте кавычки в исходнике и повторите обработку.");
            }
            if (cancelled != null && cancelled()) throw new OperationCanceledException();
            // Gender mismatches must remain visible even after many lower-priority notices.
            result.Issues.InsertRange(0, result.GenderIssues);
            if (result.Issues.Count > MaximumMarks) result.Issues.RemoveRange(MaximumMarks, result.Issues.Count - MaximumMarks);
            result.GenderIssues.Clear();
            result.Text = output.ToString();
            if (progress != null) progress(100);
            return result;
        }
        private static bool LooksLikeFirstPerson(string word)
        {
            return word.Length > 3 && (word.EndsWith("юсь", StringComparison.Ordinal) || word.EndsWith("усь", StringComparison.Ordinal) || word.EndsWith("ю", StringComparison.Ordinal) || word.EndsWith("у", StringComparison.Ordinal) || word.EndsWith("ем", StringComparison.Ordinal) || word.EndsWith("им", StringComparison.Ordinal));
        }
    }
}
