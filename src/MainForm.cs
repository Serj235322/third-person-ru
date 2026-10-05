using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ThirdPerson
{
    public sealed class MainForm : Form
    {
        private readonly RichTextBox input = Editor(false), output = Editor(true);
        private readonly ComboBox gender = new ComboBox();
        private readonly ComboBox variants = new ComboBox();
        private readonly Button applyVariant = new Button(), keepOriginal = new Button(), undoDecision = new Button();
        private ReviewDecision lastDecision;
        private bool settingsValid;
        private readonly CheckBox quotes = new CheckBox(), plural = new CheckBox();
        private readonly Button convert = new Button(), cancel = new Button(), paste = new Button(), copy = new Button(), save = new Button(), clear = new Button(), demo = new Button();
        private readonly Label counts = new Label(), status = new Label(), guidance = new Label();
        private const string NormalGuidance = "Один рассказчик на весь фрагмент. Пол должен совпадать с исходником.\r\nПроверьте смысл и местоимения: используется конечный набор грамматических правил.";
        private readonly ProgressBar progress = new ProgressBar();
        private readonly ListView changes = ReviewList(), issues = ReviewList();
        private readonly TabPage changesTab = new TabPage("Замены"), issuesTab = new TabPage("Проверить");
        private readonly TabControl review = new TabControl();
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private Conversion result;
        private bool assigning, positionsValid, sourceValid, closing, processing;
        private Color textColor = Color.FromArgb(26, 38, 53);
        private readonly Stopwatch elapsed = new Stopwatch();

        public const string DemoText = "Я приехал около 18 часов. Мне показалось, что Петров был нетрезвым.\r\nЯ не видел, кто забрал телефон. Со мной была сестра. Я живу в Москве и работаю водителем.\r\nПетров сказал: «Я ничего не брал». Я помню его слова.\r\nМы с Петровым пришли к магазину. Мой телефон остался дома.\r\nЯ купил еду. Я плачу за аренду. Я экстраполирую результаты.";
        public const string FemaleDemoText = "Я приехала около 18 часов. Мне показалось, что Петров был нетрезвым.\r\nЯ не видела, кто забрал телефон. Со мной была сестра. Я живу в Москве и работаю водителем.\r\nПетров сказал: «Я ничего не брал». Я помню его слова.\r\nМы с Петровым пришли к магазину. Мой телефон остался дома.\r\nЯ купила еду. Я плачу за аренду. Я экстраполирую результаты.";

        public MainForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Третье лицо 1.2.1 — локальная обработка текста";
            using (Stream iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("ThirdPerson.Icon.ico"))
            using (Icon appIcon = new Icon(iconStream)) Icon = (Icon)appIcon.Clone();
            MinimumSize = new Size(880, 640); ClientSize = new Size(1120, 790);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F); ForeColor = textColor; BackColor = Color.FromArgb(242, 245, 249);
            BuildLayout();
            ResumeLayout(true);
            Load += delegate { ScaleReviewColumns(DeviceDpi); };
            DpiChanged += delegate(object sender, DpiChangedEventArgs e) { ScaleReviewColumns(e.DeviceDpiNew); };
            worker.WorkerSupportsCancellation = true; worker.WorkerReportsProgress = true;
            worker.DoWork += ProcessText;
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e) { progress.Value = e.ProgressPercentage; status.Text = "Обработка: " + e.ProgressPercentage + "%"; };
            worker.RunWorkerCompleted += Finished;
            input.TextChanged += delegate { if (!assigning) { sourceValid = false; UpdateCounts(); if (result != null) status.Text = "Исходник изменён. Результат относится к предыдущему тексту."; UpdateReviewActions(); } };
            output.TextChanged += delegate { if (!assigning && result != null) { positionsValid = false; lastDecision = null; status.Text = "Результат отредактирован. Позиции замечаний устарели; список сохранён для справки."; UpdateReviewActions(); } UpdateCounts(); };
            gender.SelectedIndexChanged += SettingsChanged;
            quotes.CheckedChanged += SettingsChanged; plural.CheckedChanged += SettingsChanged;
            FormClosing += ClosingRequested;
            AcceptButton = convert;
            UpdateCounts();
        }
        private static RichTextBox Editor(bool editableResult)
        {
            RichTextBox box = new RichTextBox();
            box.Dock = DockStyle.Fill; box.BorderStyle = BorderStyle.None; box.BackColor = Color.White;
            box.Font = new Font("Segoe UI", 10F); box.DetectUrls = false; box.HideSelection = false;
            box.WordWrap = true; box.ScrollBars = RichTextBoxScrollBars.Vertical;
            box.MaxLength = editableResult ? Engine.MaximumCharacters * 2 : Engine.MaximumCharacters; box.EnableAutoDragDrop = false;
            box.AccessibleName = editableResult ? "Результат, можно редактировать" : "Исходный текст";
            box.ShortcutsEnabled = true;
            box.KeyDown += delegate(object sender, KeyEventArgs e) {
                if ((e.Control && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert)) {
                    e.SuppressKeyPress = true;
                    try {
                        if (Clipboard.ContainsText()) {
                            string value = Clipboard.GetText(TextDataFormat.UnicodeText);
                            if ((long)box.TextLength - box.SelectionLength + value.Length > box.MaxLength) {
                                MessageBox.Show("Превышен лимит поля. Разделите текст на части.", "Большой текст"); return;
                            }
                            box.SelectedText = value;
                        }
                    } catch (ExternalException) { MessageBox.Show("Буфер обмена занят. Повторите вставку."); }
                }
            };
            return box;
        }
        private static ListView ReviewList()
        {
            ListView list = new ListView();
            list.Dock = DockStyle.Fill; list.View = View.Details; list.FullRowSelect = true;
            list.HideSelection = false; list.MultiSelect = false; list.VirtualMode = true; list.ShowItemToolTips = true;
            list.Columns.Add("Позиция", 85); list.Columns.Add("Исходное", 150); list.Columns.Add("Результат", 150); list.Columns.Add("Пояснение", 650);
            return list;
        }
        private void ScaleReviewColumns(int dpi)
        {
            int[] widths = { 85, 150, 150, 650 };
            foreach (ListView list in new ListView[] { changes, issues })
                for (int i = 0; i < widths.Length; i++)
                    list.Columns[i].Width = (int)Math.Round(widths[i] * dpi / 96.0);
        }
        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 12), ColumnCount = 1, RowCount = 7 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            Controls.Add(root);
            Panel title = new Panel { Dock = DockStyle.Fill };
            title.Controls.Add(new Label { Text = "Третье лицо", Font = new Font("Segoe UI", 20F, FontStyle.Bold), AutoSize = true, Location = new Point(0, -2) });
            title.Controls.Add(new Label { Text = "Текст обрабатывается на этом компьютере. Интернет и видеокарта не требуются.", AutoSize = true, Location = new Point(2, 34), ForeColor = Color.FromArgb(87, 101, 120) });
            root.Controls.Add(title, 0, 0);

            FlowLayoutPanel options = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            options.Controls.Add(new Label { Text = "Рассказчик:", AutoSize = true, Margin = new Padding(0, 9, 8, 0) });
            gender.DropDownStyle = ComboBoxStyle.DropDownList; gender.Items.AddRange(new object[] { "Он — мужской род", "Она — женский род" }); gender.SelectedIndex = 0; gender.Width = 185; gender.AccessibleName = "Пол рассказчика";
            options.Controls.Add(gender);
            quotes.Text = "Сохранять цитаты и реплики"; quotes.Checked = true; quotes.AutoSize = true; quotes.Margin = new Padding(20, 7, 0, 0); options.Controls.Add(quotes);
            plural.Text = "Мы → они (группа определена)"; plural.AutoSize = true; plural.Margin = new Padding(18, 7, 0, 0); options.Controls.Add(plural);
            root.Controls.Add(options, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            SetupButton(paste, "Вставить", 88, delegate { PasteText(); });
            SetupButton(convert, "Преобразовать", 136, delegate { StartConversion(); });
            convert.BackColor = Color.FromArgb(31, 86, 148); convert.ForeColor = Color.White; convert.FlatStyle = FlatStyle.Flat;
            SetupButton(cancel, "Отменить", 92, delegate { worker.CancelAsync(); cancel.Enabled = false; status.Text = "Отмена…"; }); cancel.Enabled = false;
            SetupButton(copy, "Копировать результат", 170, delegate { CopyOutput(); });
            SetupButton(save, "Сохранить .txt", 120, delegate { SaveOutput(); });
            SetupButton(clear, "Очистить", 88, delegate { ClearDocument(); });
            SetupButton(demo, "Пример", 78, delegate { LoadDemo(); });
            buttons.Controls.AddRange(new Control[] { paste, convert, cancel, copy, save, clear, demo }); root.Controls.Add(buttons, 0, 2);

            guidance.Dock = DockStyle.Fill; guidance.Padding = new Padding(9, 7, 5, 3); SetGuidance(null);
            root.Controls.Add(guidance, 0, 3);

            SplitContainer editors = new SplitContainer { Size = new Size(1080, 350), SplitterDistance = 530, Dock = DockStyle.Fill, SplitterWidth = 9, BackColor = BackColor, Panel1MinSize = 200, Panel2MinSize = 200 };
            editors.Panel1.Controls.Add(EditorPanel(input, "ИСХОДНЫЙ ТЕКСТ"));
            editors.Panel2.Controls.Add(EditorPanel(output, "РЕЗУЛЬТАТ • МОЖНО РЕДАКТИРОВАТЬ"));
            root.Controls.Add(editors, 0, 4);
            review.Dock = DockStyle.Fill; review.Margin = new Padding(0, 10, 0, 0); changesTab.Controls.Add(changes); issuesTab.Controls.Add(issues);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 37, Padding = new Padding(3, 3, 0, 0), WrapContents = false };
            variants.DropDownStyle = ComboBoxStyle.DropDownList; variants.Width = 130; variants.AccessibleName = "Вариант для выбранного замечания";
            variants.SelectedIndexChanged += delegate { UpdateReviewActions(); };
            SetupButton(applyVariant, "Применить", 105, delegate { ResolveIssue(false); });
            SetupButton(keepOriginal, "Оставить как есть", 150, delegate { ResolveIssue(true); });
            SetupButton(undoDecision, "Отменить решение", 150, delegate { UndoReviewDecision(); });
            actions.Controls.AddRange(new Control[] { variants, applyVariant, keepOriginal, undoDecision }); issuesTab.Controls.Add(actions);
            review.TabPages.AddRange(new TabPage[] { changesTab, issuesTab }); root.Controls.Add(review, 0, 5);
            changes.RetrieveVirtualItem += delegate(object sender, RetrieveVirtualItemEventArgs e) { e.Item = RowAt(false, e.ItemIndex); };
            issues.RetrieveVirtualItem += delegate(object sender, RetrieveVirtualItemEventArgs e) { e.Item = RowAt(true, e.ItemIndex); };
            changes.SelectedIndexChanged += delegate { Navigate(false); }; issues.SelectedIndexChanged += delegate { LoadVariants(); Navigate(true); };
            UpdateReviewActions();
            TableLayoutPanel footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 4, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            counts.Dock = DockStyle.Fill; counts.ForeColor = Color.FromArgb(87, 101, 120); status.Dock = DockStyle.Fill;
            status.Text = "Готово к работе. Текст не сохраняется автоматически.";
            progress.Dock = DockStyle.Fill; progress.Margin = new Padding(4, 3, 0, 3);
            footer.Controls.Add(counts, 0, 0); footer.Controls.Add(status, 0, 1); footer.Controls.Add(progress, 1, 1); root.Controls.Add(footer, 0, 6);
        }
        private static Panel EditorPanel(RichTextBox box, string caption)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 0, 12, 10), Margin = new Padding(0) };
            Label label = new Label { Text = caption, Dock = DockStyle.Top, Height = 34, Padding = new Padding(0, 10, 0, 0), Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(87, 101, 120) };
            panel.Controls.Add(box); panel.Controls.Add(label); return panel;
        }
        private static void SetupButton(Button b, string text, int width, EventHandler handler)
        {
            b.Text = text; b.Width = width; b.Height = 30; b.Margin = new Padding(0, 0, 6, 0); b.Click += handler;
        }
        private void UpdateCounts()
        {
            counts.Text = string.Format("Исходник: {0:N0} символов   •   Результат: {1:N0}   •   Лимит исходника: 5 000 000", input.TextLength, output.TextLength);
            copy.Enabled = !processing && output.TextLength > 0; save.Enabled = copy.Enabled;
        }
        private void PasteText()
        {
            try {
                if (!Clipboard.ContainsText()) { status.Text = "В буфере обмена нет текста."; return; }
                string value = Clipboard.GetText(TextDataFormat.UnicodeText);
                if (value.Length > Engine.MaximumCharacters) { MessageBox.Show(this, "Лимит — 5 000 000 символов. Разделите текст на части.", "Большой текст"); return; }
                if (input.TextLength > 0 && MessageBox.Show(this, "Заменить исходный текст содержимым буфера обмена?", "Вставка", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                input.Text = value;
            } catch (ExternalException) { status.Text = "Буфер обмена занят. Повторите вставку."; }
        }
        private bool DiscardAllowed()
        {
            return (input.TextLength == 0 && output.TextLength == 0) || MessageBox.Show(this, "Очистить исходник и результат? Несохранённые правки будут потеряны.", "Очистка", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
        private void ClearDocument()
        {
            if (!DiscardAllowed()) return;
            assigning = true; input.Clear(); output.Clear(); assigning = false;
            result = null; positionsValid = false; sourceValid = false; changes.VirtualListSize = issues.VirtualListSize = 0;
            lastDecision = null; LoadVariants();
            SetGuidance(null);
            changesTab.Text = "Замены"; issuesTab.Text = "Проверить"; progress.Value = 0;
            status.Text = "Текст очищен. Буфер обмена не изменён."; UpdateCounts();
        }
        private void LoadDemo()
        {
            if (!DiscardAllowed()) return;
            assigning = true; output.Clear(); input.Text = gender.SelectedIndex == 1 ? FemaleDemoText : DemoText; assigning = false;
            result = null; positionsValid = false; changes.VirtualListSize = issues.VirtualListSize = 0;
            lastDecision = null; LoadVariants();
            changesTab.Text = "Замены"; issuesTab.Text = "Проверить";
            SetGuidance(null);
            status.Text = "Учебный пример. Нажмите «Преобразовать»."; UpdateCounts();
        }
        private void SetBusy(bool value)
        {
            processing = value; input.ReadOnly = value; output.ReadOnly = value;
            paste.Enabled = convert.Enabled = clear.Enabled = demo.Enabled = gender.Enabled = quotes.Enabled = plural.Enabled = !value;
            changes.Enabled = issues.Enabled = !value; cancel.Enabled = value; UpdateCounts(); UpdateReviewActions();
        }
        private sealed class Job { public string Text; public Options Options; }
        private void StartConversion()
        {
            if (worker.IsBusy) return;
            if (input.TextLength == 0) { status.Text = "Вставьте исходный текст."; input.Focus(); return; }
            if (output.TextLength > 0 && MessageBox.Show(this, "Заменить текущий результат новым преобразованием? Ручные правки будут потеряны.", "Повторная обработка", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            Job job = new Job { Text = input.Text, Options = new Options { Female = gender.SelectedIndex == 1, ProtectQuotes = quotes.Checked, ConvertPlural = plural.Checked } };
            SetBusy(true); progress.Value = 0; elapsed.Restart(); status.Text = "Обработка…";
            worker.RunWorkerAsync(job);
        }
        private void ProcessText(object sender, DoWorkEventArgs e)
        {
            Job job = (Job)e.Argument; int last = -1;
            try {
                e.Result = Engine.Convert(job.Text, job.Options, delegate { return worker.CancellationPending; }, delegate(int p) { if (p != last) { last = p; worker.ReportProgress(p); } });
            } catch (OperationCanceledException) { e.Cancel = true; }
        }
        private void Finished(object sender, RunWorkerCompletedEventArgs e)
        {
            elapsed.Stop(); SetBusy(false);
            if (closing) { Close(); return; }
            if (e.Cancelled) { status.Text = "Обработка отменена. Предыдущий результат сохранён."; progress.Value = 0; return; }
            if (e.Error != null) { status.Text = "Ошибка обработки. Предыдущий результат сохранён."; MessageBox.Show(this, e.Error is OutOfMemoryException ? "Недостаточно памяти. Разделите текст на меньшие части." : e.Error.Message, "Ошибка"); return; }
            AssignResult((Conversion)e.Result);
        }
        private void AssignResult(Conversion value)
        {
            // Virtual lists must be cleared before swapping their data source.
            changes.VirtualListSize = issues.VirtualListSize = 0;
            assigning = true;
            output.Clear(); output.Text = value.Text; output.ClearUndo();
            assigning = false; result = value; positionsValid = sourceValid = settingsValid = true; lastDecision = null;
            SetGuidance(result);
            changes.VirtualListSize = result.Changes.Count; issues.VirtualListSize = result.Issues.Count;
            changesTab.Text = string.Format("Замены ({0:N0})", result.ChangeCount) + (result.ChangeCount > Engine.MaximumMarks ? " • первые 2 000" : "");
            issuesTab.Text = string.Format("Проверить ({0:N0})", result.IssueCount) + (result.IssueCount > Engine.MaximumMarks ? " • первые 2 000" : "");
            LoadVariants();
            review.SelectedTab = result.IssueCount > 0 ? issuesTab : changesTab;
            status.Text = string.Format("Готово за {0:F2} с. Замены: {1:N0}. Замечания: {2:N0}. Сохранено цитат / реплик: {3:N0}.", elapsed.Elapsed.TotalSeconds, result.ChangeCount, result.IssueCount, result.ProtectedCount);
            if (result.ContextCount > 0) status.Text += " Разобрано по контексту: " + result.ContextCount + ".";
            if (result.IssueCount > Engine.MaximumMarks || result.ChangeCount > Engine.MaximumMarks) status.Text += " В каждом списке показаны первые 2 000.";
            progress.Value = 100; UpdateCounts();
        }
        private void SetGuidance(Conversion value)
        {
            if (value != null && value.GenderIssueCount > 0) {
                guidance.Text = "Найдено несовпадений рода: " + value.GenderIssueCount.ToString("N0") + ". Проверьте выбор «Он / Она» и формы в исходнике.\r\nВо вкладке «Проверить» эти места показаны первыми. Род исходных форм автоматически не меняется.";
                guidance.BackColor = Color.FromArgb(255, 234, 212); guidance.ForeColor = Color.FromArgb(145, 53, 10);
            } else {
                guidance.Text = NormalGuidance; guidance.BackColor = Color.FromArgb(229, 237, 246); guidance.ForeColor = textColor;
            }
        }
        private ListViewItem RowAt(bool isIssue, int index)
        {
            if (result == null) return new ListViewItem("");
            var marks = isIssue ? result.Issues : result.Changes;
            if (index >= marks.Count) return new ListViewItem("");
            Mark m = marks[index]; ListViewItem row = new ListViewItem((m.SourceStart + 1).ToString("N0"));
            row.SubItems.Add(m.Before); row.SubItems.Add(m.After); row.SubItems.Add(m.Reason);
            row.ToolTipText = m.Reason;
            if (isIssue) row.ForeColor = Color.FromArgb(137, 83, 10); return row;
        }
        private void Navigate(bool isIssue)
        {
            ListView list = isIssue ? issues : changes;
            if (result == null || list.SelectedIndices.Count == 0 || list.SelectedIndices[0] >= (isIssue ? result.Issues.Count : result.Changes.Count)) return;
            Mark mark = (isIssue ? result.Issues : result.Changes)[list.SelectedIndices[0]];
            if (!positionsValid) { status.Text = "Позиции устарели после ручной правки. Замечание: " + mark.Reason; return; }
            if (sourceValid) { input.Select(mark.SourceStart, mark.SourceLength); input.ScrollToCaret(); }
            output.Select(mark.OutputStart, mark.OutputLength); output.ScrollToCaret();
            status.Text = mark.Reason;
        }
        private void SettingsChanged(object sender, EventArgs e)
        {
            if (result != null) { settingsValid = false; status.Text = "Настройки изменены. Преобразуйте исходник повторно."; UpdateReviewActions(); }
        }
        private int SelectedIssue()
        {
            return result != null && issues.SelectedIndices.Count > 0 && issues.SelectedIndices[0] < result.Issues.Count ? issues.SelectedIndices[0] : -1;
        }
        private void LoadVariants()
        {
            variants.Items.Clear(); int index = SelectedIssue();
            if (index >= 0) variants.Items.AddRange(result.Issues[index].Suggestions);
            // Multiple meanings require an explicit choice; no default payment/crying choice.
            if (variants.Items.Count == 1) variants.SelectedIndex = 0;
            UpdateReviewActions();
        }
        private void UpdateReviewActions()
        {
            bool valid = !processing && result != null && positionsValid && sourceValid && settingsValid;
            bool selected = valid && SelectedIssue() >= 0;
            variants.Enabled = selected && variants.Items.Count > 0;
            applyVariant.Enabled = selected && variants.SelectedIndex >= 0;
            keepOriginal.Enabled = selected;
            undoDecision.Enabled = valid && lastDecision != null;
        }
        private void ResolveIssue(bool keep)
        {
            if (!(keep ? keepOriginal.Enabled : applyVariant.Enabled)) return;
            int index = SelectedIssue(); Mark mark = result.Issues[index];
            string value = keep ? null : (string)variants.SelectedItem;
            changes.VirtualListSize = issues.VirtualListSize = 0;
            try {
                lastDecision = ReviewEditor.Apply(result, index, value);
                if (lastDecision.OldWord != lastDecision.NewWord) {
                    assigning = true; output.Select(mark.OutputStart, mark.OutputLength); output.SelectedText = lastDecision.NewWord; output.ClearUndo(); assigning = false;
                }
            } catch (InvalidOperationException ex) { status.Text = ex.Message; RefreshReview(index); return; }
            RefreshReview(index);
            status.Text = "Проверено вручную: " + result.ReviewedCount + ". Осталось замечаний: " + result.IssueCount + ". Доступна отмена последнего решения.";
        }
        private void UndoReviewDecision()
        {
            if (!undoDecision.Enabled) return;
            ReviewDecision decision = lastDecision;
            changes.VirtualListSize = issues.VirtualListSize = 0;
            ReviewEditor.Undo(result, decision);
            if (decision.OldWord != decision.NewWord) {
                assigning = true; output.Select(decision.Issue.OutputStart, decision.NewWord.Length); output.SelectedText = decision.OldWord; output.ClearUndo(); assigning = false;
            }
            lastDecision = null; RefreshReview(decision.Index);
            status.Text = "Последнее решение отменено. Осталось замечаний: " + result.IssueCount + ".";
        }
        private void RefreshReview(int selected)
        {
            changes.VirtualListSize = result.Changes.Count; issues.VirtualListSize = result.Issues.Count;
            changesTab.Text = string.Format("Замены ({0:N0})", result.ChangeCount) + (result.ChangeCount > Engine.MaximumMarks ? " • показаны до 2 000" : "");
            issuesTab.Text = string.Format("Проверить ({0:N0})", result.IssueCount) + (result.IssueCount > result.Issues.Count ? " • показаны до 2 000" : "");
            issues.SelectedIndices.Clear();
            if (issues.VirtualListSize > 0) { int next = Math.Min(selected, issues.VirtualListSize - 1); issues.SelectedIndices.Add(next); issues.EnsureVisible(next); }
            LoadVariants(); SetGuidance(result); UpdateCounts();
        }
        private void CopyOutput()
        {
            try { if (output.TextLength > 0) { Clipboard.SetText(output.Text, TextDataFormat.UnicodeText); status.Text = "Результат скопирован. Буфер обмена содержит текст до его замены или очистки."; } }
            catch (ExternalException) { status.Text = "Буфер обмена занят. Повторите копирование."; }
        }
        private void SaveOutput()
        {
            using (SaveFileDialog dialog = new SaveFileDialog { Filter = "Текст UTF-8 (*.txt)|*.txt", FileName = "Третье лицо.txt", DefaultExt = "txt", AddExtension = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllText(dialog.FileName, output.Text, new UTF8Encoding(true)); status.Text = "Результат сохранён в выбранный файл."; }
                catch (Exception ex) { if (!(ex is IOException) && !(ex is UnauthorizedAccessException)) throw; MessageBox.Show(this, ex.Message, "Не удалось сохранить файл"); }
            }
        }
        private void ClosingRequested(object sender, FormClosingEventArgs e)
        {
            if (worker.IsBusy) { closing = true; worker.CancelAsync(); e.Cancel = true; return; }
            if (!closing && output.TextLength > 0 && MessageBox.Show(this, "Закрыть программу? Несохранённый результат будет потерян.", "Закрытие", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) e.Cancel = true;
        }
        public void RenderSample(string path)
        {
            input.Text = DemoText; elapsed.Start(); AssignResult(Engine.Convert(DemoText, new Options(), null, null)); elapsed.Stop();
            Show(); Application.DoEvents();
            if (issues.VirtualListSize > 0) { issues.SelectedIndices.Add(0); Application.DoEvents(); }
            using (Bitmap bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
            closing = true; Close();
        }
        public void RunUITests(string reportPath)
        {
            StringBuilder log = new StringBuilder();
            ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; Location = new Point(-10000, -10000);
            Show(); Application.DoEvents();
            File.WriteAllText(reportPath, "UI window created.\r\n");
            string paragraph = "Я приехал 01.02.2025 около 18:30. Я не видел, кто забрал телефон. Мне показалось, что Петров был нетрезвым. Я живу в Москве. Мой телефон остался дома.\r\n";
            StringBuilder text = new StringBuilder();
            while (text.Length + paragraph.Length <= Engine.MaximumCharacters) text.Append(paragraph);
            input.Text = text.ToString(); text = null; input.ClearUndo();
            File.AppendAllText(reportPath, "Large input loaded: " + input.TextLength + " characters.\r\n");
            StartConversion();
            File.AppendAllText(reportPath, "Worker started.\r\n");
            if (!input.ReadOnly || !output.ReadOnly || !cancel.Enabled || convert.Enabled) throw new Exception("UI busy state invalid");
            PumpUntilFinished();
            string actualOutput = output.Text;
            if (result == null || actualOutput != result.Text || output.TextLength <= input.TextLength) {
                int difference = -1;
                if (result != null) for (int n = 0; n < Math.Min(actualOutput.Length, result.Text.Length); n++) if (actualOutput[n] != result.Text[n]) { difference = n; break; }
                throw new Exception("UI large output mismatch/truncation: source=" + input.TextLength + ", output=" + output.TextLength + ", actualString=" + actualOutput.Length + ", expected=" + (result == null ? -1 : result.Text.Length) + ", firstDifference=" + difference + ", status=" + status.Text + ", closing=" + closing + ", busy=" + worker.IsBusy);
            }
            if (!copy.Enabled || !save.Enabled || output.ReadOnly || progress.Value != 100) throw new Exception("UI completed state invalid");
            changes.SelectedIndices.Add(0); Navigate(false);
            if (output.SelectedText != result.Changes[0].After || input.SelectedText != result.Changes[0].Before) throw new Exception("UI navigation mismatch");
            log.AppendLine("PASS: native window construction, async conversion, busy/completed controls, editable result, review navigation.");
            log.AppendLine("Source: " + input.TextLength + "; output: " + output.TextLength + " characters (not truncated).");
            using (Process process = Process.GetCurrentProcess()) {
                process.Refresh(); log.AppendLine("GUI peak working set: " + (process.PeakWorkingSet64 / 1048576.0).ToString("F1") + " MiB; peak paged/private allocation: " + (process.PeakPagedMemorySize64 / 1048576.0).ToString("F1") + " MiB.");
            }
            input.Text = "Я приехал.";
            if (sourceValid) throw new Exception("UI source change did not invalidate reference");
            output.Select(0, 2); output.SelectedText = "Вручную";
            if (positionsValid) throw new Exception("UI manual edits did not invalidate marks");
            log.AppendLine("PASS: edits invalidate stale source references and review positions.");
            assigning = true; input.Text = new string('а', Engine.MaximumCharacters); output.Clear(); assigning = false;
            changes.VirtualListSize = issues.VirtualListSize = 0; result = null;
            StartConversion(); worker.CancelAsync(); PumpUntilFinished();
            if (output.TextLength != 0 || !status.Text.StartsWith("Обработка отменена", StringComparison.Ordinal)) throw new Exception("UI cancellation did not preserve prior result");
            log.AppendLine("PASS: cancellation leaves prior result unchanged.");
            assigning = true; input.Text = DemoText; output.Clear(); assigning = false;
            gender.SelectedIndex = 1; StartConversion(); PumpUntilFinished();
            if (result.GenderIssueCount != 3 || !guidance.Text.Contains("несовпадений рода: 3") || review.SelectedTab != issuesTab) throw new Exception("UI gender mismatch warning missing");
            issues.SelectedIndices.Clear(); issues.SelectedIndices.Add(0); Navigate(true);
            if (output.SelectedText != "приехал") throw new Exception("UI mismatch navigation does not select the verb");
            log.AppendLine("PASS: reported female/male sample has 3 highlighted review issues and persistent gender warning.");
            assigning = true; input.Clear(); output.Clear(); assigning = false;
            result = null; changes.VirtualListSize = issues.VirtualListSize = 0; LoadDemo();
            if (input.Text.Replace("\r\n", "\n") != FemaleDemoText.Replace("\r\n", "\n")) throw new Exception("UI female demo selection mismatch");
            StartConversion(); PumpUntilFinished();
            if (result.GenderIssueCount != 0 || result.IssueCount != 1 || !output.Text.Contains("Она купила еду.") || !output.Text.Contains("Она платит за аренду.") || !output.Text.Contains("Её телефон остался дома.") || guidance.Text != NormalGuidance) throw new Exception("UI female sample conversion mismatch");
            log.AppendLine("PASS: Example button follows female selection; female sample says Она купила and has no gender mismatch warning.");
            log.AppendLine("PASS: built-in example review reduced from 4 notices to 1 (group membership); contextual food, possession and payment resolved.");
            assigning = true; input.Text = "Я плачу.\r\nЯ еду. Моем."; assigning = false;
            AssignResult(Engine.Convert(input.Text, new Options { Female = true }, null, null));
            issues.SelectedIndices.Clear(); issues.SelectedIndices.Add(0); LoadVariants(); Navigate(true);
            if (variants.Items.Count != 2 || variants.SelectedIndex != -1 || applyVariant.Enabled || !keepOriginal.Enabled || output.SelectedText != "плачу") throw new Exception("UI ambiguity must require an explicit meaning choice");
            variants.SelectedItem = "платит"; applyVariant.PerformClick();
            if (!positionsValid || output.Text != result.Text || !output.Text.Contains("Она платит.") || result.ReviewedCount != 1 || result.IssueCount != 2 || output.SelectedText != "еду") throw new Exception("UI apply or next-issue offsets invalid");
            applyVariant.PerformClick();
            if (output.Text != result.Text || output.SelectedText != "Моем" || result.IssueCount != 1) throw new Exception("UI second correction offsets invalid");
            variants.SelectedItem = "Её"; applyVariant.PerformClick();
            if (output.Text != result.Text || !output.Text.EndsWith("Её.", StringComparison.Ordinal) || result.IssueCount != 0 || keepOriginal.Enabled || applyVariant.Enabled || !undoDecision.Enabled) throw new Exception("UI shortening correction or complete-review state invalid");
            undoDecision.PerformClick();
            if (output.Text != result.Text || output.SelectedText != "Моем" || result.IssueCount != 1 || undoDecision.Enabled) throw new Exception("UI undo shortening correction invalid");
            keepOriginal.PerformClick();
            if (output.Text != result.Text || !output.Text.EndsWith("Моем.", StringComparison.Ordinal) || result.IssueCount != 0) throw new Exception("UI keep original invalid");
            undoDecision.PerformClick();
            if (result.IssueCount != 1 || output.SelectedText != "Моем") throw new Exception("UI undo keep decision invalid");
            foreach (Mark mark in result.Changes) if (output.Text.Substring(mark.OutputStart, mark.OutputLength) != mark.After) throw new Exception("UI retained change positions invalid");
            log.AppendLine("PASS: explicit variant selection, consecutive corrections, shorter replacement, next issue navigation, keep original, and undo; all retained positions remain exact.");
            gender.SelectedIndex = 0;
            if (applyVariant.Enabled || keepOriginal.Enabled || undoDecision.Enabled) throw new Exception("UI changed options must disable stale suggestions");
            AssignResult(Engine.Convert(input.Text, new Options(), null, null));
            issues.SelectedIndices.Clear(); issues.SelectedIndices.Add(0); LoadVariants();
            input.AppendText(" Исходник изменён.");
            if (applyVariant.Enabled || keepOriginal.Enabled || undoDecision.Enabled) throw new Exception("UI source edits must disable corrections");
            AssignResult(Engine.Convert(input.Text, new Options(), null, null));
            issues.SelectedIndices.Clear(); issues.SelectedIndices.Add(0); LoadVariants();
            output.AppendText(" Ручная правка.");
            if (applyVariant.Enabled || keepOriginal.Enabled || undoDecision.Enabled) throw new Exception("UI result edits must disable stale corrections");
            log.AppendLine("PASS: source edits, free output edits and option changes disable suggestions tied to an old result.");
            Size normalSize = Size; Size = MinimumSize; Application.DoEvents();
            Control actionPanel = applyVariant.Parent;
            if (applyVariant.Right > actionPanel.ClientSize.Width || undoDecision.Right > actionPanel.ClientSize.Width || applyVariant.Bottom > actionPanel.ClientSize.Height || issues.Bottom > actionPanel.Top) throw new Exception("UI review controls clipped/overlapping at minimum window size");
            Size = normalSize;
            log.AppendLine("PASS: review controls fit the minimum window size without overlapping the list.");
            log.AppendLine("Clipboard and external files were not touched. Testing host is not a 2 GB target PC.");
            File.WriteAllText(reportPath, log.ToString(), new UTF8Encoding(true));
            closing = true; Close();
        }
        private void PumpUntilFinished()
        {
            Stopwatch timeout = Stopwatch.StartNew();
            while (processing) {
                Application.DoEvents(); Thread.Sleep(1);
                if (timeout.Elapsed.TotalSeconds > 60) throw new Exception("UI processing timeout");
            }
        }
        public void EndUITest() { closing = true; if (!IsDisposed) Close(); }
    }
}
