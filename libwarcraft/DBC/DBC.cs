//
//  DBC.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2016 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Warcraft.Core;
using Warcraft.Core.Extensions;
using Warcraft.DBC.Definitions;
using Warcraft.DBC.SpecialFields;

namespace Warcraft.DBC
{
	/// <summary>
	/// DBC file handler. Parses and presents DBC files in a statically typed, easy to use fashion.
	/// </summary>
	public class DBC<T> : IDBC, IReadOnlyList<T> where T : DBCRecord, new()
	{
		/// <summary>
		/// The header of the database file. Describes the sizes and field counts of the records in the database.
		/// </summary>
		private readonly DBCHeader Header;

		/// <summary>
		/// The number of held records.
		/// </summary>
		public int RecordCount => (int)this.Header.RecordCount;

		/// <summary>
		/// The number of fields in each record.
		/// </summary>
		public int FieldCount => (int)this.Header.FieldCount;

		/// <summary>
		/// The absolute size of each record.
		/// </summary>
		public int RecordSize => (int)this.Header.RecordSize;

		/// <summary>
		/// The absolute size of the string block.
		/// </summary>
		public int StringBlockSize => (int)this.Header.StringBlockSize;

		/// <summary>
		/// The game version this database is valid for. This affects how the records are parsed, and is vital for
		/// getting correct data.
		/// </summary>
		public WarcraftVersion Version
		{
			get;
			private set;
		}

		/// <summary>
		/// The <see cref="BinaryReader"/> which holds the data of the database.
		/// </summary>
		private readonly byte[] DatabaseContents;

		private readonly long StringBlockOffset;

		/// <summary>
		/// The strings in the DBC file.
		/// </summary>
		public readonly Dictionary<long, string> Strings = new Dictionary<long, string>();

		/// <summary>
		/// The records in the database. Used as a procedural cache.
		/// </summary>
		private readonly List<T> Records;

		/// <summary>
		/// Initializes a new instance of the <see cref="DBC"/> class.
		/// </summary>
		/// <param name="inVersion">In version.</param>
		/// <param name="data">ExtendedData.</param>
		public DBC(WarcraftVersion inVersion, byte[] data)
		{
			this.Version = inVersion;
			this.DatabaseContents = data;

			using (BinaryReader databaseReader = new BinaryReader(new MemoryStream(this.DatabaseContents)))
			{
				this.Header = new DBCHeader(databaseReader.ReadBytes(DBCHeader.GetSize()));

				// Seek to and read the string block
				databaseReader.BaseStream.Seek(this.Header.RecordCount * this.Header.RecordSize, SeekOrigin.Current);
				this.StringBlockOffset = databaseReader.BaseStream.Position;
				while (databaseReader.BaseStream.Position != databaseReader.BaseStream.Length)
				{
					this.Strings.Add(databaseReader.BaseStream.Position - this.StringBlockOffset, databaseReader.ReadNullTerminatedString());
				}
			}

			this.Records = new List<T>(this.Count);

			// Initialize the record list with null values
			for (int i = 0; i < this.Count; ++i)
			{
				this.Records.Add(null);
			}
		}

		/// <summary>
		/// Gets a record from the database by its primary key ID.
		/// </summary>
		/// <returns>The record</returns>
		/// <param name="id">Primary key ID.</param>
		public T GetRecordByID(int id)
		{
			return this.FirstOrDefault(record => record.ID == id);
		}

		/// <summary>
		/// Resolves a string reference. String references are stored as offsets into the string data block
		/// of the database. This function looks up the matching string and stores it in the reference object.
		/// </summary>
		/// <returns>The string reference.</returns>
		/// <param name="reference">Reference.</param>
		public void ResolveStringReference(StringReference reference)
		{
			if (this.Strings.ContainsKey(reference.Offset))
			{
				reference.Value = this.Strings[reference.Offset];
				return;
			}

			reference.Value = "";
		}

		/// <summary>
		/// Determines whether or not the record at the given index has been cached.
		/// </summary>
		/// <param name="index">The index of the record.</param>
		/// <returns>true if the record has been cached; otherwise, false.</returns>
		internal bool HasCachedRecordAtIndex(int index)
		{
			return this.Records[index] != null;
		}

		/// <summary>
		/// Caches the given record at the given index.
		/// </summary>
		/// <param name="record">The record to cache.</param>
		/// <param name="index">The index where to cache the record.</param>
		/// <exception cref="InvalidOperationException">Thrown if there is already a record cached at the given index.</exception>
		internal void CacheRecordAtIndex(T record, int index)
		{
			if (HasCachedRecordAtIndex(index))
			{
				throw new InvalidOperationException("A record was already cached at the given index.");
			}

			this.Records[index] = record;
		}

		/*
			Enumeration implementation
		*/

		/// <inheritdoc />
		public int Count => this.RecordCount;

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <inheritdoc />
		public IEnumerator<T> GetEnumerator()
		{
			return new DBCEnumerator<T>(this, this.DatabaseContents, this.StringBlockOffset);
		}

		/*
			Indexing implementation
		*/

		/// <inheritdoc />
		public T this[int i]
		{
			get
			{
				if (HasCachedRecordAtIndex(i))
				{
					return this.Records[i];
				}

				using (BinaryReader databaseReader = new BinaryReader(new MemoryStream(this.DatabaseContents)))
				{
					long recordOffset = DBCHeader.GetSize() + this.Header.RecordSize * i;
					databaseReader.BaseStream.Seek(recordOffset, SeekOrigin.Begin);

					T record = databaseReader.ReadRecord<T>((int)this.Header.FieldCount, (int)this.Header.RecordSize, this.Version);

					foreach (var stringReference in record.GetStringReferences())
					{
						ResolveStringReference(stringReference);
					}

					this.Records[i] = record;

					return record;
				}
			}
		}
	}
}

