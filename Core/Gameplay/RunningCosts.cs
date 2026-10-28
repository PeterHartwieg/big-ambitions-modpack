using System;
using System.Collections.Generic;

namespace HardModePatch.Core
{
    // What a business's equipment and opening hours add to Real Life Overhaul's power and water, and what an office's
    // desks cost in IT (Peter, 22 October 2026: bring back the two Economy Expansion pressures RLO lacks). RLO bills a
    // business by floor area and kind, the same for a 2-hour and a 24-hour day, and nothing for the fryers or fridges in
    // it. Units are RLO's: thousandths of a kWh (watt-hours) and of a cubic metre (litres); money in cents at the start's
    // prices (RLO's price level 1000).
    public static class RunningCosts
    {
        // RLO's floor figures are a business's day. A 12-hour day matches them; 24 hours costs 1.75 times as much.
        public const int ReferenceHours = 12;
        // RLO's share of a day's use on a day the business stays shut (UtilitiesCatalogue.ClosedShare).
        public const int ClosedShare = 250;

        // RLO's bounds on its price level (PriceRules.Least, Most).
        public const int LevelLeast = 500, LevelMost = 4000;

        // A month of software, cloud and support for one desk with a computer, cents at the start's prices. Billed with
        // the office's internet as RLO bills its plans: so much a game month, a day's share each day it is connected.
        public const int SeatMonthCents = 15000;

        // The game's office business types (RLO's UtilitiesGame.KindOf, Office). Every one of them needs a connection to
        // open with the office rule on; RLO already asks it of the graphic designer, web developer and headquarters. RLO's
        // own tech firms are offices too, but RLO already charges them software by the seat (FirmsCatalogue), so they pay
        // no desk IT here.
        public static readonly string[] OfficeTypes =
        {
            "ba:businesstype_headquarters", "ba:businesstype_lawfirm", "ba:businesstype_graphicdesigner",
            "ba:businesstype_marketingagency", "ba:businesstype_recruitmentagency", "ba:businesstype_webdevelopmentagency",
            "ba:businesstype_eventplanningagency", "ba:businesstype_travelagency", "ba:businesstype_interiorinstallationfirm",
            "ba:businesstype_importexport"
        };

        public const int FactoryMachineFlag = 0x80, ForSaleFlag = 0x2000, RetailProductFlag = 0x2, ComputerFlag = 0x800000,
            SinkFlag = 0x1000000, ToiletFlag = 0x2000000;

        public struct Appliance
        {
            public readonly int OpenWatts;      // average draw while the business is open, duty cycle included
            public readonly int AlwaysWatts;    // average draw around the clock (fridges, freezers, alarms)
            public readonly int LitresPerHour;  // water while open

            public Appliance(int openWatts, int alwaysWatts, int litresPerHour)
            {
                OpenWatts = openWatts;
                AlwaysWatts = alwaysWatts;
                LitresPerHour = litresPerHour;
            }

            public bool Uses { get { return OpenWatts > 0 || AlwaysWatts > 0 || LitresPerHour > 0; } }
        }

