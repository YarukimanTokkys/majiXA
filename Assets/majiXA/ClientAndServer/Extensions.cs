using System;
using System.Text;

public static class Extensions
{
	// ===== valiablies -> byte array
	public static byte ToByte(this bool b)
	{
		return b ? (byte)1 : (byte)0;
	}

	public static byte[] ToBytes(this short s) => CheckEndian(BitConverter.GetBytes(s));
	public static byte[] ToBytes(this int i) => CheckEndian(BitConverter.GetBytes(i));
	public static byte[] ToBytes(this float f) => CheckEndian(BitConverter.GetBytes(f));
	public static byte[] ToBytes(this long l) => CheckEndian(BitConverter.GetBytes(l));
	public static byte[] ToBytes(this double d) => CheckEndian(BitConverter.GetBytes(d));
	public static byte[] ToBytes(this string s) => Encoding.UTF8.GetBytes(s);

	static byte[] CheckEndian(byte[] ret)
	{
		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(ret);
		}
		return ret;
	}


	// ===== byte array -> valiables
	public static bool ToBool(this byte[] b, ref int csr)
	{
		int c = csr;
		csr += 1;
		return BitConverter.ToBoolean(b, c);
	}

	public static byte ToByte(this byte[] b, ref int csr)
	{
		csr++;
		return b[csr - 1];
	}

	public static short ToShort(this byte[] b, ref int csr)
	{
		int c = csr;
		csr += 2;

		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(b, c, 2);
		}
		return BitConverter.ToInt16(b, c);
	}

	public static int ToInt(this byte[] b, ref int csr)
	{
		int c = csr;
		csr += 4;

		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(b, c, 4);
		}
		return BitConverter.ToInt32(b, c);
	}

	public static long ToLong(this byte[] b, ref int csr)
	{
		int c = csr;
		csr += 8;

		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(b, c, 8);
		}
		return BitConverter.ToInt64(b, c);
	}

	public static float ToFloat(this byte[] b, ref int csr)
	{
		int c = csr;
		csr += 4;

		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(b, c, 4);
		}
		return BitConverter.ToSingle(b, c);
	}

	public static double ToDouble(this byte[] b, ref int csr)
	{
		int c = csr;
		csr += 8;

		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(b, c, 8);
		}
		return BitConverter.ToDouble(b, c);
	}

	public static string ToString(this byte[] b, ref int csr, int len = -1)
	{
		int c = csr;
		if (len == -1)
		{
			len = b.Length - c;
		}
		csr += len;
		return Encoding.UTF8.GetString(b, c, len);
	}

	public static int ByteToInt(this byte[] b, ref int csr)
	{
		int c = csr;
		csr += 1;

		return (int)(sbyte)b[c];
	}
}
