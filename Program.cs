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
            var konfiguration = new Parameter(args);

            var structure = new Datenstruktur(konfiguration);

            var ausfuehrungsmodell = new Ausführungsmodell(konfiguration, structure.Zustand);

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

            while(true)
            {
                structure.Zustand.WertAusSpeicherLesen();
                
                if (ausfuehrungsmodell.AktuellerZustandModulAktivieren())
                {
                    var ausfuehrung_parameter = ausfuehrungsmodell.ParameterAktuellerZustand();

                    if ((string)ausfuehrung_parameter == "E")
                    {
                        foreach (var Eingang in Eingaenge)
                        {
                            object o = null;
                            if (Eingang.Key.Zustandlesen(out o))
                            {
                                switch (Eingang.Key.typ)
                                {
                                    case ioObjekt.Typ.BOOL: Eingang.Value.Wert = ((bool)o ? 1 : 0); break;
                                    case ioObjekt.Typ.BYTE: Eingang.Value.Wert = (int)((byte)o); break;
                                    case ioObjekt.Typ.WORD: Eingang.Value.Wert = (int)o; break;
                                    case ioObjekt.Typ.INT: Eingang.Value.Wert = (int)o; break;
                                    default: continue;
                                }
                            }
                        }
                        structure.Schreiben();                        
                    } else if ((string)ausfuehrung_parameter == "A")
                    {
                        foreach (var Ausgang in Ausgaenge)
                        {
                            Ausgang.Key.WertAusSpeicherLesen();
                            if (Ausgang.Key.aenderungExtern)
                            {
                                Console.WriteLine("Aenderung " + Ausgang.Key.Identifikation);
                                switch (Ausgang.Value.typ)
                                {
                                    case ioObjekt.Typ.BOOL: Ausgang.Value.Zustandschreiben((System.Int32)Ausgang.Key.Wert == 1 ? true : false); break;
                                    case ioObjekt.Typ.BYTE: Ausgang.Value.Zustandschreiben((byte)Ausgang.Key.Wert); break;
                                    case ioObjekt.Typ.WORD: Ausgang.Value.Zustandschreiben((short)Ausgang.Key.Wert); break;
                                    case ioObjekt.Typ.INT: Ausgang.Value.Zustandschreiben((System.Int32)Ausgang.Key.Wert); break;
                                    default: continue;
                                }

                                Ausgang.Key.aenderungExtern = false;
                            }
                        }
                    }

                    ausfuehrungsmodell.Folgezustand();
                    structure.Zustand.WertInSpeicherSchreiben();
                }
            }
        }
    }
}