        // Average draw from commercial ratings and typical duty cycles. Lights, climate and the kind's standby stay in RLO's
        // floor figure (DayUse).
        static readonly Dictionary<string, Appliance> Table = new Dictionary<string, Appliance>(StringComparer.Ordinal)
        {
            // kitchens
            { "ba:itemname_industrialfryermachine", new Appliance(5000, 0, 0) },
            { "ba:itemname_industrialgrill", new Appliance(4000, 0, 0) },
            { "ba:itemname_hotdoggrill", new Appliance(1200, 0, 0) },
            { "ba:itemname_pizzaoven", new Appliance(6000, 0, 0) },
            { "ba:itemname_industrialcoffeemachine", new Appliance(1200, 0, 20) },
            { "ba:itemname_cheapcoffeemachine", new Appliance(300, 0, 2) },
            { "ba:itemname_slushimachine", new Appliance(800, 0, 0) },
            { "ba:itemname_concessionsdisplaycase", new Appliance(500, 0, 0) },
            { "ba:itemname_counterdishwashermarble", new Appliance(1000, 0, 40) },
            { "ba:itemname_counterdishwashermodern", new Appliance(1000, 0, 40) },
            // refrigeration, around the clock
            { "ba:itemname_industrialfridge", new Appliance(0, 500, 0) },
            { "ba:itemname_industrialfreezer", new Appliance(0, 900, 0) },
            { "ba:itemname_smallindustrialfreezer", new Appliance(0, 600, 0) },
            { "ba:itemname_icecreamcounter", new Appliance(0, 800, 0) },
            { "ba:itemname_saladbar", new Appliance(0, 500, 0) },
            { "ba:itemname_drinksfridge", new Appliance(0, 250, 0) },
            { "ba:itemname_largedrinksfridge", new Appliance(0, 450, 0) },
            { "ba:itemname_standardfridge", new Appliance(0, 150, 0) },
            { "ba:itemname_luxuryfridge", new Appliance(0, 200, 0) },
            { "ba:itemname_cocktailbarbeershelf", new Appliance(0, 300, 0) },
            { "ba:itemname_woodencocktailbarbeershelf", new Appliance(0, 300, 0) },
            { "ba:itemname_watercooler", new Appliance(0, 100, 0) },
            // office and IT
            { "ba:itemname_computer", new Appliance(150, 0, 0) },
            { "ba:itemname_desktopcomputer", new Appliance(200, 0, 0) },
            { "ba:itemname_gamingcomputer", new Appliance(350, 0, 0) },
            { "ba:itemname_laptop", new Appliance(50, 0, 0) },
            { "ba:itemname_scorpiogamingsetup", new Appliance(400, 0, 0) },
            { "ba:itemname_computermonitor", new Appliance(30, 0, 0) },
            { "ba:itemname_graphictabletwithscreen", new Appliance(30, 0, 0) },
            { "ba:itemname_printer", new Appliance(100, 0, 0) },
            // factory machines
            { "ba:itemname_automatedbakingmachine", new Appliance(30000, 0, 0) },
            { "ba:itemname_bottlingmachine", new Appliance(15000, 0, 100) },
            { "ba:itemname_consumergoodsassemblymachine", new Appliance(40000, 0, 0) },
            { "ba:itemname_foodassemblymachine", new Appliance(20000, 0, 50) },
            { "ba:itemname_industrialblendingmachine", new Appliance(15000, 0, 50) },
            { "ba:itemname_industrialsewingmachine", new Appliance(3000, 0, 0) },
            { "ba:itemname_kilnmachine", new Appliance(25000, 0, 0) },
            { "ba:itemname_lasercuttingmachine", new Appliance(12000, 0, 0) },
            { "ba:itemname_polishingmachine", new Appliance(8000, 0, 0) },
            { "ba:itemname_hydroponicplanter", new Appliance(0, 2000, 0) },
            // salons, gyms, nightlife
            { "ba:itemname_hairdresserchair", new Appliance(800, 0, 0) },
            { "ba:itemname_hairdresserchairmodern", new Appliance(800, 0, 0) },
            { "ba:itemname_hairdresserheadwash", new Appliance(500, 0, 150) },
            { "ba:itemname_treadmill", new Appliance(800, 0, 0) },
            { "ba:itemname_publicshower", new Appliance(0, 0, 250) },
            { "ba:itemname_djbooth", new Appliance(1000, 0, 0) },
            { "ba:itemname_stagelights", new Appliance(2000, 0, 0) },
            { "ba:itemname_loudspeaker1", new Appliance(200, 0, 0) },
            { "ba:itemname_loudspeaker2", new Appliance(200, 0, 0) },
            { "ba:itemname_loudspeaker3", new Appliance(2000, 0, 0) },
            { "ba:itemname_loudspeaker4", new Appliance(100, 0, 0) },
            { "ba:itemname_boothprojection", new Appliance(3000, 0, 0) },
            { "ba:itemname_boothsound", new Appliance(1000, 0, 0) },
            { "ba:itemname_boothlighting", new Appliance(1500, 0, 0) },
            { "ba:itemname_casinoslotmachine", new Appliance(400, 0, 0) },
            { "ba:itemname_expensivetv", new Appliance(500, 0, 0) },
            { "ba:itemname_wallmountedtv", new Appliance(100, 0, 0) },
            { "ba:itemname_wallmountedtvmodern", new Appliance(150, 0, 0) },
            { "ba:itemname_wallmountedtvxl", new Appliance(300, 0, 0) },
            // security, around the clock
            { "ba:itemname_securitycamera", new Appliance(0, 10, 0) },
            { "ba:itemname_securitycameraroof", new Appliance(0, 10, 0) },
            { "ba:itemname_securitypanel", new Appliance(0, 50, 0) },
            { "ba:itemname_metaldetector", new Appliance(100, 0, 0) },
            // water
            { "ba:itemname_sink", new Appliance(0, 0, 30) },
            { "ba:itemname_sinkmodular", new Appliance(0, 0, 30) },
            { "ba:itemname_sinkwallmodern", new Appliance(0, 0, 30) },
            { "ba:itemname_countersink", new Appliance(0, 0, 40) },
            { "ba:itemname_countersinkmarble", new Appliance(0, 0, 40) },
            { "ba:itemname_countersinkmodern", new Appliance(0, 0, 40) },
            { "ba:itemname_shower", new Appliance(0, 0, 100) },
            { "ba:itemname_showerstructure", new Appliance(0, 0, 100) },
            { "ba:itemname_wallmountedshowerhead", new Appliance(0, 0, 100) },
            { "ba:itemname_toilet", new Appliance(0, 0, 20) },
            { "ba:itemname_toiletclassic", new Appliance(0, 0, 20) },
            { "ba:itemname_toiletmodern01", new Appliance(0, 0, 20) },
            { "ba:itemname_toiletmodern02", new Appliance(0, 0, 20) },
            { "ba:itemname_toiletstall", new Appliance(0, 0, 20) }
        };

