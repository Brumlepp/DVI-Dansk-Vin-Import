using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Xml;

namespace Dansk_Vin_Import
{
    public class Program
    {
        // Importer funktionen fra Windows API
        private static List<SyndicationItem> nyhedsartikler = new List<SyndicationItem>();
        private static DateTime sidsteUdskrivningNyheder;

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        // Definition af konstanter for vinduestil
        private const int SW_MAXIMIZE = 3;

        static async Task Main(string[] args)
        {
            // Maksimer terminalvinduet
            Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight);
            Console.SetBufferSize(Console.LargestWindowWidth, Console.LargestWindowHeight);

            // Find håndtaget til terminalvinduet
            IntPtr handle = GetConsoleWindow();

            // Maksimer vinduet
            ShowWindow(handle, SW_MAXIMIZE);

            ///// FUNKTIONER & VARIABLER //////
            
            //Skjuler Cursoren
            Console.CursorVisible = false;
            
            ///     Indlæs Nyheder   //////
            Task.Run(async () =>
            {
                Program program = new Program();
                await IndlæsNyheder();
            }).GetAwaiter().GetResult();

            // Indhentning af DVIService
            DVIService.monitorSoapClient ds = new DVIService.monitorSoapClient();

            // Design af DVIService
            string indrykning = new string(' ', 20); // Indrykning med 20 mellemrum
            string indrykningTid = new string(' ', 4); // Indrykning med 4 mellemrum

            ///////////// Double Datatype ///////////////
            double LagerFugtighed = ds.StockHumidity();
            double UdenforFugtighed = ds.OutdoorHumidity();
            double LagerTemperatur = ds.StockTemp();
            double UdenforTemperatur = ds.OutdoorTemp();
            

            ///////////// DateTime Datatype ///////////////
            DateTime sidsteUdskrivningTid = DateTime.MinValue;
            DateTime sidsteUdskrivningTemp = DateTime.MinValue;
            DateTime sidsteUdskrivningFugtighed = DateTime.MinValue;
            DateTime sidsteUdskrivningLager = DateTime.MinValue;
            
            DateTime nuværendeTid = DateTime.Now;
            DateTime nuværendeTemp = DateTime.Now;
            DateTime nuværendeFugtighed = DateTime.Now;
            DateTime nuværendeLager = DateTime.Now;
            DateTime nuværendeNyheder = DateTime.Now;

            //// DATO & TID KODE ////
            // Definer tidzoner for København, London og Singapore
            TimeZoneInfo copenhagenTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            TimeZoneInfo londonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
            TimeZoneInfo singaporeTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

            // Liste over byer og deres respektive tidzoner og dato
            List<(string By, TimeZoneInfo Tidszone, DateTime StartTid)> byer = new List<(string By, TimeZoneInfo Tidszone, DateTime StartTid)>
            {
                ("København", copenhagenTimeZone, DateTime.UtcNow),
                ("London", londonTimeZone, DateTime.UtcNow),
                ("Singapore", singaporeTimeZone, DateTime.UtcNow)
            };

            //Henter tiden
            DateTime HentByTid(TimeZoneInfo tidszone, DateTime startTid)
            {
                DateTime utcTid = DateTime.UtcNow;
                DateTime byTid = TimeZoneInfo.ConvertTimeFromUtc(utcTid, tidszone);
                DateTime justeretTid = startTid.AddSeconds((byTid - startTid).TotalSeconds);

                return justeretTid;
            }

