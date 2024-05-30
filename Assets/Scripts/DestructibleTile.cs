using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class DestructibleTile
{
    public Vector3Int position;
    public TileBase tile;

    public DestructibleTile(Vector3Int position, TileBase tile)
    {
        this.position = position;
        this.tile = tile;
    }
}
