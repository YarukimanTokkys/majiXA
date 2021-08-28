using System.IO;
using System.Collections.Generic;

namespace majiXA
{
    public class Utils
    {
        public static byte[] ToBytes(majiXA.eCommand com, params object[] objs)
        {
            var data = new List<byte>();
            data.Add((byte)com);

            foreach (var obj in objs)
            {
                var t = obj.GetType();
                if (t == typeof(System.Byte))
                    data.Add((byte)obj);                        // byte
                else if (t == typeof(System.Boolean))
                    data.Add((bool)obj ? (byte)1 : (byte)0);  // bool
                else if (t == typeof(System.Int32))
                    data.AddRange(((int)obj).ToBytes());        // int
                else if (t == typeof(System.Single))
                    data.AddRange(((float)obj).ToBytes());      // float
                else if (t == typeof(System.Double))
                    data.AddRange(((double)obj).ToBytes());     // double
                else if (t == typeof(System.String))
                    data.AddRange(((string)obj).ToBytes());     // string
                else if (t == typeof(System.Byte[]))
                    data.AddRange((System.Byte[])obj);          // byte[]
            }

            return data.ToArray();
        }
    }
}