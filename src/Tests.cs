using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ThirdPerson
{
    public static class Tests
    {
        private static int passed;
        private static void Check(bool value, string name)
        {
            if (!value) throw new Exception("FAIL: " + name);
            passed++;
        }
        private static void Example(string input, string expected, Options o, string name)
        {
            Conversion r = Engine.Convert(input, o, null, null);
            Check(r.Text == expected, name + ": ожидается «" + expected + "», получено «" + r.Text + "»");
            foreach (Mark m in r.Changes)
            {
                Check(input.Substring(m.SourceStart, m.SourceLength) == m.Before, name + " source mark");
                Check(r.Text.Substring(m.OutputStart, m.OutputLength) == m.After, name + " output mark");
            }
            foreach (Mark m in r.Issues)
                Check(m.SourceStart >= 0 && m.SourceStart + m.SourceLength <= input.Length && m.OutputStart >= 0 && m.OutputStart + m.OutputLength <= r.Text.Length, name + " issue bounds");
        }
        private static void GenderExample(string text, Options options, string[] expectedWords, string name)
        {
            Conversion r = Engine.Convert(text, options, null, null);
            Check(r.GenderIssueCount == expectedWords.Length, name + " gender issue count: " + r.GenderIssueCount);
            for (int i = 0; i < expectedWords.Length; i++) {
                Mark m = r.Issues[i];
                Check(m.Before == expectedWords[i], name + " marked word");
                Check(text.Substring(m.SourceStart, m.SourceLength) == expectedWords[i], name + " source span");
                Check(r.Text.Substring(m.OutputStart, m.OutputLength) == expectedWords[i], name + " output span and preserved gender");
                Check(m.Reason.Contains("Несовпадение рода"), name + " explicit explanation");
            }
        }
        public static int Run(string path)
        {
            StringBuilder log = new StringBuilder();
            try
            {
                Options male = new Options(), female = new Options { Female = true }, group = new Options { ConvertPlural = true };
                GenderExample("Я купил еду.", female, new string[] { "купил" }, "reported female mismatch");
                GenderExample(MainForm.DemoText, female, new string[] { "приехал", "видел", "купил" }, "exact screenshot sample");
                GenderExample(MainForm.FemaleDemoText, female, new string[0], "correct female sample");
                GenderExample(MainForm.DemoText, male, new string[0], "correct male sample");
                GenderExample("Я купила еду.", male, new string[] { "купила" }, "reverse mismatch");
                GenderExample("Я вчера не приехал и не видел.", female, new string[] { "приехал", "видел" }, "adverbs negation and coordination");
                GenderExample("Я был готов и должен приехать.", female, new string[] { "был", "готов", "должен" }, "past and short adjectives");
                GenderExample("Я была готова. Я должна приехать.", female, new string[0], "correct short adjectives");
                GenderExample("Я шёл и нёс. Я мог помочь.", female, new string[] { "шёл", "нёс", "мог" }, "irregular past verbs");
                GenderExample("Я помогал и возвращался.", female, new string[] { "помогал", "возвращался" }, "verbs outside old small list");
                GenderExample("Я помогала. Петров был готов. Мой телефон остался дома. Со мной была сестра.", female, new string[0], "other actors and objects not narrator");
                GenderExample("Я сказала, что Петров был готов. Он купил еду.", female, new string[0], "third person clause boundary");
                GenderExample("Я и Петров пришли. Я с Петровым был в магазине.", female, new string[0], "compound and uncertain subject scoped out");
                GenderExample("Петров сказал: «Я купил еду». Я приехала.", female, new string[0], "protected quoted speaker");
                GenderExample("«Я купил еду»", new Options { Female = true, ProtectQuotes = false }, new string[] { "купил" }, "explicitly unprotected quote");
                GenderExample("Я КУПИЛ еду.", female, new string[] { "КУПИЛ" }, "uppercase mismatch");
                GenderExample("Я было дома.", female, new string[] { "было" }, "neuter mismatch");
                StringBuilder earlierNotices = new StringBuilder();
                for (int k = 0; k < Engine.MaximumMarks + 100; k++) earlierNotices.Append("еду ");
                earlierNotices.Append("\nЯ купил еду.");
                GenderExample(earlierNotices.ToString(), female, new string[] { "купил" }, "gender priority after review cap");
                Example("", "", male, "empty");
                Example("Я приехал около 18 часов. Мне показалось, что Петров был нетрезвым.", "Он приехал около 18 часов. Ему показалось, что Петров был нетрезвым.", male, "uncertainty retained");
                Example("Я не видел, кто забрал 12 345 руб. 01.02.2025 в 18:30.", "Он не видел, кто забрал 12 345 руб. 01.02.2025 в 18:30.", male, "negation identifiers numbers preserved");
                Example("Я приехала. Меня встретили. Мне сообщили. Со мной была сестра.", "Она приехала. Её встретили. Ей сообщили. С ней была сестра.", female, "female and prepositions");
                Example("У меня был телефон. Он подошёл ко мне и говорил обо мне. Передо мной стоял стол.", "У него был телефон. Он подошёл к нему и говорил о нём. Перед ним стоял стол.", male, "n and preposition normalization");
                Example("Во мне, надо мной, подо мною, ото меня.", "В нём, над ним, под ним, от него.", male, "preposition variants");
                Example("Благодаря мне. Вопреки мне. Навстречу мне. Согласно мне. Вслед мне. Вне меня.", "Благодаря ему. Вопреки ему. Навстречу ему. Согласно ему. Вслед ему. Вне его.", male, "derived preposition exceptions");
                Example("Для меня, за меня, через меня, кроме меня, из-за меня, из-под меня.", "Для неё, за неё, через неё, кроме неё, из-за неё, из-под неё.", female, "accusative genitive after prepositions");
                Example("У моей сестры, в моём доме, ко моей сестре, со моими друзьями.", "У его сестры, в его доме, к его сестре, с его друзьями.", male, "possessives and ambiguous forms");
                Example("Моя сестра взяла мои документы. Я взял свою сумку и пошёл к себе.", "Его сестра взяла его документы. Он взял свою сумку и пошёл к себе.", male, "possessive reflexive distinction");
                Example("МЕНЯ ВСТРЕТИЛИ. МНЕ СООБЩИЛИ.", "ЕГО ВСТРЕТИЛИ. ЕМУ СООБЩИЛИ.", male, "uppercase");
                Example("я живу, работаю, учусь, могу, хочу и буду ждать.", "он живёт, работает, учится, может, хочет и будет ждать.", male, "known verbs and future");
                Example("Живу в Москве. Работаю водителем. Хожу пешком.", "Живёт в Москве. Работает водителем. Ходит пешком.", male, "omitted subject");
                Example("Я и Петров пришли. Он сказал, что я знаю адрес.", "Он и Петров пришли. Он сказал, что он знает адрес.", male, "coordination and embedded clause");
                Example("Я сказал: «Я ничего не брал». Я знаю его.", "Он сказал: «Я ничего не брал». Он знает его.", male, "quoted speech");
                Example("Петров сказал: сегодня я знаю ответ.\nЯ помню слова.", "Петров сказал: сегодня я знаю ответ.\nОн помнит слова.", male, "unquoted speech held for review");
                Example("Я сказал: \"Я знаю адрес\". Я помню.", "Он сказал: \"Я знаю адрес\". Он помнит.", male, "ascii quotes");
                Example("Я сказал: “Я знаю”. Я помню. Я сказал: „Я знаю“. Я помню.", "Он сказал: “Я знаю”. Он помнит. Он сказал: „Я знаю“. Он помнит.", male, "typographic quotes");
                Example("Я сказал: «Она ответила: \"Я не знаю\"». Я ушёл.", "Он сказал: «Она ответила: \"Я не знаю\"». Он ушёл.", male, "nested quotes");
                Example("Я сказал: «Я\r\nзнаю». Я помню.", "Он сказал: «Я\r\nзнаю». Он помнит.", male, "multiline quotes");
                Example("Я сказал: «Я знаю. Я помню.", "Он сказал: «Я знаю. Я помню.", male, "unclosed quote preserved");
                Check(Engine.Convert("«Я знаю", male, null, null).IssueCount == 1, "unclosed quote issue");
                Example("— Я знаю.\r\nЯ помню.\n  - Я не знаю.", "— Я знаю.\r\nОн помнит.\n  - Я не знаю.", male, "dash dialogue and whitespace");
                Example("Я — свидетель. Я знаю.", "Он — свидетель. Он знает.", male, "dash inside narration");
                Example("«Я знаю»", "«Он знает»", new Options { ProtectQuotes = false }, "explicit quote conversion");
                Example("Мы живём вместе. У нас наши вещи.", "Мы живём вместе. У нас наши вещи.", male, "plural held by default");
                Example("Мы живём вместе. У нас наши вещи. Нам сообщили. С нами пошли.", "Они живут вместе. У них их вещи. Им сообщили. С ними пошли.", group, "plural opt in");
                Example("Ко нашей сестре, со нашими друзьями.", "К их сестре, с их друзьями.", group, "plural possessive preposition normalization");
                Example("Я купил еду. Несколько дам вошли. Я плачу. Мой телефон. Я мою руки.", "Он купил еду. Несколько дам вошли. Он плачу. Мой телефон. Он моет руки.", male, "context resolution and genuinely unresolved homonyms");
                Check(Engine.Convert("еду дам плачу узнаю мой мою моем", male, null, null).IssueCount == 7, "one issue per ambiguous token");
                Example("Мой телефон остался дома. Мой адвокат работает. Я продал мою квартиру. Я мою квартиру. В моем доме. Во моём доме.", "Его телефон остался дома. Его адвокат работает. Он продал его квартиру. Он моет квартиру. В его доме. В его доме.", male, "possessive versus washing verb");
                Example("Я мою руки и Петров стоит рядом. Мой телефон! Мою квартиру, пожалуйста. Петров не мою машину взял.", "Он моет руки и Петров стоит рядом. Мой телефон! Мою квартиру, пожалуйста. Петров не мою машину взял.", male, "do not infer possession across coordination or imperative");
                Example("Я еду в Москву. Я не еду домой. Я купил еду в магазине. Я вижу еду в холодильнике. Я передал еду на кухню.", "Он едет в Москву. Он не едет домой. Он купил еду в магазине. Он видит еду в холодильнике. Он передал еду на кухню.", male, "travel versus food with direction prepositions");
                Example("Я плачу за аренду. Я не плачу налоги. Я плачу навзрыд. Я плачу за погибшего. Я плачу из-за боли. Я плачу.", "Он платит за аренду. Он не платит налоги. Он плачет навзрыд. Он плачу за погибшего. Он плачу из-за боли. Он плачу.", male, "payment versus crying and missing evidence");
                Example("Я дам показания. Для дам. Несколько дам вошли. Я узнаю завтра. Я обычно узнаю голос. Я узнаю его.", "Он даст показания. Для дам. Несколько дам вошли. Он узнает завтра. Он обычно узнаёт голос. Он узнаю его.", male, "noun verb and tense disambiguation");
                Example("Мы моем руки. В моем доме.", "Мы моем руки. В его доме.", male, "plural washing stays unresolved when group not defined");
                Example("Мы моем руки.", "Они моют руки.", group, "plural washing explicit opt in");
                Example("МОЙ телефон остался дома. Я ПЛАЧУ за аренду. Я еду к сестре. В моём доме.", "ЕЁ телефон остался дома. Она ПЛАТИТ за аренду. Она едет к сестре. В её доме.", female, "female casing in contextual replacements");
                Check(Engine.Convert(MainForm.DemoText, male, null, null).IssueCount == 1, "sample review reduced from four to one");
                Check(Engine.Convert(MainForm.FemaleDemoText, female, null, null).IssueCount == 1, "female sample review reduced from four to one");
                Check(Engine.Convert(MainForm.DemoText, female, null, null).GenderIssueCount == 3, "gender notices retained with contextual choices");
                Example("Я иду к магазину. Я веду допрос. Я ношу очки. Я не варю суп.", "Он идёт к магазину. Он ведёт допрос. Он носит очки. Он не варит суп.", male, "common lexical homonyms after explicit narrator");
                Example("Иду. Несколько варю. Я Иду встретил. Я лечу.", "Иду. Несколько варю. Он Иду встретил. Он лечу.", male, "no broad unlock of names or competing verb meanings");
                Example("Петров сказал: «Мой телефон остался дома. Я плачу за аренду. Я мою руки».", "Петров сказал: «Мой телефон остался дома. Я плачу за аренду. Я мою руки».", male, "context rules never touch protected quotes");
                ReviewTests(male, female);
                Example("Я экстраполирую результаты и приобщаю документы.", "Он экстраполирует результаты и приобщает документы.", male, "extended dictionary verbs");
                Conversion unknown = Engine.Convert("Я квазибезлексемирую результаты.", male, null, null);
                Check(unknown.Text == "Он квазибезлексемирую результаты." && unknown.IssueCount == 1, "unknown verb flagged no invented stem");
                Check(Engine.Convert("Я работаю водителем.", male, null, null).IssueCount == 0, "no spurious unknown after known verb");
                Check(Engine.VerbCount > 40000, "full compact dictionary loaded");
                Example("Я вижу Петрова. Петров: 123-456.\t\r\n\r\n№ 77/2025. C:\\case\\file.txt", "Он видит Петрова. Петров: 123-456.\t\r\n\r\n№ 77/2025. C:\\case\\file.txt", male, "exact whitespace and literal data");
                Example("Я-то думаю, что кто-то взял ящик. Семья, якорь, меняла, мойка.", "Он-то думает, что кто-то взял ящик. Семья, якорь, меняла, мойка.", male, "whole tokens");
                bool cancelled = false;
                try { Engine.Convert(new string('а', 10000), male, delegate { return true; }, null); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled, "cancellation");
                bool tooLarge = false;
                try { Engine.Convert(new string('а', Engine.MaximumCharacters + 1), male, null, null); } catch (ArgumentException) { tooLarge = true; }
                Check(tooLarge, "oversize rejected before conversion");
                string paragraph = "Я приехал 01.02.2025 около 18:30. Я не видел, кто забрал телефон. Мне показалось, что Петров был нетрезвым. Я живу в Москве. Мой телефон остался дома.\r\n";
                string expectedParagraph = "Он приехал 01.02.2025 около 18:30. Он не видел, кто забрал телефон. Ему показалось, что Петров был нетрезвым. Он живёт в Москве. Его телефон остался дома.\r\n";
                int repeat = Engine.MaximumCharacters / paragraph.Length;
                StringBuilder large = new StringBuilder(repeat * paragraph.Length);
                for (int n = 0; n < repeat; n++) large.Append(paragraph);
                string source = large.ToString(); large = null;
                GC.Collect(); GC.WaitForPendingFinalizers();
                Stopwatch time = Stopwatch.StartNew(); int previousProgress = -1;
                Conversion big = Engine.Convert(source, male, null, delegate(int p) { Check(p >= previousProgress && p <= 100, "monotonic progress"); previousProgress = p; }); time.Stop();
                Check(previousProgress == 100, "progress complete");
                Check(big.ChangeCount == repeat * 6 && big.IssueCount == 0 && big.ContextCount == repeat, "no blocks skipped in large document");
                Check(big.Changes.Count == Engine.MaximumMarks && big.Issues.Count == 0, "bounded review lists");
                Check(Engine.Convert(earlierNotices.ToString(), male, null, null).Issues.Count == Engine.MaximumMarks, "issue list capped when ambiguity persists");
                Check(big.Text.Length == repeat * expectedParagraph.Length, "large length");
                for (int n = 0; n < repeat; n++)
                    if (string.CompareOrdinal(big.Text, n * expectedParagraph.Length, expectedParagraph, 0, expectedParagraph.Length) != 0) throw new Exception("Large text mismatch at paragraph " + n);
                Check(true, "every large paragraph verified");
                using (Process process = Process.GetCurrentProcess())
                {
                    process.Refresh();
                    log.AppendLine("Engine benchmark: " + source.Length.ToString("N0") + " source characters; " + repeat.ToString("N0") + " paragraphs.");
                    log.AppendLine("Time: " + time.Elapsed.TotalSeconds.ToString("F2") + " seconds.");
                    log.AppendLine("Peak working set: " + (process.PeakWorkingSet64 / 1048576.0).ToString("F1") + " MiB; peak paged/private allocation: " + (process.PeakPagedMemorySize64 / 1048576.0).ToString("F1") + " MiB.");
                    log.AppendLine("Managed memory now: " + (GC.GetTotalMemory(false) / 1048576.0).ToString("F1") + " MiB.");
                }
                log.AppendLine("PASS: " + passed + " assertions. Known verb entries (including aliases): " + Engine.VerbCount + ".");
                log.AppendLine("Benchmark describes this host, not a measured 2 GB target PC. Review is required; the finite dictionary cannot detect every first-person verb.");
                File.WriteAllText(path, log.ToString(), new UTF8Encoding(true)); return 0;
            }
            catch (Exception ex)
            {
                log.AppendLine(ex.ToString()); File.WriteAllText(path, log.ToString(), new UTF8Encoding(true)); return 1;
            }
        }
        private static void ReviewTests(Options male, Options female)
        {
            string source = "Я плачу.\r\nЯ еду. Я узнаю. Мой телефон.";
            Conversion r = Engine.Convert(source, male, null, null);
            string initial = r.Text; int initialChanges = r.ChangeCount, initialIssues = r.IssueCount;
            ReviewDecision decision = ReviewEditor.Apply(r, 0, "платит");
            Check(r.Text == "Он платит.\r\nОн еду. Он узнаю. Мой телефон.", "apply chosen meaning");
            Check(r.ReviewedCount == 1 && r.IssueCount == initialIssues - 1 && r.ChangeCount == initialChanges + 1, "review counts");
            CheckSpans(r, source);
            ReviewEditor.Undo(r, decision);
            Check(r.Text == initial && r.IssueCount == initialIssues && r.ChangeCount == initialChanges && r.ReviewedCount == 0, "undo replacement and counts");
            CheckSpans(r, source);
            ReviewEditor.Apply(r, 0, "плачет");
            decision = ReviewEditor.Apply(r, 0, "едет"); CheckSpans(r, source);
            ReviewEditor.Undo(r, decision); CheckSpans(r, source);
            Check(r.Text.Contains("плачет") && r.Text.Contains("еду"), "undo only latest decision");
            decision = ReviewEditor.Apply(r, 0, null); CheckSpans(r, source);
            Check(!r.Issues.Exists(delegate(Mark m) { return m.Before == "еду"; }), "keep noun removes review notice");
            ReviewEditor.Undo(r, decision); CheckSpans(r, source);
            Check(r.Issues[0].Before == "еду", "undo keep restores notice");
            bool invalid = false;
            try { ReviewEditor.Apply(r, 0, "неизвестно"); } catch (ArgumentException) { invalid = true; }
            Check(invalid && r.Issues[0].Before == "еду", "unlisted suggestion rejected without mutation");
            Conversion shortForm = Engine.Convert("Моем. Я плачу.", male, null, null);
            decision = ReviewEditor.Apply(shortForm, 0, "Его"); CheckSpans(shortForm, "Моем. Я плачу.");
            ReviewEditor.Undo(shortForm, decision); CheckSpans(shortForm, "Моем. Я плачу.");
            Conversion gender = Engine.Convert("Я купил еду.", female, null, null);
            decision = ReviewEditor.Apply(gender, 0, null);
            Check(gender.GenderIssueCount == 0, "explicitly reviewed gender notice");
            ReviewEditor.Undo(gender, decision); Check(gender.GenderIssueCount == 1, "undo restores gender notice");
            string mixedSource = "Я плачу. Я купил еду.";
            Conversion mixed = Engine.Convert(mixedSource, female, null, null);
            Check(mixed.Issues[0].GenderMismatch && mixed.Issues[1].Before == "плачу", "gender-prioritised review is not in text order");
            decision = ReviewEditor.Apply(mixed, 1, "платит"); CheckSpans(mixed, mixedSource);
            Check(mixed.GenderIssueCount == 1 && mixed.Issues[0].Before == "купил", "context correction preserves later gender notice");
            ReviewEditor.Undo(mixed, decision); CheckSpans(mixed, mixedSource);
            StringBuilder many = new StringBuilder(); for (int i = 0; i < 2100; i++) many.Append("Я плачу. ");
            Conversion capped = Engine.Convert(many.ToString(), male, null, null);
            decision = ReviewEditor.Apply(capped, 0, "платит");
            Check(capped.IssueCount == 2099 && capped.Issues.Count == 1999, "hidden issues remain counted after review");
            ReviewEditor.Undo(capped, decision); Check(capped.IssueCount == 2100 && capped.Issues.Count == 2000, "undo with capped lists");
        }
        private static void CheckSpans(Conversion r, string source)
        {
            foreach (Mark m in r.Changes) {
                Check(source.Substring(m.SourceStart, m.SourceLength) == m.Before, "review source span");
                Check(r.Text.Substring(m.OutputStart, m.OutputLength) == m.After, "review output change span");
            }
            foreach (Mark m in r.Issues) Check(r.Text.Substring(m.OutputStart, m.OutputLength) == m.Before, "review output issue span");
        }
    }
}
