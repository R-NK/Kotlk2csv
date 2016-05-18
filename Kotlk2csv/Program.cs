using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;

namespace Kotlk2csv
{
    class Program
    {
        static void Main(string[] args)
        {
            // ファイルを読み込み用に開く
            try
            {
                Stream stream = File.OpenRead(args[0]);
                //tlkファイル
                if (Path.GetExtension(args[0]) == ".tlk")
                {
                    List<csv> Tablestring = new List<csv>();
                    // streamから読み込むためのBinaryReaderを作成
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        // ヘッダ読み込み
                        Header TLK = new Header();
                        TLK.FileType = reader.ReadBytes(4);
                        TLK.FileVersion = reader.ReadBytes(4);
                        TLK.LanguageID = reader.ReadInt32();
                        TLK.StringCount = reader.ReadInt32();
                        TLK.StringEntriesOffset = reader.ReadInt32();

                        if (Encoding.UTF8.GetString(TLK.FileVersion) != "V3.0")
                        {
                            Console.WriteLine("This file is not supported.");
                        }

                        //DataTableを参照してStringEntry読み込み                   
                        DataTable Table = new DataTable();
                        long currentpos = 0;
                        for (int i = 0; i < TLK.StringCount; i++)
                        {
                            Table.Flags = reader.ReadInt32();
                            Table.SoundResRef = reader.ReadBytes(16);
                            Table.VolumeVariance = reader.ReadInt32();
                            Table.PitchVariance = reader.ReadInt32();
                            Table.OffsetToString = reader.ReadInt32();
                            Table.StringSize = reader.ReadInt32();
                            Table.SoundLength = reader.ReadSingle();
                            if (Table.StringSize != 0)
                            {
                                currentpos = stream.Position;
                                stream.Seek(TLK.StringEntriesOffset + Table.OffsetToString, 0);
                                //stringをUTF8で取得
                                Tablestring.Add(
                                    new csv()
                                    {
                                        id = i.ToString(),
                                        entry = Encoding.UTF8.GetString(reader.ReadBytes(Table.StringSize))
                                    }
                                );
                                //Streamの位置を復元
                                stream.Position = currentpos;
                            }
                        }
                    }
                    try
                    {
                        if (args[1] != null)
                        {
                            using (TextWriter tw = File.CreateText(args[1]))
                            {
                                var csvwriter = new CsvWriter(tw);
                                csvwriter.WriteRecords(Tablestring);
                            }
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        using (TextWriter tw = File.CreateText("dialog.csv"))
                        {
                            var csvwriter = new CsvWriter(tw);
                            csvwriter.WriteRecords(Tablestring);
                        }
                    }
                }
                //CSVファイル
                else if (Path.GetExtension(args[0]) == ".csv")
                {
                    using (var sr = new StreamReader(stream))
                    using (var cr = new CsvReader(sr))
                    {
                        DataTable Table = new DataTable();
                        cr.Configuration.RegisterClassMap<CsvMapper>();
                        var records = cr.GetRecords<csv>();
                        foreach (var record in records)
                        {
                            Table.Flags = 7;
                            Encoding enc = Encoding.GetEncoding("UTF8");
                            Table.StringSize = enc.GetByteCount(record.entry);

                        }
                    }
                    
                }
                stream.Close();
                stream.Dispose();

            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("File not found.");
            }
            catch (IndexOutOfRangeException)
            {
                Console.WriteLine("Enter file name.");
            }
        }

        static void Headerdump(BinaryReader reader)
        {
            // ヘッダ読み込み
            List<string> TLKHeader = new List<string>();
            Header TLK = new Header();
            TLKHeader.Add("FileType:" + Encoding.UTF8.GetString(reader.ReadBytes(4)));
            TLKHeader.Add("FileVersion:" + Encoding.UTF8.GetString(reader.ReadBytes(4)));
            TLKHeader.Add("LanguageID:" + reader.ReadInt32().ToString());
            TLKHeader.Add("StringCount:" + reader.ReadInt32().ToString());
            TLKHeader.Add("StringEntriesOffset:" + reader.ReadInt32().ToString());

            foreach (string str in TLKHeader)
            {
                Console.WriteLine(str);
            }
        }
    }

    public class Header
    {
        public byte[] FileType;
        public byte[] FileVersion;
        public int LanguageID;
        public int StringCount;
        public int StringEntriesOffset;
    }
    public class DataTable
    {
        public int Flags;
        public byte[] SoundResRef; //16byte
        public int VolumeVariance;
        public int PitchVariance;
        public int OffsetToString;
        public int StringSize;
        public float SoundLength;
    }
    public class csv
    {
        public string id { get; set; }
        public string entry { get; set; }
    }
    class CsvMapper : CsvHelper.Configuration.CsvClassMap<csv>
    {
        public CsvMapper()
        {
            Map(x => x.id).Index(0);
            Map(x => x.entry).Index(1);
        }
    }
}