        // An item placed in a business: the table, else a rough figure for other mods' machines, computers and
        // plumbing by the game's item flags. Stock for sale and anything else uses nothing.
        public static Appliance Of(string itemName, int typeFlags)
        {
            if ((typeFlags & (ForSaleFlag | RetailProductFlag)) != 0) return default(Appliance);
            Appliance known;
            if (itemName != null && Table.TryGetValue(itemName, out known)) return known;
            if ((typeFlags & FactoryMachineFlag) != 0) return new Appliance(10000, 0, 0);
            if ((typeFlags & ComputerFlag) != 0) return new Appliance(150, 0, 0);
            if ((typeFlags & SinkFlag) != 0) return new Appliance(0, 0, 30);
            if ((typeFlags & ToiletFlag) != 0) return new Appliance(0, 0, 20);
            return default(Appliance);
        }

        public static bool IsSeat(int typeFlags)
        {
            return (typeFlags & ComputerFlag) != 0 && (typeFlags & (ForSaleFlag | RetailProductFlag)) == 0;
        }

        public static bool IsOffice(string businessType)
        {
            return businessType != null && Array.IndexOf(OfficeTypes, businessType) >= 0;
        }

        // The hours a business keeps open on a day, as the game's schedule has them, within a day.
        public static int Hours(bool scheduledOpen, int hoursOpen)
        {
            return scheduledOpen ? Math.Max(0, Math.Min(24, hoursOpen)) : 0;
        }

        // A business's day of power or water: RLO's floor by the hours open, and the appliances. RLO's shut-day quarter
        // is its standby ("fridges, alarms"), so the placed refrigeration and alarms replace it when they draw more,
        // rather than adding to it. RLO's water rate is the kind's basins, so placed plumbing replaces the open part of
        // it when it uses more. Cooking, machines and IT draw on top. Watt-hours of power or litres of water.
        public static long DayUse(bool power, long rloUse, int hours, Appliance a)
        {
            if (rloUse < 0) return rloUse;
            int h = Math.Max(0, Math.Min(24, hours));
            long full = h > 0 ? rloUse : rloUse * 1000 / ClosedShare;
            long standby = full * ClosedShare / 1000;
            long open = full * (1000 - ClosedShare) * h / (1000L * ReferenceHours);
            if (power) return Math.Max(standby, (long)a.AlwaysWatts * 24) + open + (long)a.OpenWatts * h;
            return standby + Math.Max(open, (long)a.LitresPerHour * h);
        }

        // A day's share of the desks' IT, cents at a price level (1000 at the start's prices, held within RLO's bounds).
        public static int SeatDayCents(int seats, int level, int monthDays)
        {
            if (seats <= 0) return 0;
            long cents = (long)seats * SeatMonthCents * Math.Max(LevelLeast, Math.Min(LevelMost, level)) / 1000;
            int days = Math.Max(1, monthDays);
            return (int)Math.Min((cents + days / 2) / days, int.MaxValue / 4);
        }
    }
}
