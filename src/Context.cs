using System;
using System.Collections.Generic;

namespace ThirdPerson
{
    // Bounded local patterns. A null replacement means a recognised noun, kept intact.
    // No model, remote call, inference about people, or unbounded sentence scan.
    public static class ContextRules
    {
        private static HashSet<string> Words(string value) { return new HashSet<string>(value.Split(' '), StringComparer.Ordinal); }
        private static readonly HashSet<string> FoodVerbs = Words("купил купила купили покупаю покупаем покупал покупала покупали приобрёл приобрел приобрела приобрели приобретаю взял взяла взяли беру брали готовлю готовим приготовил приготовила приготовили готовил готовила готовили ел ела ели ем едим съел съела съели принёс принес принесла принесли приношу заказал заказала заказали заказываю доставил доставила доставили доставляю получил получила получили получаю разогрел разогрела разогрели разогреваю");
        private static readonly HashSet<string> ObjectVerbs = Words("забрал забрала забрали забираю взял взяла взяли беру оставил оставила оставили оставляю потерял потеряла потеряли теряю нашёл нашел нашла нашли нахожу продал продала продали продаю купил купила купили покупаю видел видела видели вижу отдал отдала отдали отдаю передал передала передали передаю предъявил предъявила предъявили предъявляю проверил проверила проверили проверяю показал показала показали показываю осмотрел осмотрела осмотрели осматриваю обыскал обыскала обыскали обыскиваю изъял изъяла изъяли изымаю получил получила получили получаю снял сняла сняли снимаю");
        private static readonly HashSet<string> Payments = Words("аренду квартиру коммуналку услуги проезд парковку обучение кредит ипотеку налог налоги штраф штрафы алименты пошлину пошлины электричество газ воду интернет телефон связь покупку товар товары ремонт доставку");
        private static readonly HashSet<string> Directions = Words("в во на к ко из от до домой туда обратно вперёд вперед назад");
        private static readonly HashSet<string> LadyQuantifiers = Words("несколько много мало двух трёх трех четырёх четырех пяти шести семи восьми девяти десяти этих тех для среди");
        private static readonly HashSet<string> Future = Words("завтра послезавтра позже потом впоследствии");
        private static readonly HashSet<string> Repeated = Words("обычно всегда постоянно часто ежедневно");
        private static readonly HashSet<string> ThirdPredicates = ThirdForms();
        private static readonly HashSet<string> SubjectVerbs = Words("иду веду ношу варю");
        internal static bool TryLexical(string text, int end, string original, string word, string previous, string earlier, VerbRule rule, out string replacement)
        {
            replacement = null;
            // These common verbs have noun/name readings in the dictionary. Only resolve
            // after an explicit narrator subject, not after another predicate or a preposition.
            if (!SubjectVerbs.Contains(word) || rule.Plural || rule.Target.Contains(" / ") ||
                !(previous == "я" || previous == "не" && earlier == "я") ||
                char.IsUpper(original[0]) && original != original.ToUpperInvariant() || PredicateAhead(text, end)) return false;
            replacement = rule.Target; return true;
        }
        private static HashSet<string> ThirdForms()
        {
            var forms = Words("лежит лежат стоял стояла стояли остался осталась остались пропал пропала пропали принадлежит принадлежал принадлежала находился находилась находились был была были будет будут");
            foreach (VerbRule rule in Lexicon.Entries.Values)
                if (!rule.Ambiguous) forms.Add(rule.Target);
            return forms;
        }
        private static bool Letter(char c) { return char.IsLetter(c) || c == '\u0301'; }
        internal static string Next(string text, ref int position)
        {
            while (position < text.Length && (text[position] == ' ' || text[position] == '\t')) position++;
            int start = position;
            // A malformed or huge token cannot trigger a lengthy look-ahead.
            while (position < text.Length && position - start < 64 && Letter(text[position])) position++;
            if (position < text.Length && Letter(text[position])) return "";
            return text.Substring(start, position - start).ToLowerInvariant();
        }
        private static bool PredicateAhead(string text, int end)
        {
            // Skip at least the modified noun, at most three words, never punctuation/quotes/newlines.
            for (int n = 0; n < 4; n++) {
                string word = Next(text, ref end);
                if (word.Length == 0) return false;
                if (word == "и" || word == "но" || word == "или" || word == "как" || word == "что") return false;
                if (n > 0 && ThirdPredicates.Contains(word)) return true;
            }
            return false;
        }
        internal static bool TryResolve(string text, int end, string word, string previous, Options options, out string replacement, out string reason)
        {
            replacement = null; reason = "Выбор по ближайшему контексту";
            int p = end; string next = Next(text, ref p);
            if (word == "мой" || word == "мою" || word == "моем" || word == "моём") {
                bool prep = word == "моём" || word == "моем" ? WordsLocative.Contains(previous) : WordsAccusative.Contains(previous);
                if (next.Length > 0 && (prep || ObjectVerbs.Contains(previous) || PredicateAhead(text, end))) {
                    replacement = options.Female ? "её" : "его";
                    reason = "Притяжательное местоимение: предлог, предшествующий глагол или сказуемое после определяемого слова";
                    return true;
                }
                if (word == "мою" && (previous == "я" || previous == "не")) {
                    replacement = "моет"; reason = "«Мою» после «я / не»: глагол мыть"; return true;
                }
                if (word == "моем" && previous == "мы" && options.ConvertPlural) {
                    replacement = "моют"; reason = "«Мы моем»: глагол мыть, преобразование группы включено"; return true;
                }
                return false;
            }
            if (word == "еду") {
                if (FoodVerbs.Contains(previous) || ObjectVerbs.Contains(previous)) { reason = "«Еду» после глагола покупки, получения или действия с предметом: существительное сохранено"; return true; }
                if (Directions.Contains(next) && !PredicateAhead(text, end)) { replacement = "едет"; reason = "«Еду» перед направлением поездки: глагол ехать"; return true; }
            }
            if (word == "дам") {
                if (LadyQuantifiers.Contains(previous)) { reason = "«Дам» после количества / «для / среди»: существительное сохранено"; return true; }
                if (previous == "я" && !PredicateAhead(text, end)) { replacement = "даст"; reason = "«Я дам»: глагол дать"; return true; }
            }
            if (word == "плачу") {
                string payment = next == "за" ? Next(text, ref p) : next;
                // «Не плачу за аренду» keeps the negation; «плачу за погибшего» stays unresolved.
                if (Payments.Contains(payment)) { replacement = "платит"; reason = "«Плачу» рядом с названием платежа: глагол платить"; return true; }
                if (next == "навзрыд" || next == "горько" || next == "рыдая") { replacement = "плачет"; reason = "«Плачу» рядом с описанием плача"; return true; }
            }
            if (word == "узнаю") {
                if (Future.Contains(next)) { replacement = "узнает"; reason = "«Узнаю» перед указанием будущего времени"; return true; }
                if (Repeated.Contains(previous) || Repeated.Contains(next)) { replacement = "узнаёт"; reason = "«Узнаю» рядом с указанием повторяющегося действия"; return true; }
            }
            return false;
        }
        private static readonly HashSet<string> WordsLocative = Words("в во на о об обо при");
        private static readonly HashSet<string> WordsAccusative = Words("в во на за про через");
        internal static string[] Suggestions(string word, Options options, VerbRule lexical)
        {
            string possessive = options.Female ? "её" : "его";
            if (word == "мой") return new string[] { possessive };
            if (word == "мою") return new string[] { possessive, "моет" };
            if (word == "моем" || word == "моём") return new string[] { possessive, "моют" };
            if (word == "еду") return new string[] { "едет" };
            if (word == "дам") return new string[] { "даст" };
            if (word == "плачу") return new string[] { "платит", "плачет" };
            if (word == "узнаю") return new string[] { "узнаёт", "узнает" };
            if (word == "мы") return new string[] { "они" };
            if (word == "нас") return new string[] { "их", "них" };
            if (word == "нам") return new string[] { "им", "ним" };
            if (word == "нами") return new string[] { "ими", "ними" };
            if (word.StartsWith("наш", StringComparison.Ordinal)) return new string[] { "их" };
            return lexical == null ? new string[0] : lexical.Target.Split(new string[] { " / " }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
