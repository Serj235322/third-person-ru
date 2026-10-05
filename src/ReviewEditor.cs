using System;
using System.Collections.Generic;

namespace ThirdPerson
{
    // One reversible decision. Offsets refer to the exact output snapshot.
    public sealed class ReviewDecision
    {
        internal Mark Issue, AddedChange;
        internal int Index;
        internal string OldWord, NewWord;
    }
    public static class ReviewEditor
    {
        public static ReviewDecision Apply(Conversion result, int index, string variant)
        {
            if (index < 0 || index >= result.Issues.Count) throw new ArgumentOutOfRangeException("index");
            Mark issue = result.Issues[index];
            string oldWord = result.Text.Substring(issue.OutputStart, issue.OutputLength);
            if (variant != null && Array.IndexOf(issue.Suggestions, variant) < 0) throw new ArgumentException("Вариант отсутствует в списке.");
            string newWord = variant ?? oldWord;
            var decision = new ReviewDecision { Issue = issue, Index = index, OldWord = oldWord, NewWord = newWord };
            if (oldWord != newWord) {
                EnsureNoOverlap(result, issue);
                result.Text = result.Text.Remove(issue.OutputStart, issue.OutputLength).Insert(issue.OutputStart, newWord);
                Shift(result.Changes, issue.OutputStart, newWord.Length - oldWord.Length);
                Shift(result.Issues, issue.OutputStart, newWord.Length - oldWord.Length);
                result.ChangeCount++;
                if (result.Changes.Count < Engine.MaximumMarks) {
                    decision.AddedChange = new Mark { SourceStart = issue.SourceStart, SourceLength = issue.SourceLength, OutputStart = issue.OutputStart, OutputLength = newWord.Length, Before = issue.Before, After = newWord, Reason = "Вариант выбран при проверке: " + issue.Reason };
                    result.Changes.Add(decision.AddedChange);
                }
            }
            result.Issues.RemoveAt(index); result.IssueCount--; result.ReviewedCount++;
            if (issue.GenderMismatch) result.GenderIssueCount--;
            return decision;
        }
        public static void Undo(Conversion result, ReviewDecision decision)
        {
            Mark issue = decision.Issue;
            if (decision.OldWord != decision.NewWord) {
                if (decision.AddedChange != null) result.Changes.Remove(decision.AddedChange);
                result.Text = result.Text.Remove(issue.OutputStart, decision.NewWord.Length).Insert(issue.OutputStart, decision.OldWord);
                Shift(result.Changes, issue.OutputStart, decision.OldWord.Length - decision.NewWord.Length);
                Shift(result.Issues, issue.OutputStart, decision.OldWord.Length - decision.NewWord.Length);
                result.ChangeCount--;
            }
            result.Issues.Insert(Math.Min(decision.Index, result.Issues.Count), issue);
            result.IssueCount++; result.ReviewedCount--;
            if (issue.GenderMismatch) result.GenderIssueCount++;
        }
        private static void Shift(List<Mark> marks, int position, int delta)
        {
            foreach (Mark mark in marks) if (mark.OutputStart > position) mark.OutputStart += delta;
        }
        private static void EnsureNoOverlap(Conversion result, Mark issue)
        {
            foreach (Mark other in result.Issues)
                if (other != issue && other.OutputStart < issue.OutputStart + issue.OutputLength && other.OutputStart + other.OutputLength > issue.OutputStart)
                    throw new InvalidOperationException("Есть пересекающееся замечание. Исправьте это место в поле результата.");
            foreach (Mark other in result.Changes)
                if (other.OutputStart < issue.OutputStart + issue.OutputLength && other.OutputStart + other.OutputLength > issue.OutputStart)
                    throw new InvalidOperationException("Есть пересекающаяся замена. Исправьте это место в поле результата.");
        }
    }
}
