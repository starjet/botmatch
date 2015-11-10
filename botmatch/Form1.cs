using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Runtime.InteropServices;
using System.Threading;

namespace botmatch
{
    public partial class Form1 : Form
    {
        IDictionary<double, int> noteDictionary = new Dictionary<double, int>();
        IDictionary<double, bool> noteUsedDictionary = new Dictionary<double, bool>();
        IDictionary<double, bool> noteIsDoubleDictionary = new Dictionary<double, bool>();
        IDictionary<double, bool> noteIsHoldDictionary = new Dictionary<double, bool>();
        IDictionary<double, double> noteHoldLengthDictionary = new Dictionary<double, double>();
        double totalMS = 0;
        byte key;
        byte key2;
        byte key3;
        byte key4;
        double keyLength;
        double keyLength2;
        bool autoExit = true;
        TimeSpan origtime;
        TimeSpan curTime;
        KeyValuePair<double, int>[] kvpArrayNotes;
        KeyValuePair<double, bool>[] kvpArrayNotesUsed;
        KeyValuePair<double, bool>[] kvpArrayNotesDouble;
        KeyValuePair<double, bool>[] kvpArrayNotesHold;
        KeyValuePair<double, double>[] kvpArrayNotesHoldLength;

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        static extern bool keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string connectionString;
            MongoClient client;
            MongoServer server;
            MongoDatabase database;
            MongoCollection<BsonDocument> collection;
            MongoCursor<BsonDocument> cursor = null;
            string[] args=Environment.GetCommandLineArgs();
            
            try
            {
                if (args[1] == "-collection")
                {
                    if (args.Length > 2)
                    {
                        MessageBox.Show("Attempting to read collection \"" + args[2] + "\" from local MongoDB server, \"sif\" database.");
                        connectionString = "mongodb://localhost";
                        try
                        {
                            client = new MongoClient(connectionString);
                            server = client.GetServer();
                            database = server.GetDatabase("sif");
                            collection = database.GetCollection<BsonDocument>(args[2]);
                            cursor = collection.FindAll();
                        }
                        catch
                        {
                            MessageBox.Show("Cannot access MonogDB server on localhost.");
                        }

                        if (args.Length > 3) 
                        {
                            if (args[3] == "-no_auto_exit") 
                            {
                                MessageBox.Show("Auto exit disabled.");
                                autoExit = false;
                            }
                        }
                    }
                }
            }
            catch { }

            if (args.Length == 1)
            {
                MessageBox.Show("Usage: " + System.IO.Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName)+" -collection <MongoDB collection name>");
                Environment.Exit(0);
            }
            
