using Darkages.Common;
using Darkages.Enums;
using Darkages.Models;
using Darkages.ScriptingBase;
using Darkages.Sprites;

namespace Darkages.Types;

public class Area : Map
{
     private static readonly byte[] Sotp = File.ReadAllBytes("sotp.dat");
    public byte[] Data;
    public ushort Hash;
    public bool Ready;
    private readonly object _mapLoadLock = new();
    public TileGrid[,] ObjectGrid { get; private set; }
    public TileContent[,] Tile { get; private set; }
    public Dictionary<string, AreaScript> Scripts { get; set; } = [];
    public string FilePath { get; set; }

    public void UpdateMusic()
    {
        var players = GetObjects(this, s => true, Get.Aislings);
        foreach (var player in players)
        {
            player.Client.SendMusic();
        }
    }

    public IEnumerable<byte> GetRowData(int row)
    {
        try
        {
            var buffer = new byte[Width * 6];
            var bPos = 0;
            var dPos = row * Width * 6;

            for (var i = 0; i < Width; i++, bPos += 6, dPos += 6)
            {
                buffer[bPos + 0] = Data[dPos + 1];
                buffer[bPos + 1] = Data[dPos + 0];
                buffer[bPos + 2] = Data[dPos + 3];
                buffer[bPos + 3] = Data[dPos + 2];
                buffer[bPos + 4] = Data[dPos + 5];
                buffer[bPos + 5] = Data[dPos + 4];
            }

            return buffer;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return default;
    }

    public bool IsOccupied(int x, int y)
    {
        if (x < 0 || x >= Width) return true;
        if (y < 0 || y >= Height) return true;

        var isSprite = Tile[x, y] is TileContent.Aisling or TileContent.Monster or TileContent.Mundane or TileContent.Chest or TileContent.Wall;
        return isSprite;
    }

    public bool IsWall(int x, int y)
    {
        if (x < 0 || x >= Width) return true;
        if (y < 0 || y >= Height) return true;

        var isWall = Tile[x, y] is TileContent.Wall or TileContent.Chest;
        return isWall;
    }

    public bool IsChest(int x, int y)
    {
        if (x < 0 || x >= Width) return true;
        if (y < 0 || y >= Height) return true;

        var isChest = Tile[x, y] == TileContent.Chest;
        return isChest;
    }

    public bool OnLoaded()
    {
        lock (_mapLoadLock)
        {
            Tile = new TileContent[Width, Height];
            ObjectGrid = new TileGrid[Width, Height];

            using var stream = new MemoryStream(Data);
            using var reader = new BinaryReader(stream);
            var index = 0;

            try
            {
                reader.BaseStream.Seek(0, SeekOrigin.Begin);

                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        ObjectGrid[x, y] = new TileGrid(this, x, y);
                        var leftForeground = ((ushort)(Data[index++] | Data[index++] << 8));
                        var rightForeground = ((ushort)(Data[index++] | Data[index++] << 8));

                        reader.BaseStream.Seek(2, SeekOrigin.Current);

                        if (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            var a = reader.ReadInt16();
                            var b = reader.ReadInt16();

                            if (ParseMapWalls(a, b))
                                if (LEGENDS_CONSTANTS.CHEST_SPRITES.Contains(leftForeground) ||
                                    LEGENDS_CONSTANTS.CHEST_SPRITES.Contains(rightForeground))
                                    Tile[x, y] = TileContent.Chest;
                                else
                                    Tile[x, y] = TileContent.Wall;
                            else
                                Tile[x, y] = TileContent.None;
                        }
                        else
                        {
                            Tile[x, y] = TileContent.Wall;
                        }
                    }
                }

                foreach (var block in Blocks)
                    Tile[block.X, block.Y] = TileContent.Wall;

                Ready = true;
            }
            catch (Exception ex)
            {
                Console.Write($"Map# {ID} did not load\n");
                return false;
            }
        }

        return Ready;
    }

    private static bool ParseMapWalls(short lWall, short rWall)
    {
        if (lWall == 0 && rWall == 0) return false;
        if (lWall == 0) return Sotp[rWall - 1] == 0x0F;
        if (rWall == 0) return Sotp[lWall - 1] == 0x0F;

        var left = Sotp[lWall - 1];
        var right = Sotp[rWall - 1];

        return left == 0x0F || right == 0x0F;
    }

    public void Update(in TimeSpan elapsedTime)
    {
        if (Scripts != null)
        {
            var script = Scripts.Values.FirstOrDefault();
            script?.Update(elapsedTime);
        }

        UpdateAreaObjects(elapsedTime);
    }

    private void UpdateAreaObjects(TimeSpan elapsedTime)
    {
        var objectCache = GetObjects(this, sprite => (sprite != null) && sprite.AislingsOnMap().Any(), Get.Items);

        foreach (var obj in objectCache)
            if (obj != null)
            {
                switch (obj)
                {
                    case Item item:
                        {
                            var stale = !((DateTime.UtcNow - item.AbandonedDate).TotalMinutes > 1);

                            if (stale)
                                item.AuthenticatedAislings = null;

                            break;
                        }
                }

                obj.LastUpdated = DateTime.UtcNow;
            }
    }
}