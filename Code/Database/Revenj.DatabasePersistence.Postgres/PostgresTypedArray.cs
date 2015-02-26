using System;
using System.Collections.Generic;
using System.IO;
using Revenj.DatabasePersistence.Postgres.Converters;
using Revenj.DomainPatterns;
using Revenj.Utility;

namespace Revenj.DatabasePersistence.Postgres
{
	public static class PostgresTypedArray
	{
		public static string ToArray<T>(IEnumerable<T> data, Func<T, string> converter)
		{
			if (data == null)
				return "NULL";
			using (var cms = ChunkedMemoryStream.Create())
			{
				Func<T, IPostgresTuple> toTuple = v => new ValueTuple(converter(v), false, true);
				var writer = cms.GetWriter();
				ToArray(writer, cms.TmpBuffer, data, toTuple);
				writer.Flush();
				cms.Position = 0;
				return cms.GetReader().ReadToEnd();
			}
		}

		public static void ToArray<T>(TextWriter sw, char[] buf, IEnumerable<T> data, Func<T, IPostgresTuple> converter)
		{
			if (data == null)
			{
				sw.Write("NULL");
				return;
			}
			var list = new List<IPostgresTuple>();
			foreach (var item in data)
				list.Add(converter(item));
			sw.Write('\'');
			var arr = new ArrayTuple(list.ToArray());
			arr.InsertRecord(sw, buf, string.Empty, PostgresTuple.EscapeQuote);
			sw.Write('\'');
		}

		public static void ToArray<T>(TextWriter sw, char[] buf, T[] data, Func<T, IPostgresTuple> converter)
		{
			if (data == null)
			{
				sw.Write("NULL");
				return;
			}
			var arr = new IPostgresTuple[data.Length];
			for (int i = 0; i < data.Length; i++)
				arr[i] = converter(data[i]);
			sw.Write('\'');
			var tuple = new ArrayTuple(arr);
			tuple.InsertRecord(sw, buf, string.Empty, PostgresTuple.EscapeQuote);
			sw.Write('\'');
		}

		public static List<T> ParseCollection<T>(BufferedTextReader reader, int context, IServiceLocator locator, Func<BufferedTextReader, int, int, IServiceLocator, T> parseItem)
		{
			var cur = reader.Read();
			if (cur == ',' || cur == ')')
				return null;
			var espaced = cur != '{';
			if (espaced)
			{
				for (int i = 0; i < context; i++)
					reader.Read();
			}
			var list = new List<T>();
			cur = reader.Peek();
			if (cur == '}')
				reader.Read();
			var arrayContext = Math.Max(context << 1, 1);
			var recordContext = arrayContext << 1;
			while (cur != -1 && cur != '}')
			{
				cur = reader.Read();
				if (cur == 'N')
				{
					reader.Read();
					reader.Read();
					reader.Read();
					list.Add(default(T));
				}
				else
				{
					var escaped = cur != '(';
					if (escaped)
					{
						for (int i = 0; i < arrayContext; i++)
							reader.Read();
					}
					list.Add(parseItem(reader, 0, recordContext, locator));
					if (escaped)
					{
						for (int i = 0; i < arrayContext; i++)
							reader.Read();
					}
				}
				cur = reader.Read();
			}
			if (espaced)
			{
				for (int i = 0; i < context; i++)
					reader.Read();
			}
			reader.Read();
			return list;
		}
	}
}