            foreach (BsonDocument document in cursor)
            {
                BsonArray bsonArray = document["response_data"]["live_info"][0]["notes_list"].AsBsonArray;
                
                foreach (BsonDocument note in bsonArray)
                {
                    try
                    {
                        noteDictionary.Add(new KeyValuePair<double, int>(Math.Round(note["timing_sec"].ToDouble(), 2), note["position"].ToInt32()));
                        noteUsedDictionary.Add(new KeyValuePair<double, bool>(Math.Round(note["timing_sec"].ToDouble(), 2), false));
                        noteIsDoubleDictionary.Add(new KeyValuePair<double, bool>(Math.Round(note["timing_sec"].ToDouble(), 2), false));
                        
                        if (note["effect"] == 3)
                        {
                            noteIsHoldDictionary.Add(new KeyValuePair<double, bool>(Math.Round(note["timing_sec"].ToDouble(), 2), true));
                        }
                        else 
                        {
                            noteIsHoldDictionary.Add(new KeyValuePair<double, bool>(Math.Round(note["timing_sec"].ToDouble(), 2), false));
                        }

                        noteHoldLengthDictionary.Add(new KeyValuePair<double, double>(Math.Round(note["timing_sec"].ToDouble(), 2), note["effect_value"].ToDouble()));
                    }
                    catch(ArgumentException ex1)
                    {
                        noteDictionary.Add(new KeyValuePair<double, int>(Math.Round(note["timing_sec"].ToDouble(), 2) + 0.001, note["position"].ToInt32()));
                        noteUsedDictionary.Add(new KeyValuePair<double, bool>(Math.Round(note["timing_sec"].ToDouble(), 2) + 0.001, false));
                        noteIsDoubleDictionary.Add(new KeyValuePair<double, bool>(Math.Round(note["timing_sec"].ToDouble(), 2) + 0.001, true));

                        if (note["effect"] == 3)
                        {
                            noteIsHoldDictionary.Add(new KeyValuePair<double, bool>(Math.Round(note["timing_sec"].ToDouble(), 2) + 0.001, true));
                        }
                        else
                        {
                            noteIsHoldDictionary.Add(new KeyValuePair<double, bool>(Math.Round(note["timing_sec"].ToDouble(), 2) + 0.001, false));
                        }

                        noteHoldLengthDictionary.Add(new KeyValuePair<double, double>(Math.Round(note["timing_sec"].ToDouble(), 2) + 0.001, note["effect_value"].ToDouble()));
                    }
                }

                kvpArrayNotes = noteDictionary.ToArray();
                kvpArrayNotesUsed = noteUsedDictionary.ToArray();
                kvpArrayNotesDouble = noteIsDoubleDictionary.ToArray();
                kvpArrayNotesHold = noteIsHoldDictionary.ToArray();
                kvpArrayNotesHoldLength = noteHoldLengthDictionary.ToArray();
            }
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            if (kvpArrayNotesUsed.Last().Value && autoExit)
            {
                if (!kvpArrayNotesHold.Last().Value)
                {
                    if (!backgroundWorker3.IsBusy && !backgroundWorker4.IsBusy)
                    {
                        timer2.Enabled = false;
                        Thread.Sleep(100);
                        Application.OpenForms[this.Name].Activate();
                        MessageBox.Show("Song complete. Exiting application.");
                        Environment.Exit(0);
                    }
                }
                else
                {
                    if (!backgroundWorker3.IsBusy && !backgroundWorker4.IsBusy)
                    {
                        timer2.Enabled = false;
                        Thread.Sleep(100 + Convert.ToInt32(kvpArrayNotesHoldLength.Last().Value * 1000));
                        Application.OpenForms[this.Name].Activate();
                        MessageBox.Show("Song complete. Exiting application.");
                        Environment.Exit(0);
                    }
                }
            }

            if (totalMS == 0)
            {
                totalMS = noteDictionary.First().Key * 1000;
            }
            else
            {
                curTime = DateTime.Now.TimeOfDay;
                double timeDiff = curTime.TotalMilliseconds - origtime.TotalMilliseconds;
                totalMS = timeDiff + noteDictionary.First().Key * 1000;
            }

