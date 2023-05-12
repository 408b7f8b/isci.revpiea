using System;
using System.Linq;
using isci;
using isci.Allgemein;
using isci.Daten;
using isci.Beschreibung;
using isci.Anwendungen;
using System.Collections.Generic;

namespace revpiea
{
    class Program
    {
        static void Main(string[] args)
        {
            var konfiguration = new Parameter("konfiguration.json");
            
            var structure = new Datenstruktur(konfiguration.OrdnerDatenstruktur);

            RevPiZugriff.SystemkonfigurationLesen();
            RevPiZugriff.EinUndAusgängeAufstellen();
            RevPiZugriff.control.Open();

            var dm = new Datenmodell(konfiguration.Identifikation);

            var Ausgaenge = new Dictionary<dtInt32, ioObjekt>();
            var Eingaenge = new Dictionary<ioObjekt, dtInt32>();

            foreach (var eintrag_ in RevPiZugriff.Eingänge)
            {
                var eintrag = eintrag_.Value.EintragErstellen("");
                dm.Dateneinträge.Add(eintrag);
                Eingaenge.Add(eintrag_.Value, eintrag);
            }

            foreach (var eintrag_ in RevPiZugriff.Ausgänge)
            {
                var eintrag = eintrag_.Value.EintragErstellen("");
                dm.Dateneinträge.Add(eintrag);
                Ausgaenge.Add(eintrag, eintrag_.Value);
            }

            System.IO.File.WriteAllText(konfiguration.OrdnerDatenmodelle + "/" + konfiguration.Identifikation + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(dm));

            structure.DatenmodellEinhängen(dm);
            structure.Start();

            var beschreibung = new Modul(konfiguration.Identifikation, "isci.revpiea", dm.Dateneinträge);
            beschreibung.Name = "RevPiEA Ressource " + konfiguration.Identifikation;
            beschreibung.Beschreibung = "Modul zur EA-Integration von RevPi";
            beschreibung.Speichern(konfiguration.OrdnerBeschreibungen + "/" + konfiguration.Identifikation + ".json");

            var Zustand = new dtZustand(konfiguration.OrdnerDatenstruktur);
            Zustand.Start();

            while(true)
            {
                Zustand.Lesen();
                var erfüllteTransitionen = konfiguration.Ausführungstransitionen.Where(a => a.Eingangszustand == (System.Int32)Zustand.value);
                if (erfüllteTransitionen.Count<Ausführungstransition>() > 0)
                {
                    if (erfüllteTransitionen.ElementAt(0).Eingangszustand == (System.Int32)Zustand.value)
                    {
                        foreach (var Eingang in Eingaenge)
                        {
                            object o = null;
                            if (Eingang.Key.Zustandlesen(out o))
                            {
                                switch (Eingang.Key.typ)
                                {
                                    case ioObjekt.Typ.BOOL: Eingang.Value.value = ((bool)o ? 1 : 0); break;
                                    case ioObjekt.Typ.BYTE: Eingang.Value.value = (int)((byte)o); break;
                                    case ioObjekt.Typ.WORD: Eingang.Value.value = o; break;
                                    case ioObjekt.Typ.INT: Eingang.Value.value = o; break;
                                    default: continue;
                                }
                            
                                Eingang.Value.Schreiben();
                            }
                        }
                        structure.Schreiben();
                        Zustand.value = erfüllteTransitionen.ElementAt(0).Ausgangszustand;
                    } else if (erfüllteTransitionen.ElementAt(1).Eingangszustand == (System.Int32)Zustand.value)
                    {
                        foreach (var Ausgang in Ausgaenge)
                        {
                            Ausgang.Key.Lesen();
                            if (Ausgang.Key.aenderung)
                            {
                                Console.WriteLine("Aenderung " + Ausgang.Key.Identifikation);
                                switch (Ausgang.Value.typ)
                                {
                                    case ioObjekt.Typ.BOOL: Ausgang.Value.Zustandschreiben((System.Int32)Ausgang.Key.value == 1 ? true : false); break;
                                    case ioObjekt.Typ.BYTE: Ausgang.Value.Zustandschreiben((byte)Ausgang.Key.value); break;
                                    case ioObjekt.Typ.WORD: Ausgang.Value.Zustandschreiben((short)Ausgang.Key.value); break;
                                    case ioObjekt.Typ.INT: Ausgang.Value.Zustandschreiben((System.Int32)Ausgang.Key.value); break;
                                    default: continue;
                                }

                                Ausgang.Key.aenderung = false;
                            }
                        }
                        Zustand.value = erfüllteTransitionen.ElementAt(1).Ausgangszustand;
                    }
                    
                    Zustand.Schreiben();
                }
            }
        }
    }
}