﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ffxivlib
{
    public partial class Chatlog
    {
        public class Entry
        {
            #region Properties

            public byte[] Raw { get; set; }
            public DateTime Timestamp { get; set; }
            public string Code { get; set; }
            public string Text { get; set; }
            public string RawString { get; set; }

            #endregion

            #region Constructor

            /// <summary>
            ///     Builds a chatlog entry out of a byte array
            ///     The implementation is sketchy at best but it should be reliable enough
            /// </summary>
            /// <param name="raw">The array to process</param>
            public Entry(byte[] raw)
            {
                Raw = raw;
                RawString = Encoding.UTF8.GetString(raw);
                ProcessEntry(raw);
            }

            #endregion

            #region Private methods

            /// <summary>
            ///     Main processing function
            ///     This extracts the timestamp, code and process the text to clean it.
            /// </summary>
            /// <param name="raw">The array to process</param>
            private void ProcessEntry(byte[] raw)
            {
                List<byte> workingCopy = raw.ToList();
                if (raw.Length < Constants.TIMESTAMP_SIZE + Constants.CHATCODE_SIZE)
                    return;
                try
                    {
                        Timestamp = GetTimeStamp(int.Parse(
                            Encoding.UTF8.GetString(
                                workingCopy.ToArray()
                                ).Substring(0, Constants.TIMESTAMP_SIZE),
                            NumberStyles.HexNumber));
                    }
                catch
                    {
                        return;
                    }
                workingCopy.RemoveRange(0, 8);
                Code = Encoding.UTF8.GetString(workingCopy.ToArray(), 0, Constants.CHATCODE_SIZE);
                workingCopy.RemoveRange(0, 4);
                int sep = workingCopy[1] == ':' ? 2 : 1; // Removes :: separators 
                workingCopy.RemoveRange(0, sep);
                workingCopy = CleanFormat(workingCopy);
                workingCopy = CleanName(workingCopy);
                workingCopy = CleanMob(workingCopy);
                workingCopy = CleanHQ(workingCopy);
                Text = CleanString(Encoding.UTF8.GetString(workingCopy.ToArray()));
            }

            /// <summary>
            ///     Removes any invalid character left
            /// </summary>
            /// <param name="input"></param>
            /// <returns></returns>
            private static string CleanString(string input)
            {
                return new string(input.Where(value =>
                    (value >= 0x0020 && value <= 0xD7FF) ||
                    (value >= 0xE000 && value <= 0xFFFD) ||
                    value == 0x0009 ||
                    value == 0x000A ||
                    value == 0x000D).ToArray());
            }

            /// <summary>
            ///     Removes junk around NPC names that's only useful to the client.
            /// </summary>
            /// <param name="workingCopy"></param>
            /// <returns></returns>
            private static List<byte> CleanMob(List<byte> workingCopy)
            {
                var pattern = new List<byte>
                {
                    0x20,
                    0x20,
                    0xEE,
                    0x81,
                    0xAF,
                    0x20
                };
                if (workingCopy.Count <= 0)
                    return workingCopy;
                if (pattern.Where((t, i) => workingCopy[i] != t).Any())
                    return workingCopy;
                workingCopy.RemoveRange(0, pattern.Count);
                return workingCopy;
            }

            /// <summary>
            ///     This replaces the HQ icon 0xEE 0x80 0xBC by a simple HQ 0x48 0x51
            ///     This unfortunately isn't very reliable as it might replaces other icons used by SE.
            /// </summary>
            /// <param name="workingCopy"></param>
            /// <returns></returns>
            private static List<byte> CleanHQ(List<byte> workingCopy)
            {
                var pattern = new List<byte>
                {
                    0xEE,
                    0x80,
                    0xBC
                };
                var hqRep = new List<byte>
                {
                    0x48,
                    0x51
                };
                int i = workingCopy.FindIndex(item => item == pattern[0]);
                if (i == -1)
                    return workingCopy;
                for (; i < pattern.Count; i++)
                    {
                        if (workingCopy[i] != pattern[i])
                            return workingCopy;
                    }
                workingCopy.RemoveRange(i, pattern.Count);
                workingCopy.InsertRange(i, hqRep);
                return workingCopy;
            }

            /// <summary>
            ///     Removes tags used for formatting
            ///     0x02 0xXX 0xXX 0x03
            ///     0x02 0xXX 0xXX 0xXX 0xXX 0xXX 0xXX 0x03
            /// </summary>
            /// <param name="workingCopy"></param>
            /// <returns></returns>
            private static List<byte> CleanFormat(List<byte> workingCopy)
            {
                int[] idx = workingCopy.Select((b, i) => b == 0x02 ? i : -1).Where(i => i != -1).ToArray();
                bool changed = false;
                foreach (int i in idx)
                    {
                        if (workingCopy.Count > i + 8 && workingCopy[i + 8] == 0x03)
                            {
                                workingCopy.RemoveRange(i, 9);
                                changed = true;
                            }
                        if (workingCopy.Count > i + 4 && workingCopy[i + 4] == 0x03)
                            {
                                workingCopy.RemoveRange(i, 5);
                                changed = true;
                            }
                        if (changed)
                            workingCopy = CleanFormat(workingCopy);
                    }
                return workingCopy;
            }

            /// <summary>
            ///     Removes junk around PC names that's only useful to the client.
            /// </summary>
            /// <param name="workingCopy"></param>
            /// <returns></returns>
            private static List<byte> CleanName(List<byte> workingCopy)
            {
                if (workingCopy.Count(f => f == 0x3) == 1)
                    return workingCopy;
                int name = workingCopy.FindIndex(0, item => item == 0x3);
                if (name != -1)
                    {
                        workingCopy.RemoveRange(0, name + 1);
                        name = workingCopy.FindIndex(0, item => item == 0x3);
                        if (name != -1)
                            workingCopy.RemoveRange(name - 9, 10);
                    }
                return workingCopy;
            }

            /// <summary>
            ///     Creates a DateTime object out of our timestamp
            /// </summary>
            /// <param name="value">Timestamp to convert</param>
            /// <returns>DateTime object corresponding to the timestamp</returns>
            private static DateTime GetTimeStamp(double value)
            {
                var dt = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                return dt.AddSeconds(value).ToLocalTime();
            }

            #endregion
        };
    }
}