using System;
using System.Collections.Generic;

namespace HardModePatch.Core
{
    public sealed class FactoryDiploma
    {
        public readonly int Id, Fee, Days, Hour;
        public readonly string Key, Item, SkillA, SkillB;
        public readonly int GainA, GainB;

        public FactoryDiploma(int id, string key, string item, int fee, int days, int hour,
            string skillA, int gainA, string skillB, int gainB)
        {
            Id = id; Key = key; Item = item; Fee = fee; Days = days; Hour = hour;
            SkillA = skillA; GainA = gainA; SkillB = skillB; GainB = gainB;
        }
    }

    public static class FactoryDiplomas
    {
        public const int Sessions = 8, Homework = 4, Hours = 4;
        public static readonly FactoryDiploma[] All =
        {
            new FactoryDiploma(18, "hmp_garment_making", "ba:itemname_industrialsewingmachine", 3600, 1 | 4 | 16, 9, "Visual", 35, "Handling", 20),
            new FactoryDiploma(19, "hmp_electronics_technician", "ba:itemname_kilnmachine", 3900, 2 | 8 | 32, 9, "Electrical", 45, "Troubleshooting", 10),
            new FactoryDiploma(34, "hmp_jewelry_goldsmithing", "ba:itemname_polishingmachine", 3800, 1 | 4 | 16, 13, "Visual", 35, "Joinery", 20)
        };

        public static FactoryDiploma ForItem(string item)
        {
            foreach (var course in All) if (string.Equals(item, course.Item, StringComparison.Ordinal)) return course;
            return null;
        }

        public static FactoryDiploma ForId(int id)
        {
            foreach (var course in All) if (id == course.Id) return course;
            return null;
        }

        public static bool CanInject(IEnumerable<int> existingIds, int limit)
        {
            var used = new HashSet<int>(existingIds);
            foreach (var course in All) if (course.Id < 0 || course.Id >= limit || !used.Add(course.Id)) return false;
            return true;
        }

        public static bool GateReady(bool selected, bool injected, bool careers, bool study, bool record)
        {
            return selected && injected && careers && study && record;
        }

        public static bool Refuse(string item, bool ready, bool diplomaHeld)
        {
            return ready && !diplomaHeld && ForItem(item) != null;
        }
    }
}