            for (int i = 0; i < kvpArrayNotes.Length; i++) 
            {
                if (kvpArrayNotes[i].Key < totalMS / 1000.0)
                {
                    if (!kvpArrayNotesUsed[i].Value)
                    {
                        kvpArrayNotesUsed[i] = new KeyValuePair<double, bool>(kvpArrayNotesUsed[i].Key, true);

                        if (!kvpArrayNotesDouble[i].Value)
                        {
                            pressKey(kvpArrayNotes[i].Value, kvpArrayNotesHoldLength[i].Value);

                            if (!kvpArrayNotesHold[i].Value)
                            {
                                backgroundWorker1.RunWorkerAsync();
                            }
                            else 
                            {
                                if (!backgroundWorker3.IsBusy)
                                {
                                    pressKey3(kvpArrayNotes[i].Value, kvpArrayNotesHoldLength[i].Value);
                                    backgroundWorker3.RunWorkerAsync();
                                }
                                else 
                                {
                                    pressKey4(kvpArrayNotes[i].Value, kvpArrayNotesHoldLength[i].Value);
                                    backgroundWorker4.RunWorkerAsync();
                                }
                            }
                        }
                        else 
                        {
                            pressKey2(kvpArrayNotes[i].Value, kvpArrayNotesHoldLength[i].Value);

                            if (!kvpArrayNotesHold[i].Value)
                            {
                                backgroundWorker2.RunWorkerAsync();
                            }
                            else
                            {
                                if (!backgroundWorker4.IsBusy)
                                {
                                    pressKey4(kvpArrayNotes[i].Value, kvpArrayNotesHoldLength[i].Value);
                                    backgroundWorker4.RunWorkerAsync();
                                }
                                else 
                                {
                                    pressKey3(kvpArrayNotes[i].Value, kvpArrayNotesHoldLength[i].Value);
                                    backgroundWorker3.RunWorkerAsync();
                                }
                            }
                        }
                    }
                }
            }
        }

        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 'a')
            {
                origtime = DateTime.Now.TimeOfDay;
                timer2.Enabled = true;
                Cursor.Position = new Point(this.Left-10,this.Top-10);
                mouse_event((uint)MOUSEEVENTF_LEFTDOWN | (uint)MOUSEEVENTF_LEFTUP, (uint)Cursor.Position.X, (uint)Cursor.Position.Y, 0, 0);
            }
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            keybd_event(key, 0, 0, 0);
            Thread.Sleep(50);
            keybd_event(key, 0, KEYEVENTF_KEYUP, 0);
        }

        private void backgroundWorker2_DoWork(object sender, DoWorkEventArgs e)
        {
            keybd_event(key2, 0, 0, 0);
            Thread.Sleep(50);
            keybd_event(key2, 0, KEYEVENTF_KEYUP, 0);
        }

        private void backgroundWorker3_DoWork(object sender, DoWorkEventArgs e)
        {
            int keyMS = Convert.ToInt32(keyLength * 1000.0);

            hold(new Action(() =>
            {
                keybd_event(key3, 0, KEYEVENTF_EXTENDEDKEY, 0);
            }), keyMS);

            keybd_event(key3, 0, KEYEVENTF_KEYUP, 0);
        }

        private void backgroundWorker4_DoWork(object sender, DoWorkEventArgs e)
        {
            int keyMS2 = Convert.ToInt32(keyLength2 * 1000.0);

            hold(new Action(() =>
            {
                keybd_event(key4, 0, KEYEVENTF_EXTENDEDKEY, 0);
            }), keyMS2);

            keybd_event(key4, 0, KEYEVENTF_KEYUP, 0);
        }

        public void hold(Action action, int numMilliseconds)
        {
            DateTime start = DateTime.Now;

            while (DateTime.Now.Subtract(start).TotalMilliseconds < numMilliseconds)
            {
                action();
                Thread.Sleep(30);
            }
        }

        private void pressKey(int value, double length)
        {
            keyLength = length;

            switch (value) 
            {
                case 1:
                    key = 0x50;
                    break;
                case 2:
                    key = 0x4F;
                    break;
                case 3:
                    key = 0x49;
                    break;
                case 4:
                    key = 0x55;
                    break;
                case 5:
                    key = 0x20;
                    break;
                case 6:
                    key = 0x52;
                    break;
                case 7:
                    key = 0x45;
                    break;
                case 8:
                    key = 0x57;
                    break;
                case 9:
                    key = 0x51;
                    break;
            }
        }

        private void pressKey2(int value, double length)
        {
            keyLength2 = length;

            switch (value)
            {
                case 1:
                    key2 = 0x50;
                    break;
                case 2:
                    key2 = 0x4F;
                    break;
                case 3:
                    key2 = 0x49;
                    break;
                case 4:
                    key2 = 0x55;
                    break;
                case 5:
                    key2 = 0x20;
                    break;
                case 6:
                    key2 = 0x52;
                    break;
                case 7:
                    key2 = 0x45;
                    break;
                case 8:
                    key2 = 0x57;
                    break;
                case 9:
                    key2 = 0x51;
                    break;
            }
        }

        private void pressKey3(int value, double length)
        {
            keyLength = length;

            switch (value)
            {
                case 1:
                    key3 = 0x50;
                    break;
                case 2:
                    key3 = 0x4F;
                    break;
                case 3:
                    key3 = 0x49;
                    break;
                case 4:
                    key3 = 0x55;
                    break;
                case 5:
                    key3 = 0x20;
                    break;
                case 6:
                    key3 = 0x52;
                    break;
                case 7:
                    key3 = 0x45;
                    break;
                case 8:
                    key3 = 0x57;
                    break;
                case 9:
                    key3 = 0x51;
                    break;
            }
        }

        private void pressKey4(int value, double length)
        {
            keyLength2 = length;

            switch (value)
            {
                case 1:
                    key4 = 0x50;
                    break;
                case 2:
                    key4 = 0x4F;
                    break;
                case 3:
                    key4 = 0x49;
                    break;
                case 4:
                    key4 = 0x55;
                    break;
                case 5:
                    key4 = 0x20;
                    break;
                case 6:
                    key4 = 0x52;
                    break;
                case 7:
                    key4 = 0x45;
                    break;
                case 8:
                    key4 = 0x57;
                    break;
                case 9:
                    key4 = 0x51;
                    break;
            }
        }
    }
}
