using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace MDACS.Database.MediaTools
{
    public class MP4Info
    {
        public static UInt32 BigSwap(UInt32 v)
        {
            unchecked
            {
                return (UInt32)IPAddress.NetworkToHostOrder((int)v);
            }
        }

        /// <summary>
        /// A incomplete MVHD atom header. The fields after `duration` are misssing.
        /// 
        /// https://developer.apple.com/library/content/documentation/QuickTime/QTFF/QTFFChap2/qtff2.html#//apple_ref/doc/uid/TP40000939-CH204-54948
        /// </summary>
        public class MVHDInfo
        {
            public byte version;
            public byte[] flags;
            public uint creation_time;
            public uint modification_time;
            /// <summary>
            /// Number of time scale units per second of real time.
            /// </summary>
            public uint time_scale;
            /// <summary>
            /// Duration in time scale units.
            /// </summary>
            public uint duration;

            public MVHDInfo(BinaryReader br)
            {
                version = br.ReadByte();
                flags = br.ReadBytes(3);
                creation_time = BigSwap(br.ReadUInt32());
                modification_time = BigSwap(br.ReadUInt32());
                time_scale = BigSwap(br.ReadUInt32());
                duration = BigSwap(br.ReadUInt32());
            }
        }

        public struct AtomInfo
        {
            public long offset;
            public long size;
            public string type;
        }

        public static IEnumerable<AtomInfo> EnumerateAtoms(BinaryReader br, long offset, long max_size)
        {
            br.BaseStream.Seek(offset, SeekOrigin.Begin);

            while (br.BaseStream.Position < offset + max_size)
            {
                var atom_size = BigSwap(br.ReadUInt32());
                var atom_type = Encoding.ASCII.GetString(br.ReadBytes(4));

                AtomInfo info;

                info.offset = br.BaseStream.Position;
                info.size = atom_size;
                info.type = atom_type;

                yield return info;

                br.BaseStream.Seek(atom_size, SeekOrigin.Current);
            }
        }

        /// <summary>
        /// Return the duration in seconds of a MP4 file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static double GetDuration(string path)
        {
            var fp = File.OpenRead(path);
            var br = new BinaryReader(fp);

            foreach (var atom in EnumerateAtoms(br, 0, br.BaseStream.Length))
            {
                if (atom.type.Equals("mvhd"))
                {
                    br.BaseStream.Seek(atom.offset, SeekOrigin.Begin);
                    var mvhdinfo = new MVHDInfo(br);

                    //Console.WriteLine($"{mvhdinfo.duration} {mvhdinfo.time_scale}");

                    // Use double to ensure fractional seconds are represented.
                    return (double)mvhdinfo.duration / (double)mvhdinfo.time_scale;
                }

                if (atom.type.Equals("moov"))
                {
                    foreach (var satom in EnumerateAtoms(br, atom.offset, atom.size))
                    {
                        if (satom.type.Equals("mvhd"))
                        {
                            br.BaseStream.Seek(satom.offset, SeekOrigin.Begin);
                            var mvhdinfo = new MVHDInfo(br);

                            //Console.WriteLine($"{mvhdinfo.duration} {mvhdinfo.time_scale}");

                            // Use double to ensure fractional seconds are represented.
                            return (double)mvhdinfo.duration / (double)mvhdinfo.time_scale;
                        }
                    }
                }
            }

            return 0.0;
        }
    }
}
