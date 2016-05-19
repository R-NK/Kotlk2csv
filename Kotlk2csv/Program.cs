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
                            //SoundResRef読み込み
                            Table.SoundResRef = ReaduntilNull.ReadNullTerminatedString(reader); //0に当たるまで一文字ずつ取得
                            Encoding en = Encoding.GetEncoding("UTF-8");
                            stream.Seek(15 - en.GetByteCount(Table.SoundResRef), SeekOrigin.Current);
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
                                if (Table.SoundResRef != null)
                                {
                                    Tablestring.Add(
                                        new csv()
                                        {
                                            id = i.ToString(),
                                            soundref = Table.SoundResRef,
                                            entry = Encoding.UTF8.GetString(reader.ReadBytes(Table.StringSize))
                                        }
                                    );
                                }
                                else
                                {
                                    Tablestring.Add(
                                        new csv()
                                        {
                                            id = i.ToString(),
                                            soundref = "",
                                            entry = Encoding.UTF8.GetString(reader.ReadBytes(Table.StringSize))
                                        }
                                    );
                                }
                                //Streamの位置を復元
                                stream.Position = currentpos;
                            }
                        }
                    }
                    try
                    {
                        if (args[1] != null) //第二引数を出力ファイル名に
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
                        var records = cr.GetRecords<csv>().ToList();
                        Table.OffsetToString = 0;
                        List<byte[]> TLK = new List<byte[]>();
                        List<byte[]> TLKString = new List<byte[]>();
                        //ヘッダ出力
                        List<byte[]> TLKHeader = new List<byte[]>();
                        TLKHeader.Add(Encoding.UTF8.GetBytes("TLK V3.0"));
                        TLKHeader.Add(BitConverter.GetBytes(0));
                        int StringCount = records.Count();
                        //StringCount
                        TLKHeader.Add(BitConverter.GetBytes(StringCount));
                        //StringEntriesOffset
                        TLKHeader.Add(BitConverter.GetBytes(StringCount * 40 + 20));                      
                        //DataTable出力
                        foreach (var record in records)
                        {
                            Table.Flags = 7;
                            Encoding enc = Encoding.GetEncoding("UTF-8");
                            Table.StringSize = enc.GetByteCount(record.entry);
                            TLK.Add(BitConverter.GetBytes(Table.Flags));
                            if (record.soundref != "")
                            {
                                TLK.Add(Encoding.UTF8.GetBytes(record.soundref));
                                //SoundRefResが16バイトになるまで0で埋める
                                for (int i = 1; i + enc.GetByteCount(record.soundref) <= 16; i++)
                                {                                   
                                    TLK.Add(BitConverter.GetBytes(false));
                                }
                                for (int i = 0; i < 2; i++)
                                {
                                    TLK.Add(BitConverter.GetBytes(0));
                                }
                            }else
                            {
                                for (int i = 0; i < 6; i++)
                                {
                                    TLK.Add(BitConverter.GetBytes(0));
                                }
                            }
                            TLK.Add(BitConverter.GetBytes(Table.OffsetToString));
                            TLK.Add(BitConverter.GetBytes(Table.StringSize));
                            TLK.Add(BitConverter.GetBytes(0));
                            Table.OffsetToString += Table.StringSize;
                            TLKString.Add(Encoding.UTF8.GetBytes(record.entry));
                        }
                        TLKHeader.AddRange(TLK);
                        TLKHeader.AddRange(TLKString);                        
                        try
                        {
                            if (args[1] != null) //第二引数を出力ファイル名に
                            {
                                FileStream fs = new FileStream(args[1], FileMode.OpenOrCreate, FileAccess.Write);
                                var flattenedList = TLKHeader.SelectMany(bytes => bytes);
                                var byteArray = flattenedList.ToArray();
                                fs.Write(byteArray, 0, byteArray.Length);
                                fs.Close();
                            }
                        }
                        catch (IndexOutOfRangeException)
                        {
                            FileStream fs = new FileStream("exported.tlk", FileMode.OpenOrCreate, FileAccess.Write);
                            var flattenedList = TLKHeader.SelectMany(bytes => bytes);
                            var byteArray = flattenedList.ToArray();
                            fs.Write(byteArray, 0, byteArray.Length);
                            fs.Close();
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
    public static class ReaduntilNull
    {
        public static string ReadNullTerminatedString(this BinaryReader stream)
        {
            string str = "";
            char ch;
            while ((int)(ch = stream.ReadChar()) != 0)
                str = str + ch;
            return str;
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
        public string SoundResRef; //16byte
        public int VolumeVariance;
        public int PitchVariance;
        public int OffsetToString;
        public int StringSize;
        public float SoundLength;
    }
    public class csv
    {
        public string id { get; set; }
        public string soundref { get; set; }
        public string entry { get; set; }
    }
    class CsvMapper : CsvHelper.Configuration.CsvClassMap<csv>
    {
        public CsvMapper()
        {
            Map(x => x.id).Index(0);
            Map(x => x.soundref).Index(1);
            Map(x => x.entry).Index(2);
        }
    }
}
