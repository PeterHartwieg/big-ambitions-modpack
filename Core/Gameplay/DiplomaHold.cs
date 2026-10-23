using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HardModePatch.Core
{
    // Keeps a save's factory diploma records while RLO can't read them, and gives them back once a reviewed RLO and
    // HMP's courses are loaded again. RLO's careers line (CareersStore, modData "reallife:careers") reads
    //     v=1~h:...~s:...~c:rec;rec~r:rec;rec~...
    // with fields split by '|'. 'c' holds courses under way, 'r' finished ones. A record's fields start
    //     course|mode|start|seen|done|settled day|homework|fee|instalments|loan|on loan|grant|stage|exam from|exam to|...
    // An RLO that doesn't know a course id drops that record when it reads the line, and the record is gone after
    // RLO's next save.
    //
    // The hold is our own line in the same version: 'c' and 'r' parts whose records are "<day>#<record>", the day the
    // record was first held. It is settled in one step once RLO can read our courses again (Restore): all records go
    // back together or none do, so no attempt is matched against another.
    public static class DiplomaHold
    {
        public const string CareersKey = "reallife:careers";
        public const string HoldKey = "hardmodepatch:factory_diplomas_held";
        const int StageField = 12, StartField = 2, SettledField = 5, ExamFromField = 13, ExamToField = 14, ExcusedDayField = 23;
        const int Studying = 0, Exam = 1, Retake = 6;
        static readonly char[] Parts = { 'c', 'r' };

        sealed class Line
        {
            public string Version;
            public readonly List<KeyValuePair<char, List<string>>> Parts = new List<KeyValuePair<char, List<string>>>();

            public List<string> Records(char letter)
            {
                foreach (var part in Parts) if (part.Key == letter) return part.Value;
                return null;
            }

            public List<string> Ensure(char letter)
            {
                var records = Records(letter);
                if (records != null) return records;
                records = new List<string>();
                Parts.Add(new KeyValuePair<char, List<string>>(letter, records));
                return records;
            }

            public override string ToString()
            {
                var text = new StringBuilder("v=").Append(Version);
                foreach (var part in Parts) text.Append('~').Append(part.Key).Append(':').Append(string.Join(";", part.Value.ToArray()));
                return text.ToString();
            }
        }

        // One of our records: where it is ('c' or 'r'), its fields and, in the hold, the day it was last seen.
        sealed class Held
        {
            public char Part;
            public int Day;
            public string[] Fields;
            public int Id { get { return Int(0, -1); } }
            public string Field(int i) { return i < Fields.Length ? Fields[i] : string.Empty; }
            public int Int(int i, int otherwise)
            {
                int value;
                return i < Fields.Length && int.TryParse(Fields[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : otherwise;
            }
            public bool Live { get { int stage = Int(StageField, 0); return stage == Studying || stage == Exam || stage == Retake; } }
            public string Record { get { return string.Join("|", Fields); } }
            public string Stored { get { return Day.ToString(CultureInfo.InvariantCulture) + "#" + Record; } }
        }

        // The line split as RLO's TechReader splits it; null if it isn't one. A later part with the same letter
        // replaces an earlier one there, so it does here too.
        static Line Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var pieces = text.Split('~');
            if (!pieces[0].StartsWith("v=", StringComparison.Ordinal)) return null;
            int version;
            if (!int.TryParse(pieces[0].Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out version)) return null;
            var line = new Line { Version = pieces[0].Substring(2) };
            for (int i = 1; i < pieces.Length; i++)
            {
                string piece = pieces[i];
                if (piece.Length < 2 || piece[1] != ':') continue;
                var records = piece.Substring(2).Split(';').Where(r => r.Length > 0).ToList();
                int at = line.Parts.FindIndex(p => p.Key == piece[0]);
                if (at >= 0) line.Parts[at] = new KeyValuePair<char, List<string>>(piece[0], records);
                else line.Parts.Add(new KeyValuePair<char, List<string>>(piece[0], records));
            }
            return line;
        }

        public static int[] Ids { get { return FactoryDiplomas.All.Select(c => c.Id).ToArray(); } }

        static Held FromLine(char part, string record, int day)
        {
            return new Held { Part = part, Day = day, Fields = record.Split('|') };
        }

        static Held FromHold(char part, string stored)
        {
            int hash = stored.IndexOf('#');
            int day;
            if (hash <= 0 || !int.TryParse(stored.Substring(0, hash), NumberStyles.Integer, CultureInfo.InvariantCulture, out day)) return null;
            return new Held { Part = part, Day = day, Fields = stored.Substring(hash + 1).Split('|') };
        }

        // Our records in RLO's line.
        static List<Held> Ours(Line line, int day)
        {
            var ids = Ids;
            var found = new List<Held>();
            if (line == null) return found;
            foreach (char letter in Parts)
                foreach (var record in line.Records(letter) ?? new List<string>())
                {
                    var held = FromLine(letter, record, day);
                    if (ids.Contains(held.Id)) found.Add(held);
                }
            return found;
        }

        static List<Held> HeldIn(Line hold)
        {
            var found = new List<Held>();
            if (hold == null) return found;
            foreach (char letter in Parts)
                foreach (var stored in hold.Records(letter) ?? new List<string>())
                {
                    var held = FromHold(letter, stored);
                    if (held != null) found.Add(held);
                }
            return found;
        }

        static string Write(string version, IEnumerable<Held> records)
        {
            var hold = new Line { Version = version };
            foreach (var record in records) hold.Ensure(record.Part).Add(record.Stored);
            return hold.Parts.Count == 0 ? null : hold.ToString();
        }

        // How many of our records a careers line or hold carries.
        public static int Count(string text)
        {
            var line = Parse(text);
            if (line == null) return 0;
            int inLine = Ours(line, 0).Count;
            return inLine > 0 ? inLine : HeldIn(line).Count;
        }

        public static bool HasOurs(string careers) { return Ours(Parse(careers), 0).Count > 0; }

        // While RLO can't read our courses: the hold to store. Our records in RLO's line, plus every held record the
        // line doesn't have as it was held: a hold never loses a record it has. A record already held unchanged keeps
        // the day it was first held, so a line RLO leaves alone for days counts as held since then. RLO's line is never
        // changed. Null when there is nothing to hold.
        public static string Keep(string careers, string held, int today)
        {
            var line = Parse(careers);
            var previous = Parse(held);
            var ours = Ours(line, today);
            if (ours.Count == 0) return held;
            if (previous != null && previous.Version != line.Version) return held;
            // Matched one for one: each record in the line takes one held copy of the same part and text, if any.
            var unmatched = HeldIn(previous);
            foreach (var record in ours)
            {
                int at = unmatched.FindIndex(h => h.Part == record.Part && h.Record == record.Record);
                if (at < 0) continue;
                record.Day = unmatched[at].Day;
                unmatched.RemoveAt(at);
            }
            ours.AddRange(unmatched);
            return Write(line.Version, ours);
        }

        // Moves a held course under way on by whole weeks, at least the days it was held: its start, last counted class
        // and excused day (studying) or its exam window (exam, retake). RLO counts no class missed and no exam window
        // lapsed meanwhile, and whole weeks keep the course's weekdays. The course may wait up to six days more than it
        // was held before its next class. Finished records are not moved.
        static void Pause(Held record, int days)
        {
            if (days <= 0 || !record.Live) return;
            int weeks = (days + 6) / 7 * 7;
            Func<int, bool, string> moved = (field, only) =>
            {
                int value = record.Int(field, int.MinValue);
                if (value == int.MinValue || (only && value < 0)) return record.Field(field);
                return (value + weeks).ToString(CultureInfo.InvariantCulture);
            };
            var fields = record.Fields.ToArray();
            if (record.Int(StageField, 0) == Studying)
            {
                fields[StartField] = moved(StartField, false);
                if (fields.Length > SettledField) fields[SettledField] = moved(SettledField, true);
                if (fields.Length > ExcusedDayField) fields[ExcusedDayField] = moved(ExcusedDayField, true);
            }
            else
            {
                if (fields.Length > ExamFromField) fields[ExamFromField] = moved(ExamFromField, false);
                if (fields.Length > ExamToField) fields[ExamToField] = moved(ExamToField, false);
            }
            record.Fields = fields;
        }

        public sealed class Restored
        {
            // RLO's careers line to store, or null when it stays as it is.
            public string Careers;
            // True when the hold is done with and can be removed; otherwise Hold is what still waits.
            public bool Done;
            public string Hold;
            public int Records;
            public string Waiting;
        }

        // Why the records can't all join RLO's line, or null when RLO's reader will keep every one of them: at most
        // liveMost courses under way, one per course, and resultsMost results. RLO's reader checks nothing else for a
        // course it knows. Every record already in the line counts, so this errs only towards waiting.
        static string NoRoom(Line line, List<Held> records, int liveMost, int resultsMost)
        {
            var under = (line.Records('c') ?? new List<string>()).Select(r => FromLine('c', r, 0)).Where(r => r.Live).ToList();
            int live = under.Count;
            var liveCourses = new HashSet<int>(under.Select(r => r.Id));
            int results = (line.Records('r') ?? new List<string>()).Count;
            foreach (var record in records)
            {
                if (record.Part == 'c')
                {
                    if (!record.Live) return "a finished record is in the courses part";
                    if (live >= liveMost) return "RLO's limit of " + liveMost + " courses under way is reached";
                    if (liveCourses.Contains(record.Id)) return "course " + record.Id + " is already under way";
                    live++;
                    liveCourses.Add(record.Id);
                }
                else
                {
                    if (record.Live) return "a course under way is in the results part";
                    if (results >= resultsMost) return "RLO's limit of " + resultsMost + " results is reached";
                    results++;
                }
            }
            return null;
        }

        // With a reviewed RLO and our courses registered:
        // 1. Held records that RLO's line still has as they were held (RLO never rewrote them, for example with its
        //    study switched off) are moved on in place.
        // 2. The other held records go back together, moved on, if RLO's reader will keep them all, or none of them do
        //    and they keep waiting in the hold. All or nothing, so two attempts at a course are never told apart.
        // Null when the line or the hold can't be read or their versions differ: the hold waits as it is.
        public static Restored Restore(string careers, string held, int today, int liveMost, int resultsMost)
        {
            var line = Parse(careers);
            var hold = Parse(held);
            if (line == null || hold == null || line.Version != hold.Version) return null;
            var rest = HeldIn(hold);
            var result = new Restored();
            var ids = Ids;
            bool changed = false;
            // Matched one for one: each of our records in the line takes one held copy of the same part and text.
            foreach (char letter in Parts)
            {
                var records = line.Records(letter);
                if (records == null) continue;
                for (int i = 0; i < records.Count; i++)
                {
                    var record = FromLine(letter, records[i], today);
                    if (!ids.Contains(record.Id)) continue;
                    int at = rest.FindIndex(h => h.Part == letter && h.Record == records[i]);
                    if (at < 0) continue;
                    Pause(record, today - rest[at].Day);
                    rest.RemoveAt(at);
                    result.Records++;
                    if (record.Record == records[i]) continue;
                    records[i] = record.Record;
                    changed = true;
                }
            }
            result.Waiting = NoRoom(line, rest, liveMost, resultsMost);
            if (result.Waiting == null)
            {
                foreach (var record in rest)
                {
                    Pause(record, today - record.Day);
                    line.Ensure(record.Part).Add(record.Record);
                    changed = true;
                }
                result.Records += rest.Count;
                result.Done = true;
            }
            else result.Hold = Write(hold.Version, rest);
            result.Careers = changed ? line.ToString() : null;
            return result;
        }
    }
}
