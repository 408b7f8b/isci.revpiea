using System;
using System.Linq;
using isci;
using isci.Allgemein;
using isci.Daten;
using isci.Beschreibung;
using isci.Anwendungen;
using System.Collections.Generic;

namespace isci.revpiea
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
            if (RevPiZugriff.control.Open())
            {
                Logger.Information("RevPi-Prozessabbild-Zugriff aktiv.");
            } else {
                Logger.Fatal("Konnte nicht auf RevPi-Prozessabbild zugreifen.");
                System.Environment.Exit(-1);
            }

            var dm = new Datenmodell(konfiguration.Identifikation);

            var Ausgaenge = new Dictionary<Dateneintrag, ioObjekt>();
            var Eingaenge = new Dictionary<ioObjekt, Dateneintrag>();

            foreach (var eintrag_ in RevPiZugriff.Eingänge)
            {
                Logger.Information("Erstelle Dateneintrag für Eingang " + eintrag_.Key);
                var eintrag = eintrag_.Value.EintragErstellen();
                dm.Dateneinträge.Add(eintrag);
                Eingaenge.Add(eintrag_.Value, eintrag);
            }

            foreach (var eintrag_ in RevPiZugriff.Ausgänge)
            {
                Logger.Information("Erstelle Dateneintrag für Ausgang " + eintrag_.Key);
                var eintrag = eintrag_.Value.EintragErstellen();
                dm.Dateneinträge.Add(eintrag);
                Ausgaenge.Add(eintrag, eintrag_.Value);
            }

            dm.Speichern(konfiguration);

            structure.DatenmodellEinhängen(dm);
            structure.Start();

            new Modul(konfiguration.Identifikation, "isci.revpiea", dm.Dateneinträge) {
                Name = "RevPiEA Ressource " + konfiguration.Identifikation,
                Beschreibung = "Modul zur EA-Integration von RevPi"
            }.Speichern(konfiguration);

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
                                Logger.Alles("Aenderung Eingang: " + Eingang.Value.Identifikation);
                                switch (Eingang.Key.typ)
                                {
                                    case ioObjekt.Typ.BOOL: Eingang.Value.Wert = (bool)o; break;
                                    case ioObjekt.Typ.BYTE: Eingang.Value.Wert = (byte)o; break;
                                    case ioObjekt.Typ.WORD: Eingang.Value.Wert = (short)o; break;
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
                                Ausgang.Key.aenderungExtern = false;
                                Logger.Alles("Aenderung Ausgang: " + Ausgang.Key.Identifikation);
                                switch (Ausgang.Value.typ)
                                {
                                    case ioObjekt.Typ.BOOL:
                                    case ioObjekt.Typ.BYTE:
                                    case ioObjekt.Typ.WORD:
                                    case ioObjekt.Typ.INT: Ausgang.Value.Zustandschreiben(Ausgang.Key.Wert); break;
                                    default: continue;
                                }
                            }
                        }
                    }

                    ausfuehrungsmodell.Folgezustand();
                    structure.Zustand.WertInSpeicherSchreiben();
                }

                //System.Threading.Thread.Sleep(1);
                isci.Helfer.SleepForMicroseconds(konfiguration.PauseArbeitsschleifeUs);
            }
        }
    }
}