            while (true)
            {
                DateTime iterationTid = DateTime.Now;

                // Tjek om der er gået 1 sekund siden sidste udskrivning af tiden
                if ((iterationTid - sidsteUdskrivningTid).TotalSeconds >= 1)
                {
                    Console.SetCursorPosition(0, 0);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("///// Tidzoner: /////");
                    Console.ResetColor();
                    foreach (var by in byer)
                    {
                        nuværendeTid = HentByTid(by.Tidszone, by.StartTid); // Opdater nuværendeTid med den nye værdi
                        Console.WriteLine($"{by.By}: {nuværendeTid.ToString("dddd, dd/MM/yyyy HH:mm:ss", new CultureInfo("da-DK"))}");
                    }

                    // Opdater sidsteUdskrivningTid med det aktuelle tidspunkt
                    sidsteUdskrivningTid = iterationTid;
                }

                // Tjek om der er gået 1 minut siden sidste udskrivning af temperatur
                else if ((nuværendeTemp - sidsteUdskrivningTemp).TotalMinutes >= 5)
                {
                    LagerTemperatur = ds.StockTemp(); // Opdater LagerTemperatur med den nye værdi
                    Console.SetCursorPosition(0, 6);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("///// Temp: /////");
                    Console.ResetColor();
                    Console.WriteLine("Temperatur på lageret er: " + LagerTemperatur + " °C");
                    Console.WriteLine("Temperatur Udenforfor er: "+ UdenforTemperatur + " °C");



                    //lav en if statement så vi kan se forskel når temp er uder 12-14 og over !!!
                   
                    sidsteUdskrivningTemp = nuværendeTemp;
                }

                // Tjek om der er sket ændring i fugtighed
                else if ((nuværendeFugtighed - sidsteUdskrivningFugtighed).TotalMinutes >= 5)
                {
                    LagerFugtighed = ds.StockHumidity(); // Opdater LagerFugtighed med den nye værdi
                    Console.SetCursorPosition(0, 11);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("///// Fugtighed: /////");
                    Console.ResetColor();
                    Console.WriteLine("Fugtighed på lageret er: " + LagerFugtighed + "%");
                    Console.WriteLine("Fugtighed udefor er: " + UdenforFugtighed + " °C");

                    sidsteUdskrivningFugtighed = nuværendeFugtighed;
                }

                // Tjek om der er sket ændring i lagerstatus
                else if ((nuværendeLager - sidsteUdskrivningLager).TotalMinutes >= 5)
                {
                    Console.SetCursorPosition(0, 15);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Under Minimum");
                    Console.ResetColor();
                    foreach (string LagerUnderMinimum in ds.StockItemsUnderMin())
                    {
                        Console.WriteLine(LagerUnderMinimum);
                    }

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Over Max");
                    Console.ResetColor();
                    foreach (string LagerOverMax in ds.StockItemsOverMax())
                    {
                        Console.WriteLine(LagerOverMax);
                    }

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Mest solgt");
                    Console.ResetColor();
                    foreach (string LagerMestSolgte in ds.StockItemsMostSold())
                    {
                        Console.WriteLine(LagerMestSolgte);
                    }

                    sidsteUdskrivningLager = nuværendeLager;
                }

                // Tjek om der er gået 5 minutter siden sidste indlæsning af nyheder
                else if ((nuværendeNyheder - sidsteUdskrivningNyheder).TotalMinutes >= 5)
                {
                    await IndlæsNyheder(); // Indlæs nyheder
                    Console.SetCursorPosition(0, 25);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Seneste nyheder:");
                    Console.ResetColor();
                    int counter = 0; // Tæller for at begrænse antallet af udskrevne nyheder
                    
                    foreach (SyndicationItem nyhedsartikel in nyhedsartikler)
                    {
                        if (counter < 3) // Kontrollér antallet af udskrevne nyheder
                        {
                            Console.WriteLine("Titel: " + nyhedsartikel.Title.Text);
                            Console.WriteLine("Beskrivelse: " + nyhedsartikel.Summary.Text);
                            //Console.WriteLine("Link: " + nyhedsartikel.Links.FirstOrDefault()?.Uri);
                            Console.WriteLine();
                            counter++; // Øg tælleren
                        }
                        else
                        {
                            break; // Stop loopet, når antallet af udskrevne nyheder når tre
                        }
                    }

                    sidsteUdskrivningNyheder = nuværendeNyheder;
                }



                // Opdater nuværendeTid med den nye værdi
                nuværendeTid = iterationTid;
                // Opdater nuværendeTemp med den nye værdi
                nuværendeTemp = iterationTid;
                // Opdater nuværendeFugtighed med den nye værdi
                nuværendeFugtighed = iterationTid;
                // Opdater nuværendeLager med den nye værdi
                nuværendeLager = iterationTid;
                // Opdater nuværendeNyheder med den nye værdi
                nuværendeNyheder = iterationTid;
            }
        }

        private static async Task IndlæsNyheder()
        {
            string rssUrl = "https://nordjyske.dk/rss/nyheder"; // Udskift med den faktiske RSS-feed-URL

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string rssFeed = await client.GetStringAsync(rssUrl);
                    using (XmlReader reader = XmlReader.Create(new System.IO.StringReader(rssFeed)))
                    {
                        SyndicationFeed feed = SyndicationFeed.Load(reader);
                        nyhedsartikler = feed.Items.ToList();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fejl ved indlæsning af nyheder: {ex.Message}");
                }
            }
        }
    }
}
