﻿using System;
using System.Collections.Generic;
using System.IO;
using Revenj.Utility;

namespace Revenj.DatabasePersistence.Postgres.Converters
{
	public class RecordTuple : IPostgresTuple
	{
		private readonly IPostgresTuple[] Properties;

		public RecordTuple(IPostgresTuple[] properties)
		{
			this.Properties = properties;
			//TODO: check if properties count > 0, otherwise return ()
		}

		public bool MustEscapeRecord { get { return Properties != null; } }
		public bool MustEscapeArray { get { return Properties != null; } }

		public RecordTuple Except(IEnumerable<int> indexes)
		{
			if (indexes != null)
				foreach (var item in indexes)
					Properties[item] = null;
			return this;
		}

		public string BuildTuple(bool quote)
		{
			if (Properties == null)
				return "NULL";
			using (var cms = ChunkedMemoryStream.Create())
			{
				var sw = cms.GetWriter();
				Action<TextWriter, char> mappings = null;
				if (quote)
				{
					mappings = PostgresTuple.EscapeQuote;
					sw.Write('\'');
				}
				sw.Write('(');
				for (int i = 0; i < Properties.Length; i++)
				{
					var p = Properties[i];
					if (p != null)
					{
						if (p.MustEscapeRecord)
						{
							sw.Write('"');
							p.InsertRecord(sw, cms.TmpBuffer, "1", mappings);
							sw.Write('"');
						}
						else p.InsertRecord(sw, cms.TmpBuffer, string.Empty, mappings);
					}
					if (i < Properties.Length - 1)
						sw.Write(',');
				}
				sw.Write(')');
				if (quote)
					sw.Write('\'');
				sw.Flush();
				cms.Position = 0;
				return cms.GetReader().ReadToEnd();
			}
		}

		public Stream Build()
		{
			return Build(false, null);
		}

		private static readonly byte[] NULL = new byte[] { (byte)'N', (byte)'U', (byte)'L', (byte)'L' };

		public Stream Build(bool bulk, Action<TextWriter, char> mappings)
		{
			if (Properties == null)
				return new MemoryStream(NULL);
			var cms = ChunkedMemoryStream.Create();
			var sw = cms.GetWriter();
			if (bulk)
			{
				for (int i = 0; i < Properties.Length; i++)
				{
					var p = Properties[i];
					if (p != null)
						p.InsertRecord(sw, cms.TmpBuffer, string.Empty, mappings);
					else
						sw.Write("\\N");
					if (i < Properties.Length - 1)
						sw.Write('\t');
				}
			}
			else
			{
				sw.Write('(');
				for (int i = 0; i < Properties.Length; i++)
				{
					var p = Properties[i];
					if (p != null)
					{
						if (p.MustEscapeRecord)
						{
							sw.Write('"');
							//TODO string.Empty !?
							p.InsertRecord(sw, cms.TmpBuffer, "1", null);
							sw.Write('"');
						}
						else p.InsertRecord(sw, cms.TmpBuffer, string.Empty, null);
					}
					if (i < Properties.Length - 1)
						sw.Write(',');
				}
				sw.Write(')');
			}
			sw.Flush();
			cms.Position = 0;
			return cms;
		}

		public void InsertRecord(TextWriter sw, char[] buf, string escaping, Action<TextWriter, char> mappings)
		{
			if (Properties == null)
				return;
			sw.Write('(');
			var newEscaping = escaping + '1';
			string quote = null;
			for (int i = 0; i < Properties.Length; i++)
			{
				var p = Properties[i];
				if (p != null)
				{
					if (p.MustEscapeRecord)
					{
						//TODO: build quote only once and reuse it, instead of looping all the time
						quote = quote ?? PostgresTuple.BuildQuoteEscape(escaping);
						if (mappings != null)
							foreach (var q in quote)
								mappings(sw, q);
						else
							sw.Write(quote);
						p.InsertRecord(sw, buf, newEscaping, mappings);
						if (mappings != null)
							foreach (var q in quote)
								mappings(sw, q);
						else
							sw.Write(quote);
					}
					else p.InsertRecord(sw, buf, escaping, mappings);
				}
				if (i < Properties.Length - 1)
					sw.Write(',');
			}
			sw.Write(')');
		}

		public void InsertArray(TextWriter sw, char[] buf, string escaping, Action<TextWriter, char> mappings)
		{
			if (Properties == null)
			{
				sw.Write("NULL");
				return;
			}
			sw.Write('(');
			var newEscaping = escaping + '1';
			string quote = null;
			for (int i = 0; i < Properties.Length; i++)
			{
				var p = Properties[i];
				if (p != null)
				{
					if (p.MustEscapeRecord)
					{
						quote = quote ?? PostgresTuple.BuildQuoteEscape(escaping);
						if (mappings != null)
							foreach (var q in quote)
								mappings(sw, q);
						else
							sw.Write(quote);
						p.InsertRecord(sw, buf, newEscaping, mappings);
						if (mappings != null)
							foreach (var q in quote)
								mappings(sw, q);
						else
							sw.Write(quote);
					}
					else p.InsertRecord(sw, buf, escaping, mappings);
				}
				if (i < Properties.Length - 1)
					sw.Write(',');
			}
			sw.Write(')');
		}
	}
}
