﻿//
//  WDT.cs
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

using Warcraft.ADT.Chunks;
using Warcraft.WDT.Chunks;
using System.IO;
using System.Collections.Generic;
using Warcraft.Core.Extensions;

namespace Warcraft.WDT
{
	public class WorldTable
	{
		public TerrainVersion Version;
		public WorldTableHeader Header;
		public WorldTableAreaInfo AreaInfo;
		public TerrainWorldModelObjects WorldModelObjects;
		public TerrainWorldModelObjectPlacementInfo WorldModelObjectPlacementInfo;

		public WorldTable(byte[] data)
		{
			using (MemoryStream ms = new MemoryStream(data))
			{
				using (BinaryReader br = new BinaryReader(ms))
				{
					this.Version = br.ReadIFFChunk<TerrainVersion>();
					this.Header = br.ReadIFFChunk<WorldTableHeader>();
					this.AreaInfo = br.ReadIFFChunk<WorldTableAreaInfo>();
					this.WorldModelObjects = br.ReadIFFChunk<TerrainWorldModelObjects>();

					if (this.WorldModelObjects.Filenames.Count > 0)
					{
						this.WorldModelObjectPlacementInfo = br.ReadIFFChunk<TerrainWorldModelObjectPlacementInfo>();
					}
				}
			}
		}

		/// <summary>
		/// Gets a list of loaded areas. When working with unloaded files (i.e, not in a game or application),
		/// this method will never return any areas.
		/// </summary>
		/// <returns>The loaded areas.</returns>
		public IEnumerable<AreaInfoEntry> GetLoadedAreas()
		{
			foreach (AreaInfoEntry entry in this.AreaInfo.Entries)
			{
				if (entry.Flags.HasFlag(AreaInfoFlags.IsLoaded))
				{
					yield return entry;
				}
			}
		}

		/// <summary>
		/// Gets a list of area information entries that have terrain tiles.
		/// </summary>
		/// <returns>The areas with terrain.</returns>
		public IEnumerable<AreaInfoEntry> GetAreasWithTerrain()
		{
			foreach (AreaInfoEntry entry in this.AreaInfo.Entries)
			{
				if (entry.Flags.HasFlag(AreaInfoFlags.HasTerrainData))
				{
					yield return entry;
				}
			}
		}

		/// <summary>
		/// Gets the area info for the specified coordinates.
		/// </summary>
		/// <returns>The area info.</returns>
		/// <param name="inTileX">In tile x.</param>
		/// <param name="inTileY">In tile y.</param>
		public AreaInfoEntry GetAreaInfo(uint inTileX, uint inTileY)
		{
			return this.AreaInfo.GetAreaInfo(inTileX, inTileY);
		}

		/// <summary>
		/// Determines whether this instance has any terrain.
		/// </summary>
		/// <returns><c>true</c> if this instance has any terrain; otherwise, <c>false</c>.</returns>
		public bool HasAnyTerrain()
		{
			foreach (AreaInfoEntry entry in this.AreaInfo.Entries)
			{
				if (entry.Flags.HasFlag(AreaInfoFlags.HasTerrainData))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Determines whether the tile at the specified coordinates is populated with terrain data or not.
		/// </summary>
		/// <returns><c>true</c> if the tile is populated; otherwise, <c>false</c>.</returns>
		/// <param name="inTileX">0-based x coordinate of the tile.</param>
		/// <param name="inTileY">0-based y coordinate of the tile.</param>
		public bool IsTilePopulated(uint inTileX, uint inTileY)
		{
			AreaInfoEntry infoEntry = this.AreaInfo.GetAreaInfo(inTileX, inTileY);
			return infoEntry.Flags.HasFlag(AreaInfoFlags.HasTerrainData);
		}

		public byte[] Serialize()
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter bw = new BinaryWriter(ms))
				{
					bw.WriteIFFChunk(this.Version);
					bw.WriteIFFChunk(this.Header);
					bw.WriteIFFChunk(this.AreaInfo);
					bw.WriteIFFChunk(this.WorldModelObjects);

					if (this.WorldModelObjects.Filenames.Count > 0 && this.WorldModelObjectPlacementInfo != null)
					{
						bw.WriteIFFChunk(this.WorldModelObjectPlacementInfo);
					}
				}

				return ms.ToArray();
			}
		}
	}
}

