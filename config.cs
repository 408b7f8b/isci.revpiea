using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using IctBaden.RevolutionPi;
using IctBaden.RevolutionPi.Configuration;
using IctBaden.RevolutionPi.Model;
using isci.Daten;
using System.Runtime.InteropServices;

namespace isci.revpiea
{
    public static class RevPiZugriff
    {
        public static PiControl control = new PiControl();
        //static PiConfiguration config = new PiConfiguration();

        static Systemkonfiguration Konfiguration;

        public static void SystemkonfigurationLesen(string pfad = "/etc/revpi/config.rsc")
        {
            try {
                Logger.Information("Lese RevPi-Systemkonfiguration: " + pfad);
                var inh = System.IO.File.ReadAllText(pfad).Replace("\"out\":", "\"Out\":");
                var r = Newtonsoft.Json.JsonConvert.DeserializeObject<Systemkonfiguration>(inh);
                Konfiguration = r;
            } catch (System.Exception e)
            {
                Logger.Fatal("Ausnahme beim Lesen der Systemkonfiguration: " + e.Message);
                System.Environment.Exit(-1);
            }            
        }

        public static Dictionary<string, ioObjekt> Eingänge;

        public static Dictionary<string, ioObjekt> Ausgänge;

        public static void EinUndAusgängeAufstellen()
        {
            Logger.Information("Interne Erstellung des Zugriffs auf RevPi-Ein- und Ausgänge.");

            Eingänge = new Dictionary<string, ioObjekt>();
            Ausgänge = new Dictionary<string, ioObjekt>();

            foreach(var device in Konfiguration.Devices)
            {
                foreach (var _inp in device.inp)
                {
                    var o = new ioObjekt(_inp.Value, device.offset);
                    if (!o.sichtbar) continue;
                    //Eingänge.Add(_inp.Key, o);
                    Eingänge.Add(o.Name, o);
                    Logger.Information(o.Name + " als Eingang hinzugefügt. Adressoffset: " + o.AdresseByte + "." + o.AdresseBit);
                }
                foreach (var _out in device.Out)
                {
                    var o = new ioObjekt(_out.Value, device.offset);
                    if (!o.sichtbar) continue;
                    //Ausgänge.Add(_out.Key, o);
                    Ausgänge.Add(o.Name, o);
                    Logger.Information(o.Name + " als Ausgang hinzugefügt. Adressoffset: " + o.AdresseByte + "." + o.AdresseBit);
                }                
            }
        }
    }
    

    public class Systemkonfiguration
    {
        public List<Device> Devices;
    }

    public class Device
    {
        public int offset;
        public Dictionary<string, List<object>> inp;
        public Dictionary<string, List<object>> Out;
    }

    public class ioObjekt
    {
        public string Name;
        public int AdresseByte;
        public int AdresseBit;
        public bool sichtbar;
        private object wert;
        public enum Typ {
                BOOL = 1, BYTE = 8, WORD = 16, INT = 32
        };
        public Typ typ;

        private byte[] wert_;

        public ioObjekt(List<object> objektliste, int offset = 0)
        {
            Name = (string)objektliste[0];
            wert = objektliste[1];
            typ = (Typ) System.Int32.Parse((string)objektliste[2]);
            AdresseByte = offset + System.Int32.Parse((string)objektliste[3]);
            sichtbar = (bool)objektliste[4];
            var AdresseBit_ = System.Int32.Parse((string)objektliste[5]);
            var AdresseBit_g = (int) (AdresseBit_ / 8.0);
            var AdresseBit_m = AdresseBit_ % 8;
            AdresseBit = AdresseBit_m;
            AdresseByte += AdresseBit_g;
            int l = (typ == Typ.INT ? 4 : (typ == Typ.WORD ? 2 : 1));
            wert_ = new byte[l];
        }

        public Dateneintrag EintragErstellen()
        {
            switch(typ)
            {
                case Typ.BOOL: return new dtBool(wert_[0] != 0 ? true : false, Name);
                case Typ.BYTE: return new dtUInt8(wert_[0], Name);
                case Typ.INT: return new dtInt32((wert_[3] << 24) + (wert_[2] << 16) + (wert_[1] << 8) + wert_[0], Name);
                case Typ.WORD: return new dtInt16((short)((wert_[1] << 8) + wert_[0]), Name);
                default: return null;
            }
        }

        public bool Zustandlesen()
        {
            int l = (typ == Typ.INT ? 4 : (typ == Typ.WORD ? 2 : 1));

            var wert__ = new byte[l];
            wert__ = RevPiZugriff.control.Read(AdresseByte, l);

            for (int i = 0; i < l; ++i)
            {
                if (wert__[i] != wert_[i])
                {
                    goto Raus;
                }
            }

            return false;

            Raus:
            wert_ = wert__;
            return true;
        }

        public bool Zustandlesen(out object o)
        {
            bool r = Zustandlesen();

            if (!r) {
                o = null;
                return false;
            }

            switch(typ)
            {
                case Typ.BOOL: var b = ((int)wert_[0] & (1 << AdresseBit)) != 0; o = b; break;
                case Typ.BYTE: o = wert_[0]; break;
                case Typ.INT: var i = (wert_[3] << 24) + (wert_[2] << 16) + (wert_[1] << 8) + wert_[0]; o = i; break;
                case Typ.WORD: var j = (short)(wert_[1] << 8) + wert_[0]; o = j; break;
                default: o = null; break;
            }

            if (o != null) return true;

            return false;
        }

        public void Zustandschreiben(object o)
        {
            switch(typ)
            {
                case Typ.BOOL: wert_[0] = BitConverter.GetBytes((wert_[0] & ~(1 << AdresseBit)) | ( ((bool)o ? 1 : 0) << AdresseBit))[0]; break;
                case Typ.BYTE: wert_[0] = (byte)o; break;
                case Typ.INT: wert_ = BitConverter.GetBytes((int)o); break;
                case Typ.WORD: wert_ = BitConverter.GetBytes((short)o); break;
                default: break;
            }

            RevPiZugriff.control.Write(AdresseByte, wert_);
        }
    }
}