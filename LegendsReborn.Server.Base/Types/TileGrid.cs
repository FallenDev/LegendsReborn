using Darkages.Object;
using Darkages.Sprites;

namespace Darkages.Types;

public class TileGrid : ObjectManager
{
    private readonly Area _map;
    private readonly int _x, _y;

    public TileGrid(Area map, int x, int y)
    {
        _map = map;
        _x = x;
        _y = y;
    }

    public List<Sprite> Sprites => GetObjects(_map,
            o => (o.X == _x) && (o.Y == _y) && o.Alive,
            Get.Monsters | Get.Mundanes | Get.Aislings)
        .ToList();

    public bool IsPassable(Sprite sprite, bool fromAisling)
    {
        //var length = 0;
        lock (Sprites)
        {
            if (sprite is Monster monster && monster.Template.IgnoreCollision)
                return true;

            //can an aisling walk over a monster
            //can a normal monster walk over an aisling

            var position = new Position(_x, _y);
            var objectsOnSpot = Sprites.Where(obj => obj != null && obj.Position.Equals(position)).ToList();

            return !objectsOnSpot.Any();
        }
    }
}